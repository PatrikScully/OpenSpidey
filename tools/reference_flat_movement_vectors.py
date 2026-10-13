"""Compile recovered free-plane movement finalization and speed-ramp rules with identity GTE/world stubs."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    finalizer = (root / "src/game/player/player_frame_finalizer.cpp").read_text()
    preparation = (root / "src/game/player/player_frame_preparation.cpp").read_text()
    player_source = (root / "src/game/player/spidey.cpp").read_text()
    harness = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#include <cstring>
#include <cstdlib>
struct MATRIX { short m[3][3]; };
using VECTOR = Vector;
VECTOR working, transformed;
void print_if_false(bool, const char*) {}
void M3dMaths_SetIdentityRotation(MATRIX*) {}
void gte_SetRotMatrix(MATRIX*) {}
void gte_ldlvl(VECTOR* v) { working = *v; }
void gte_rtir() { transformed = Vector{(working.vx << 12) >> 12, (working.vy << 12) >> 12, (working.vz << 12) >> 12}; }
void gte_stlvnl(VECTOR* v) { *v = transformed; }
short sines[4096], cosines[4096];
int Sine(int angle) { return sines[angle & 4095]; }
int Cosine(int angle) { return cosines[angle & 4095]; }
struct Camera { int mCameraMode = 0; } camera;
Camera* G_CAMERA_LIST = &camera;
const int CAMERAMODE_LOOKAROUND = 17;
CVector boss;
void* gBossRelated = &boss;
int Utils_XZDist(CVector*, CVector*) { return 0; }
void Utils_CalcAim(CSVector*, CVector*, CVector*) {}
struct Held { int field_10C; } held;
struct CPlayer {
    CVector mVel;
    char field_E2D, field_E2E;
    unsigned mActionState;
    unsigned short mAnim;
    short field_E32, effectiveHeading;
    CSVector mAngles;
    CVector mAcc, field_A8{0,-4096,0}, mOrientationReferenceDirection;
    int field_DFC = 1, mHeadingTurnTicksRemaining = 0, mHeadingTurnStep = 0, mTargetHeading = 0, mHeadingDelta = 0;
    int mAnimSpeed, field_EBC, mElapsedTicks;
    unsigned char field_8E8 = 0, field_8EA, field_AE4, field_AE5, field_E8C, mIsCrawling;
    unsigned char mArenaConstraintActive = 0;
    int mArenaConstraintRadius = 0;
    CVector mPos;
    MATRIX mTransform;
    Held* mHeldObject;
    short GetEffectiveHeading() { return effectiveHeading; }
    void SetTargetTorsoAngle(short, bool);
    int GetPerpendicularisationRadius() { return 0; }
};
struct PlayerFrameFinalizer { CPlayer& player_; void updateMovementVelocity(int cameraYaw); void updateHeadingAndGravity(); };
struct PlayerFramePreparation { CPlayer& player_; void updateMovementRamp(); };
'''
    harness += controller_method(finalizer, "void PlayerFrameFinalizer::updateMovementVelocity(i32 cameraYaw)")
    harness += controller_method(preparation, "void PlayerFramePreparation::updateMovementRamp()")
    harness += controller_method(player_source, "void CPlayer::SetTargetTorsoAngle(i16 a2, bool a3)")
    harness += controller_method(finalizer, "void PlayerFrameFinalizer::updateHeadingAndGravity()")
    harness += r'''
int main() {
    for (int i = 0; i < 4096; i++) { double a = i * 0.001536096911877394; sines[i] = std::sin(a) * 4096.0; cosines[i] = std::cos(a) * 4096.0; }
    int f, s, heading, input, cameraHeading, anim, enabled, lookaround, crawling, holding, small, suppress, airSuppressed;
    int mode;
    while (std::cin >> mode) {
        CPlayer p{};
        if (mode == 1) {
            int wanted, factor, crawl, fast;
            std::cin >> heading >> wanted >> p.mActionState >> factor >> crawl >> fast
                >> p.mHeadingTurnTicksRemaining >> p.mTargetHeading >> p.mHeadingTurnStep >> p.mElapsedTicks;
            p.effectiveHeading = heading; p.field_DFC = factor; p.mIsCrawling = crawl;
            p.SetTargetTorsoAngle(wanted, fast);
            PlayerFrameFinalizer{p}.updateHeadingAndGravity();
            std::cout << p.mAngles.vy << ' ' << p.mTargetHeading << ' ' << p.mHeadingTurnTicksRemaining << ' ' << p.mHeadingTurnStep << '\n';
            continue;
        }
        std::cin >> p.mVel.vx >> p.mVel.vy >> p.mVel.vz >> f >> s >> heading >> input >> cameraHeading
           >> p.mActionState >> p.field_EBC >> anim >> p.mAnimSpeed >> enabled >> lookaround >> crawling >> holding >> small
           >> suppress >> airSuppressed >> p.mElapsedTicks;
        p.field_E2D = f; p.field_E2E = s; p.effectiveHeading = heading; p.field_E32 = input; p.mAnim = anim;
        p.field_AE4 = enabled; p.field_8EA = lookaround; p.mIsCrawling = crawling; p.field_AE5 = suppress; p.field_E8C = airSuppressed;
        held.field_10C = small ? 8 : 0; p.mHeldObject = holding ? &held : nullptr;
        PlayerFrameFinalizer{p}.updateMovementVelocity(cameraHeading);
        PlayerFramePreparation{p}.updateMovementRamp();
        std::cout << p.mVel.vx << ' ' << p.mVel.vy << ' ' << p.mVel.vz << ' ' << p.mAnimSpeed << ' ' << p.field_EBC << '\n';
    }
}
'''
    randomizer = random.Random(0x4B72AD)
    cases = []
    for index in range(1024):
        values = [randomizer.randint(-2147483648, 2147483647) for _ in range(3)]
        values += [0, 0] if index % 3 == 0 else [randomizer.randint(-128, 127), randomizer.randint(-128, 127)]
        values += [randomizer.randint(-32768, 32767) for _ in range(3)]
        values += [randomizer.choice([1, 2, 4, 16, 0x400000, 0x40040004, 0xBFF7F8C1]),
                   randomizer.choice([-2147483648, -1, 0, 1, 15, 16, 2147483647]), randomizer.choice([0, 21, 50, 51, 52]),
                   randomizer.choice([0x10000, 0, -1, 0x8000])]
        values += [int(randomizer.randrange(5) == 0), int(randomizer.randrange(5) == 0)]
        values += [randomizer.randrange(2) for _ in range(5)]
        values += [randomizer.choice([-2147483648, -1, 0, 1, 2, 4, 6, 2147483647])]
        cases.append(dict(input=values))
    turns = []
    for _ in range(1024):
        values = [randomizer.randint(-32768, 32767), randomizer.randint(-32768, 32767),
                  randomizer.choice([1, 2, 4, 16, 0x2000000, 0x2000002]), randomizer.choice([1, 2, 4]),
                  randomizer.randrange(2), randomizer.randrange(2), randomizer.randint(0, 40),
                  randomizer.randrange(4096), randomizer.randint(-4096, 4096), randomizer.choice([0, 1, 2, 4, 6, 100])]
        turns.append(dict(input=values))
    requests = "".join("0 " + " ".join(map(str, case["input"])) + "\n" for case in cases)
    requests += "".join("1 " + " ".join(map(str, case["input"])) + "\n" for case in turns)
    with tempfile.TemporaryDirectory(prefix="openspidey-flat-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", "-fsigned-char", "-I", str(root / "src"),
                        str(path / "reference.cpp"), str(root / "src/core/fixed_vector.cpp"), "-o", str(path / "reference")], check=True)
        result = subprocess.run([str(path / "reference")], input=requests, text=True, capture_output=True, check=True)
    for case, line in zip(cases + turns, result.stdout.splitlines(), strict=True):
        case["result"] = list(map(int, line.split()))
    target = root / "modern/OpenSpidey.Tests/Fixtures/flat-movement.json"
    target.write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    (target.parent / "heading-turns.json").write_text(json.dumps(turns, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} recovered free-plane movement and {len(turns)} heading-turn fixtures.")


if __name__ == "__main__":
    main()
