#!/usr/bin/env python3
"""Regenerate independent compiled fixtures for the recovered integer aiming formulas."""
import json
import random
import shutil
import subprocess
import tempfile
from pathlib import Path

OUTPUT = Path(__file__).resolve().parents[1] / "modern/OpenSpidey.Tests/Fixtures/aim-vectors.json"
CPP = r'''
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <iostream>
using i32 = std::int32_t;
struct Vector { i32 x, y, z; };
i32 root(i32 v) { return v <= -32768 ? 32768 : v < 0 ? 0 : i32(std::sqrt(double(v))); }
i32 angle(i32 v) { return i32(std::atan(double(v) / 4096.0) * 651.0006103515625); }
i32 sine(i32 v) { return std::int16_t(std::sin((v & 4095) * 0.001536096911877394) * 4096.0); }
i32 cosine(i32 v) { return std::int16_t(std::cos((v & 4095) * 0.001536096911877394) * 4096.0); }
int main() {
    Vector from, to; i32 magnitude;
    while (std::cin >> from.x >> from.y >> from.z >> to.x >> to.y >> to.z >> magnitude) {
        i32 x = (to.x - from.x) >> 12, y = (to.y - from.y) >> 12, z = (to.z - from.z) >> 12;
        i32 yaw = z > 0 ? 2048 - angle(-((x << 12) / z)) : z < 0 ? angle((x << 12) / z) : x > 0 ? -1024 : 1024;
        i32 horizontal = root(x*x + z*z);
        i32 pitch = horizontal ? (y > 0 ? angle((y << 12) / horizontal) : -angle((-4096 * y) / horizontal)) : y > 0 ? 1024 : -1024;
        pitch &= 4095; yaw &= 4095;
        i32 amount = (cosine(pitch) * magnitude) >> 12;
        x = (from.x - to.x) >> 12; y = (from.y - to.y) >> 12; z = (from.z - to.z) >> 12;
        std::cout << pitch << ' ' << yaw << ' ' << -(amount * sine(yaw)) << ' ' << sine(pitch) * magnitude << ' '
            << -(amount * cosine(yaw)) << ' ' << root(x*x + y*y + z*z) << '\n';
    }
}
'''


def main():
    compiler = shutil.which("g++")
    if compiler is None:
        raise SystemExit("Install g++ to regenerate aiming fixtures.")
    cases = [[0, 0, 0, 0, 0, 0, 240]]
    for target in [(100, 0, 0), (-100, 0, 0), (0, 100, 0), (0, -100, 0), (0, 0, 100), (0, 0, -100)]:
        cases.append([0, 0, 0, *(component * 4096 for component in target), 240])
    rng = random.Random(0x4E6310)
    for _ in range(512):
        cases.append([*(rng.randint(-20000 * 4096, 20000 * 4096) for _ in range(6)), rng.choice([0, 1, 26, 40, 128, 240, 4096])])
    with tempfile.TemporaryDirectory(prefix="openspidey-aim-") as directory:
        source = Path(directory) / "aim.cpp"
        binary = Path(directory) / "aim"
        source.write_text(CPP)
        subprocess.run([compiler, "-std=c++17", "-O2", "-fwrapv", str(source), "-o", str(binary)], check=True)
        result = subprocess.run([str(binary)], input="\n".join(" ".join(map(str, values)) for values in cases) + "\n",
                                text=True, capture_output=True, check=True)
    outputs = [list(map(int, line.split())) for line in result.stdout.splitlines()]
    if len(outputs) != len(cases):
        raise RuntimeError("The compiled reference did not return every aiming case.")
    OUTPUT.write_text(json.dumps([{"input": values, "result": output} for values, output in zip(cases, outputs)], indent=2) + "\n")
    print(f"Wrote {len(cases)} compiled aiming cases to {OUTPUT}")


if __name__ == "__main__":
    main()
