"""Generate ground-response fixtures by compiling the recovered controller method with actor stubs."""
import json
from pathlib import Path
import random
import subprocess
import tempfile


def controller_method(source, signature="void PlayerPhysicsController::respondToGround("):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 1
    position = opening + 1
    while depth:
        if source[position] == "{":
            depth += 1
        elif source[position] == "}":
            depth -= 1
        position += 1
    return source[start:position]


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/game/player/player_physics_controller.cpp").read_text()
    harness = r'''
#include "game/player/player_motion.h"
#include <iostream>
using spidey::player::PlayerMotion;
namespace core = spidey::core;
struct CBody { u16 mFlags; };
struct SLineInfo { CVector Position, Normal; i32 Distance; u32* pFace; CBody* pItem; };
struct Player {
    CVector mPos, mVel, mShadowPos, normal;
    u16 mCollision, mSurfaceClearance;
    u8 mGroundContactGraceUpdates = 0;
    void* mHeldObject;
    CFriction mFric;
    int field_574 = 1, field_578 = 2, brightness = 0;
    bool notified = false;
    CVector& crawlSurfaceNormal() { return normal; }
    void AdjustBrightness(int value) { brightness = value; }
};
struct Policy { bool limitVerticalFallAfterGroundHit; };
struct PlayerPhysicsController {
    Player& player_;
    CVector initialPosition_;
    Policy policy_;
    void respondToGround(SLineInfo& ground, i32 fall, i32 savedFallVelocity);
    void notifySupportBody(const SLineInfo& ground) { player_.notified = (ground.pItem->mFlags & 0x100) != 0; }
};
'''
    harness += controller_method(source)
    harness += r'''
void emit(const CVector& value) { std::cout << value.vx << ' ' << value.vy << ' ' << value.vz << ' '; }
int main() {
    Player player;
    CVector initial;
    SLineInfo ground;
    u32 flags;
    int distance, support, holding, clearance, collisions, dx, dy, dz, fall, savedVelocity, limit;
    while (std::cin >> player.mPos.vx >> player.mPos.vy >> player.mPos.vz
           >> initial.vx >> initial.vy >> initial.vz
           >> player.mVel.vx >> player.mVel.vy >> player.mVel.vz
           >> ground.Position.vx >> ground.Position.vy >> ground.Position.vz
           >> ground.Normal.vx >> ground.Normal.vy >> ground.Normal.vz
           >> distance >> flags >> support >> holding >> clearance >> collisions
           >> dx >> dy >> dz >> fall >> savedVelocity >> limit) {
        u32 face[4] = {0,0,0,flags};
        CBody body{u16(support ? 0x100 : 0)};
        ground.Distance = distance;
        ground.pFace = face;
        ground.pItem = &body;
        player.mHeldObject = holding ? &body : nullptr;
        player.mSurfaceClearance = clearance;
        player.mCollision = collisions;
        player.mFric = CFriction(dx,dy,dz);
        player.notified = false;
        PlayerPhysicsController controller{player,initial,Policy{limit != 0}};
        controller.respondToGround(ground,fall,savedVelocity);
        emit(player.mPos);
        emit(player.mVel);
        std::cout << player.mCollision << ' ' << unsigned(player.mGroundContactGraceUpdates) << ' ';
        emit(player.normal);
        emit(player.mShadowPos);
        std::cout << player.notified << ' ' << (player.brightness == player.field_574) << '\n';
    }
}
'''
    randomizer = random.Random(0x466CE0)
    cases = []
    for holding in [False, True]:
        for normal_y in [-4096, -2601, -2600, -2599, 0, 4096]:
            for distance in [-1, 0, 1]:
                for limit in [False, True]:
                    for fall in [-8193, 0, 8193]:
                        cases.append(dict(holding=holding, normalY=normal_y, distance=distance,
                                          limit=limit, fall=fall))
    for index in range(256):
        cases.append(dict(holding=bool(index % 2), normalY=randomizer.randint(-4096, 4096),
                          distance=randomizer.choice([-2147483648, 0, 2147483647]),
                          limit=bool(index % 3), fall=randomizer.randint(-2147483648, 2147483647)))
    for case in cases:
        case.update(position=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                    initial=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                    velocity=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                    contact=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                    normal=[randomizer.randint(-4096, 4096), case.pop("normalY"), randomizer.randint(-4096, 4096)],
                    flags=randomizer.choice([0, 0x4000000, 0x800000, 0x4800000]),
                    support=bool(randomizer.getrandbits(1)), clearance=randomizer.choice([0, 96, 65535]),
                    collisions=randomizer.getrandbits(16),
                    damping=[randomizer.choice([0, 1, 4, 31, 32, 255]) for _ in range(3)],
                    savedVelocity=randomizer.randint(-2147483648, 2147483647))
    requests = "".join(" ".join(map(str, case["position"] + case["initial"] + case["velocity"] +
                                  case["contact"] + case["normal"] + [case["distance"], case["flags"],
                                  int(case["support"]), int(case["holding"]), case["clearance"], case["collisions"]] +
                                  case["damping"] + [case["fall"], case["savedVelocity"], int(case["limit"])])) + "\n"
                       for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-ground-response-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", "-I", str(root / "src"),
                        str(path / "reference.cpp"), str(root / "src/core/fixed_vector.cpp"),
                        "-o", str(path / "reference")], check=True)
        result = subprocess.run([str(path / "reference")], input=requests, text=True,
                                capture_output=True, check=True)
    for case, line in zip(cases, result.stdout.splitlines(), strict=True):
        case["result"] = list(map(int, line.split()))
        if len(case["result"]) != 16:
            raise RuntimeError("Unexpected ground-response reference output shape.")
    destination = root / "modern/OpenSpidey.Tests/Fixtures/ground-response.json"
    destination.write_text(json.dumps(cases, indent=2) + "\n")
    print(f"Generated {len(cases)} recovered ground-response fixtures.")


if __name__ == "__main__":
    main()
