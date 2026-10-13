"""Generate synthetic matrix-animation fixtures with the recovered C++ interpolation routine."""
import json
from pathlib import Path
import random
import subprocess
import tempfile


def function(source, signature):
    start = source.index(signature)
    return source[start:source.index("\n// @Ok", start)]


def main():
    root = Path(__file__).resolve().parent.parent
    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    animation = (root / "src/engine/rendering/m3dutils.cpp").read_text()
    harness = """#include <cstdint>
#include <cstring>
#include <iostream>
#include <vector>
using i16 = int16_t; using i32 = int32_t; using u8 = uint8_t; using u16 = uint16_t; using u32 = uint32_t;
struct CSuper { int mAnim = 0; int mFrame = 0; int mAnimMode = 0; };
struct Vector { int vx, vy, vz; };
Vector G_GENERAL_LONG_VECTOR, G_OP12_RESULT;
int G_SCALAR;
i16* gTweenBuffer;
enum {GT_SEVEN=7, GT_EIGHT=8, GT_NINE=9, GT_ELEVEN=11, GT_TWELVE=12, GT_THIRTEEN=13};
void gte_lddp(int value) { G_SCALAR = value; }
void MTC2(int value, int reg) {
    if (reg==GT_SEVEN) G_OP12_RESULT.vx=value;
    if (reg==GT_EIGHT) G_OP12_RESULT.vy=value;
    if (reg==GT_NINE) G_OP12_RESULT.vz=value;
    if (reg==GT_ELEVEN) G_GENERAL_LONG_VECTOR.vx=value;
    if (reg==GT_TWELVE) G_GENERAL_LONG_VECTOR.vy=value;
    if (reg==GT_THIRTEEN) G_GENERAL_LONG_VECTOR.vz=value;
}
"""
    harness += function(maths, "void gsub_46E090(void)")
    harness += function(maths, "i16* gsub_46E430(i16 *a1)")
    harness += function(animation, "void M3dUtils_InterpolateVectors(i32 NumVectors")
    harness += """
int main() {
    int frames, interval, parts;
    while (std::cin >> frames >> interval >> parts) {
        int size = ((frames + interval - 1) / interval) * parts * 12;
        std::vector<i16> keys(size), output(parts * 12);
        for (int i = 0, value; i < size; ++i) { std::cin >> value; keys[i] = value; }
        std::vector<u32> file(3 + (size + 1) / 2);
        file[0]=1; file[1]=12; file[2]=frames | ((interval - 1) << 16);
        std::memcpy(file.data()+3, keys.data(), size * 2);
        gTweenBuffer = output.data();
        CSuper actor;
        for (int frame=0; frame<frames; ++frame) {
            for (int mode=0; mode<2; ++mode) {
                actor.mFrame=frame; actor.mAnimMode=mode;
                if (interval==1) std::memcpy(output.data(), keys.data()+frame*parts*12, parts*24);
                else M3dUtils_InterpolateVectors(parts*4, interval, file.data(), &actor, 0, parts);
                for (auto value : output) std::cout << value << ' ';
            }
        }
        std::cout << '\\n';
    }
}
"""
    randomizer = random.Random(454270)
    cases = []
    for frames in (1, 2, 3, 5, 6, 7, 12, 17):
        for interval in (1, 2, 3, 4, 6, 99):
            for parts in (1, 3):
                count = ((frames + interval - 1) // interval) * parts * 12
                keys = [randomizer.randint(-32768, 32767) for _ in range(count)]
                cases.append(dict(frames=frames, interval=interval, parts=parts, keys=keys))
    requests = "".join(f"{case['frames']} {case['interval']} {case['parts']} " +
                       " ".join(map(str, case['keys'])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-matrix-oracle-") as directory:
        path = Path(directory)
        (path / "oracle.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", str(path / "oracle.cpp"),
                        "-o", str(path / "oracle")], check=True)
        result = subprocess.run([str(path / "oracle")], input=requests, text=True,
                                capture_output=True, check=True)
    for case, line in zip(cases, result.stdout.splitlines(), strict=True):
        values = list(map(int, line.split()))
        stride = case["parts"] * 12
        if len(values) != case["frames"] * 2 * stride:
            raise ValueError("Invalid reference matrix result")
        case["expected"] = [values[index:index + stride] for index in range(0, len(values), stride)]
    target = root / "modern/OpenSpidey.Tests/Fixtures/matrix-animation.json"
    target.write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} cases using recovered matrix interpolation: {target}")


if __name__ == "__main__":
    main()
