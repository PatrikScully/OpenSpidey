"""Generate decoder parity fixtures using the recovered C++ reference, without game assets."""
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parent.parent
    reference = (root / "src/engine/rendering/animation_decoder.cpp").read_text()
    table_start = reference.index("static const u8 gDecompressStreamModeTable")
    table_end = reference.index("};", table_start) + 2
    function_start = reference.index("static u8* DecompressStream(")
    function_end = reference.index("\n// @Ok", function_start)
    harness = """#include <cstdint>
#include <iostream>
#include <vector>
using u8 = uint8_t; using i16 = int16_t; using i32 = int32_t;
""" + reference[table_start:table_end] + reference[function_start:function_end] + """
int main() {
    int samples, size;
    while (std::cin >> samples >> size) {
        std::vector<u8> source(size + 3, 0);
        for (int i = 0, value; i < size; ++i) { std::cin >> value; source[i] = value; }
        std::vector<i16> output(samples);
        u8* end = DecompressStream(source.data(), output.data(), 1, samples);
        std::cout << (end - source.data());
        for (auto value : output) std::cout << ' ' << value;
        std::cout << '\\n';
    }
}
"""
    randomizer = random.Random(433)
    cases = []
    for mode in range(16):
        for block in range(1, 17):
            samples = 2 * block + block // 2 + 1
            source = bytes([(block - 1) << 4 | mode]) + randomizer.randbytes(256)
            cases.append((samples, source))
        cases.append((1, bytes([mode]) + randomizer.randbytes(8)))
    requests = "".join(f"{samples} {len(source)} " + " ".join(map(str, source)) + "\n"
                       for samples, source in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-animation-oracle-") as directory:
        path = Path(directory)
        (path / "oracle.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", str(path / "oracle.cpp"),
                        "-o", str(path / "oracle")], check=True)
        result = subprocess.run([str(path / "oracle")], input=requests, text=True,
                                capture_output=True, check=True)
    vectors = []
    for (samples, source), line in zip(cases, result.stdout.splitlines(), strict=True):
        consumed, *values = map(int, line.split())
        if len(values) != samples or not 0 < consumed <= len(source):
            raise ValueError("Invalid reference result")
        vectors.append(dict(source=source[:consumed].hex(), values=values))
    target = root / "modern/OpenSpidey.Tests/Fixtures/animation-channels.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(vectors, separators=(",", ":")) + "\n")
    print(f"Generated {len(vectors)} cases from the recovered C++ decoder: {target}")
    generate_pose_vectors(root)


def generate_pose_vectors(root):
    reference = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    start = reference.index("MATRIX* RotMatrixYXZ(")
    end = reference.index("\n// @Ok", start)
    harness = """#include <cmath>
#include <cstdint>
#include <iostream>
using i16 = int16_t;
struct SVECTOR { i16 vx, vy, vz; };
struct MATRIX { i16 m[3][3]; i16 t[3]; };
""" + reference[start:end] + """
int main() {
    int value;
    while (std::cin >> value) {
        i16 raw[3][6]; raw[0][0] = value;
        for (int i = 1; i < 18; ++i) { std::cin >> value; raw[i/6][i%6] = value; }
        MATRIX matrices[3];
        int parents[] = {2, 0, 2}; int order[] = {2, 0, 1};
        for (int part : order) {
            SVECTOR angles {raw[part][0], raw[part][1], raw[part][2]};
            RotMatrixYXZ(&angles, &matrices[part]);
            for (int axis = 0; axis < 3; ++axis) {
                int translation = raw[part][3 + axis];
                if (parents[part] != part) {
                    MATRIX& parent = matrices[parents[part]];
                    translation = parent.m[axis][0]*raw[part][3] + parent.m[axis][1]*raw[part][4] + parent.m[axis][2]*raw[part][5];
                    translation = static_cast<i16>(translation >> 12);
                    translation += parent.t[axis];
                }
                matrices[part].t[axis] = static_cast<i16>(translation);
            }
        }
        for (auto& matrix : matrices) {
            for (auto& row : matrix.m) for (auto cell : row) std::cout << cell << ' ';
            for (auto cell : matrix.t) std::cout << cell << ' ';
        }
        std::cout << '\\n';
    }
}
"""
    randomizer = random.Random(454)
    cases = [[0] * 18]
    for _ in range(64):
        cases.append([randomizer.randint(-32768, 32767) for _ in range(18)])
    requests = "".join(" ".join(map(str, raw)) + "\n" for raw in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-pose-oracle-") as directory:
        path = Path(directory)
        (path / "oracle.cpp").write_text(harness)
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", str(path / "oracle.cpp"),
                        "-o", str(path / "oracle")], check=True)
        result = subprocess.run([str(path / "oracle")], input=requests, text=True,
                                capture_output=True, check=True)
    vectors = []
    for raw, line in zip(cases, result.stdout.splitlines(), strict=True):
        transforms = list(map(int, line.split()))
        if len(transforms) != 36:
            raise ValueError("Invalid pose reference result")
        vectors.append(dict(joints=raw, transforms=transforms))
    target = root / "modern/OpenSpidey.Tests/Fixtures/animation-poses.json"
    target.write_text(json.dumps(vectors, separators=(",", ":")) + "\n")
    print(f"Generated {len(vectors)} hierarchy pose cases using recovered rotation code: {target}")


if __name__ == "__main__":
    main()
