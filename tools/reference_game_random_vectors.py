"""Generate original Utils_InitialRand/Rnd sequences; optionally compare the PC machine code."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / "src/core/utils.cpp").read_text()
    code = r'''
#include <cassert>
#include <iostream>
#include "core/types.h"
int G_RND_RELATED_ONE, G_RND_RELATED_TWO, G_RND_RELATED_THREE;
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
'''
    code += controller_method(source, "void Utils_InitialRand(int a)") + "\n"
    code += controller_method(source, "int Rnd(i32 n)") + "\n"
    code += r'''
int main(int argc, char** argv) {
#ifdef VALIDATE_ORIGINAL
    assert(argc == 2); original_executable::loadOriginalImage(argv[1]);
#endif
    int seed, count;
    while (std::cin >> seed >> count) {
        Utils_InitialRand(seed);
#ifdef VALIDATE_ORIGINAL
        reinterpret_cast<void(*)(int)>(0x4E5D80)(seed);
#endif
        std::cout << '[';
        for (int index = 0; index < count; ++index) {
            int bound; std::cin >> bound; int value = Rnd(bound);
#ifdef VALIDATE_ORIGINAL
            int original = reinterpret_cast<int(*)(int)>(0x4E5DA0)(bound);
            assert(value == original);
            assert(G_RND_RELATED_ONE == *reinterpret_cast<int*>(0x6B4C7C));
            assert(G_RND_RELATED_TWO == *reinterpret_cast<int*>(0x6B4B90));
            assert(G_RND_RELATED_THREE == *reinterpret_cast<int*>(0x6B4B94));
#endif
            if (index) std::cout << ',';
            std::cout << '[' << value << ',' << G_RND_RELATED_ONE << ',' << G_RND_RELATED_TWO << ',' << G_RND_RELATED_THREE << ']';
        }
        std::cout << "]\n";
    }
}
'''
    rng = random.Random(0x4E5DA0)
    bounds = [0, 1, 2, 3, 5, 16, 32, 480, -1, -32, 32767, 32768, 65535, 65536, 2147483647, -2147483648]
    seeds = [0, 1, -1, 0x12A6CC58, 2147483647, -2147483648] + [rng.randint(-2147483648, 2147483647) for _ in range(122)]
    cases = [dict(seed=seed, bounds=bounds + [rng.choice(bounds) for _ in range(16)]) for seed in seeds]
    requests = "".join(f"{case['seed']} {len(case['bounds'])} " + " ".join(map(str, case['bounds'])) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-random-reference-") as folder:
        path = Path(folder)
        (path / "reference.cpp").write_text(code)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["g++", "-std=c++17", "-O0", "-fwrapv", *flags, "-I", str(root / "tests/runtime"), "-I", str(root / "src"),
                        str(path / "reference.cpp"), "-o", str(path / "reference")], check=True)
        command = [str(path / "reference")]
        if args.original_exe:
            command.append(str(args.original_exe.resolve()))
        output = subprocess.check_output(command, input=requests.encode()).decode().splitlines()
    for case, line in zip(cases, output, strict=True):
        case["expected"] = json.loads(line)
    (root / "modern/OpenSpidey.Tests/Fixtures/game-random.json").write_text(json.dumps(cases, separators=(",", ":")) + "\n")
    print(f"Generated {len(cases)} original random sequences / {sum(len(case['bounds']) for case in cases)} updates.")
    if args.original_exe:
        print("Every output and state word matches original PC Utils_InitialRand/Rnd machine code.")


if __name__ == "__main__":
    main()
