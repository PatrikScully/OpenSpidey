"""Generate synthetic movement fixtures by compiling the recovered C++ rules."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path, help="Also validate collision-free steps against the original PC binary (Linux/i386)")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    harness = r'''
#include "game/player/player_motion.h"
#include "game/player/sweep_geometry.h"
#include <cmath>
#include <iostream>
using namespace spidey::player;
using VECTOR = Vector;
'''
    harness += controller_method(maths, "void VectorNormal(VECTOR* a1, VECTOR* a2)")
    harness += controller_method(maths, "i32 M3dMaths_SquareRoot0(i32 i)")
    harness += r'''
void emit(const CVector& value) { std::cout << value.vx << ' ' << value.vy << ' ' << value.vz << ' '; }
int main() {
    CVector velocity, acceleration, normal;
    int dx, dy, dz, ticks, reach, length, horizontalLength;
    while (std::cin >> velocity.vx >> velocity.vy >> velocity.vz
           >> acceleration.vx >> acceleration.vy >> acceleration.vz
           >> dx >> dy >> dz >> normal.vx >> normal.vy >> normal.vz
           >> ticks >> reach >> length >> horizontalLength) {
        CVector updated = velocity;
        PlayerMotion::updateVelocity(updated, acceleration, CFriction(dx, dy, dz));
        emit(updated);
        emit(PlayerMotion::displacement(velocity, ticks));
        emit(PlayerMotion::collisionFreeDisplacement(velocity, ticks));
        emit(PlayerMotion::velocityFromDisplacement(velocity, ticks));
        CVector slide = velocity;
        PlayerMotion::slideHorizontally(slide, normal);
        emit(slide);
        std::cout << PlayerMotion::fallDisplacement(velocity.vy, ticks) << ' '
                  << PlayerMotion::dampedValue(velocity.vx, dx) << ' '
                  << SweepGeometry::scaledSquaredLength(velocity) << ' '
                  << SweepGeometry::scaledSquaredLength(velocity, true) << ' '
                  << SweepGeometry::reachForTicks(reach, ticks) << ' ';
        emit(SweepGeometry::mainRay(velocity, reach, ticks, length));
        emit(SweepGeometry::shoulderSide(velocity, reach, length));
        emit(SweepGeometry::shoulderForward(velocity, reach, ticks, length, horizontalLength));
        emit(CVector(10,20,30) + PlayerMotion::collisionFreeDisplacement(updated, ticks));
        VECTOR normalized{velocity.vx,velocity.vy,velocity.vz};
        VectorNormal(&normalized,&normalized);
        std::cout << normalized.vx << ' ' << normalized.vy << ' ' << normalized.vz << ' '
                  << M3dMaths_SquareRoot0(SweepGeometry::scaledSquaredLength(velocity)) << ' ';
        std::cout << '\n';
    }
}
'''
    randomizer = random.Random(0x491BB827)
    ticks = [-1, 0, 1, 2, 3, 4, 5, 6, 7, 31, 2147483647]
    values = [-2147483648, -4097, -2049, -2048, 0, 2048, 2049, 4097, 2147483647]
    cases = []
    for index in range(611):
        if index < len(ticks) * len(values):
            value = values[index // len(ticks)]
            velocity = [value, values[(index // len(ticks) + 2) % len(values)], value]
        else:
            velocity = [randomizer.randint(-2147483648, 2147483647) for _ in range(3)]
        case = dict(velocity=velocity,
                    acceleration=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                    damping=[randomizer.choice([0, 1, 2, 6, 31, 32, 63, 255]) for _ in range(3)],
                    normal=randomizer.choice([[4096, 0, 0], [-4096, 0, 0], [0, 0, 4096],
                                              [0, 0, -4096], [2896, 0, 2896], [0, 0, 0]]),
                    elapsedTicks=ticks[index % len(ticks)],
                    reach=randomizer.choice([0, 70, -70, 4096, 2147483647]),
                    length=randomizer.randint(1, 65535), horizontalLength=randomizer.randint(1, 65535))
        cases.append(case)
    requests = "".join(" ".join(map(str, case["velocity"] + case["acceleration"] + case["damping"] +
                                  case["normal"] + [case["elapsedTicks"], case["reach"],
                                                    case["length"], case["horizontalLength"]])) + "\n"
                       for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-motion-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", "-I", str(root / "src"),
                        str(path / "reference.cpp"), str(root / "src/core/fixed_vector.cpp"),
                        "-o", str(path / "reference")], check=True)
        result = subprocess.run([str(path / "reference")], input=requests, text=True,
                                capture_output=True, check=True)
    for case, line in zip(cases, result.stdout.splitlines(), strict=True):
        output = list(map(int, line.split()))
        if len(output) != 36:
            raise RuntimeError("Unexpected movement reference output shape.")
        case["result"] = output[:29]
        case["collisionFreePosition"] = output[29:32]
        case["normalizedVelocity"] = output[32:35]
        case["originalLength"] = output[35]
    if args.original_exe:
        validate_original(root, args.original_exe.resolve(), cases, requests)
    destination = root / "modern/OpenSpidey.Tests/Fixtures/player-motion.json"
    destination.write_text(json.dumps(cases, indent=2) + "\n")
    print(f"Generated {len(cases)} recovered C++ movement/sweep fixtures: {destination}")


def validate_original(root, executable, cases, requests):
    harness = r'''
#include "game/player/player_motion.h"
#include "original_executable.h"
#include <iostream>
template<class Value> void put(void* object, unsigned offset, const Value& value) {
    std::memcpy(static_cast<u8*>(object)+offset, &value, sizeof(value));
}
int main(int argc, char** argv) {
    static_assert(sizeof(void*) == 4, "The original executable uses 32-bit pointers.");
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x81,0xEC,0x1C,0x01,0x00,0x00};
    assert(!std::memcmp(reinterpret_cast<void*>(0x466CE0), prologue, sizeof(prologue)));
    typedef void (__attribute__((thiscall)) *Original)(void*);
    CVector velocity, acceleration, normal;
    int dx, dy, dz, ticks, reach, length, horizontalLength, index = 0;
    while (std::cin >> velocity.vx >> velocity.vy >> velocity.vz
           >> acceleration.vx >> acceleration.vy >> acceleration.vz
           >> dx >> dy >> dz >> normal.vx >> normal.vy >> normal.vz
           >> ticks >> reach >> length >> horizontalLength) {
        alignas(4) u8 player[0x1000] = {};
        const CVector position(10,20,30);
        put(player, 8, position);
        put(player, 0x60, velocity);
        put(player, 0x6C, acceleration);
        put(player, 0x78, CFriction(dx,dy,dz));
        put(player, 0x80, ticks);
        put(player, 0xE1C, i32(0x40000));
        put(player, 0x12A, u16(index++ % 2 ? 270 : 271));
        put(player, 0x128, u16(13));
        reinterpret_cast<Original>(0x466CE0)(player);
        CVector updated, moved;
        std::memcpy(&updated, player+0x60, sizeof(updated));
        std::memcpy(&moved, player+8, sizeof(moved));
        moved -= position;
        std::cout << updated.vx << ' ' << updated.vy << ' ' << updated.vz << ' '
                  << moved.vx << ' ' << moved.vy << ' ' << moved.vz << '\n';
    }
}
'''
    with tempfile.TemporaryDirectory(prefix="openspidey-motion-original-") as directory:
        path = Path(directory)
        (path / "original.cpp").write_text(harness)
        subprocess.run(["c++", "-m32", "-std=c++17", "-O0", "-fwrapv", "-fno-pie", "-no-pie",
                        "-I", str(root / "src"), "-I", str(root / "tests/runtime"),
                        str(path / "original.cpp"), str(root / "src/core/fixed_vector.cpp"),
                        "-o", str(path / "original")], check=True)
        result = subprocess.run([str(path / "original"), str(executable)], input=requests, text=True,
                                capture_output=True, check=True)
        for index, (case, line) in enumerate(zip(cases, result.stdout.splitlines(), strict=True)):
            expected_move = [word(value - initial) for value, initial in zip(case["collisionFreePosition"], [10, 20, 30], strict=True)]
            expected = case["result"][:3] + expected_move
            if list(map(int, line.split())) != expected:
                raise RuntimeError(f"Original executable collision-free movement differs at sample {index}.")
    print(f"Validated {len(cases)} collision-free movement steps against original executable instructions.")


def word(value):
    return (value + 2147483648) % 4294967296 - 2147483648


if __name__ == "__main__":
    main()
