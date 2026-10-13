"""Generate float-word and ground-probe fixtures from recovered C++, optionally checking original x87 code."""
import argparse
import json
from pathlib import Path
import random
import struct
import subprocess
import tempfile


def bits(value):
    return struct.unpack("<I", struct.pack("<f", value))[0]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path, help="Validate float conversion with the original PC binary (Linux/i386)")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    harness = r'''
#include "core/floating_word.h"
#include "game/player/ground_probe_fan.h"
#include <iostream>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
int main(int argc, char** argv) {
#ifdef VALIDATE_ORIGINAL
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x55,0x8B,0xEC};
    assert(!std::memcmp(reinterpret_cast<void*>(0x529C7C),prologue,3));
#endif
    u32 mode, xBits, zBits, spreadBits, index;
    while (std::cin >> mode >> xBits) {
        float x;
        std::memcpy(&x,&xBits,4);
        if (mode == 0) {
            i32 word = spidey::core::truncateToGameWord(x);
#ifdef VALIDATE_ORIGINAL
            i32 original;
            u32 entry = 0x529C7C;
            asm volatile("flds %1; call *%2" : "=&a"(original) : "m"(x), "r"(entry)
                : "edx","ecx","st","memory","cc");
            assert(original == word);
#endif
            std::cout << word << '\n';
        } else {
            std::cin >> zBits >> spreadBits >> index;
            float z, spread;
            std::memcpy(&z,&zBits,4);
            std::memcpy(&spread,&spreadBits,4);
            const auto point = spidey::player::GroundProbeFan::point(x,z,spread,index);
            u32 px, pz;
            std::memcpy(&px,&point.x,4);
            std::memcpy(&pz,&point.z,4);
            std::cout << px << ' ' << pz << '\n';
        }
    }
}
'''
    randomizer = random.Random(0xCF93771)
    word_bits = [0, 0x80000000, 0x7f800000, 0xff800000, 0x7fc00000, 0xffc00000,
                 0x7f800001, 0xff800001, 0x5f000000, 0xdf000000, 0x5effffff, 0xdeffffff]
    word_bits += [bits(value) for value in [-7.75, 7.75, 2147483648, -2147483648, 4294967296, -4294967296]]
    word_bits += [randomizer.getrandbits(32) for _ in range(512)]
    words = [dict(bits=value) for value in word_bits]
    probes = []
    for x, z, spread in [(100, 200, 10), (-17491, -14420, 10), (100.5, -200.25, 7.5),
                         (2147483648, -2147483648, 10), (-100, 200, -5)]:
        for index in range(10):
            probes.append(dict(x=bits(x), z=bits(z), spread=bits(spread), index=index))
    requests = "".join(f"0 {case['bits']}\n" for case in words)
    requests += "".join(f"1 {case['x']} {case['z']} {case['spread']} {case['index']}\n" for case in probes)
    with tempfile.TemporaryDirectory(prefix="openspidey-ground-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", *flags, "-I", str(root / "src"),
                        "-I", str(root / "tests/runtime"), str(path / "reference.cpp"),
                        "-o", str(path / "reference")], check=True)
        command = [str(path / "reference")]
        if args.original_exe:
            command.append(str(args.original_exe.resolve()))
        result = subprocess.run(command, input=requests, text=True, capture_output=True, check=True)
    lines = result.stdout.splitlines()
    if len(lines) != len(words) + len(probes):
        raise RuntimeError("Unexpected ground-probe reference output shape.")
    for case, line in zip(words, lines[:len(words)], strict=True):
        case["word"] = int(line)
    for case, line in zip(probes, lines[len(words):], strict=True):
        case["result"] = list(map(int, line.split()))
    destination = root / "modern/OpenSpidey.Tests/Fixtures/ground-probes.json"
    destination.write_text(json.dumps(dict(words=words, probes=probes), indent=2) + "\n")
    print(f"Generated {len(words)} float-word and {len(probes)} ground-probe fixtures.")
    if args.original_exe:
        print(f"All {len(words)} float-word conversions match original x87 instructions.")


if __name__ == "__main__":
    main()
