"""Generate actor hook fixtures from recovered C++ affine math and original hook constants."""
import argparse
import json
from pathlib import Path
import random
import re
import struct
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def original_bytes(executable, address, size):
    data = executable.read_bytes()
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    sections = struct.unpack_from("<H", data, pe + 6)[0]
    optional_size = struct.unpack_from("<H", data, pe + 20)[0]
    image_base = struct.unpack_from("<I", data, pe + 24 + 28)[0]
    section_table = pe + 24 + optional_size
    rva = address - image_base
    for index in range(sections):
        virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from(
            "<IIII", data, section_table + index * 40 + 8)
        if virtual_address <= rva and rva + size <= virtual_address + min(virtual_size, raw_size):
            offset = raw_offset + rva - virtual_address
            return data[offset:offset + size]
    raise ValueError(f"Original hook packet address {address:#x} is outside backed PE sections.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original-exe", type=Path, help="Verify packet bytes and affine vectors with privately installed PC machine code (Linux/i386)")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    packets = []
    original = [("thug", 0x557DA8, 16, [(0, 200, 0, 9), (0, -150, -240, 3)]),
                ("henchman", 0x557E74, 15, [(-100, 0, -350, 7), (0, 0, -380, 9),
                                          (100, 0, -350, 10), (0, 0, -380, 14)])]
    for name, address, parts, hooks in original:
        encoded = struct.pack("<HH", 0, len(hooks)) + b"".join(struct.pack("<hhhH", *hook) for hook in hooks)
        if args.original_exe and original_bytes(args.original_exe, address, len(encoded)) != encoded:
            raise RuntimeError(f"{name}: private executable hook packet differs from the source constants.")
        packets.append(dict(name=name, address=address, partCount=parts, bytes=encoded.hex(), hooks=hooks))

    maths = (root / "src/engine/rendering/ps2funcs.cpp").read_text()
    hook_source = (root / "src/engine/rendering/m3dutils.cpp").read_text()
    dynamic = controller_method(hook_source, "void M3dUtils_GetDynamicHookPosition(")
    finish = dynamic[dynamic.index("\tpOut->vx <<= 12;"):]
    harness = r'''
#include <cstdint>
#include <iostream>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using i16 = int16_t;
struct VECTOR { int32_t vx, vy, vz; };
struct SMatrix { i16 m[3][3]; i16 t[3]; };
struct MATRIX { i16 m[3][3]; int32_t t[3]; };
struct CSuper { VECTOR mPos; };
VECTOR G_GENERAL_LONG_VECTOR;
'''
    affine = controller_method(maths, "void gsub_46F820(")
    # Original 0x46F820 uses __allmul/ADC/__allshr for every dot product. The recovered
    # source currently omits these long casts; restore them in this isolated reference.
    affine = re.sub(r"(p(?:PoseFrame|Transform)->m\[\d\]\[\d\])", r"int64_t(\1)", affine)
    harness += affine
    harness += "\nvoid finish(VECTOR* pOut, CSuper* pSuper) {\n" + finish
    harness += r'''
int main(int argc, char** argv) {
#ifdef VALIDATE_ORIGINAL
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x83, 0xEC, 0x20};
    assert(!std::memcmp(reinterpret_cast<void*>(0x46F820), prologue, sizeof(prologue)));
    static_assert(sizeof(SMatrix) == 24 && sizeof(MATRIX) == 32);
    *reinterpret_cast<u32*>(0x6B2454) = 1; // Region zero model table: only checked for non-null on this path.
    *reinterpret_cast<u16*>(0x6B2478) = 16;
#endif
    CSuper actor;
    MATRIX world;
    SMatrix part;
    i16 hook[4];
    while (std::cin >> actor.mPos.vx >> actor.mPos.vy >> actor.mPos.vz
           >> world.t[0] >> world.t[1] >> world.t[2]) {
        for (auto& row : world.m) for (auto& value : row) std::cin >> value;
        for (auto& row : part.m) for (auto& value : row) std::cin >> value;
        for (auto& value : part.t) std::cin >> value;
        for (auto& value : hook) std::cin >> value;
        gsub_46F820(hook, &part, &world);
        VECTOR result = G_GENERAL_LONG_VECTOR;
#ifdef VALIDATE_ORIGINAL
        reinterpret_cast<void (*)(void*, SMatrix*, MATRIX*)>(0x46F820)(hook, &part, &world);
        VECTOR original = *reinterpret_cast<VECTOR*>(0x610BA0);
        assert(original.vx == result.vx && original.vy == result.vy && original.vz == result.vz);
#endif
        finish(&result, &actor);
#ifdef VALIDATE_ORIGINAL
        alignas(4) u8 originalActor[0x200] = {};
        SMatrix poses[16] = {};
        poses[hook[3]] = part;
        SMatrix* posePointer = poses;
        originalActor[4] = 4; // Use the current pose buffer, bypassing animation packet selection.
        originalActor[0x12C] = 2; // Dynamic hooks do not apply the render/static-hook mirror flag.
        std::memcpy(originalActor + 8, &actor.mPos, sizeof(actor.mPos));
        std::memcpy(originalActor + 0x164, &world, sizeof(world));
        std::memcpy(originalActor + 0x184, &posePointer, sizeof(posePointer));
        VECTOR dynamicResult;
        reinterpret_cast<void (*)(VECTOR*, void*, void*)>(0x453EE0)(&dynamicResult, originalActor, hook);
        assert(dynamicResult.vx == result.vx && dynamicResult.vy == result.vy && dynamicResult.vz == result.vz);
#endif
        std::cout << result.vx << ' ' << result.vy << ' ' << result.vz << '\n';
    }
}
'''
    identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096]
    yaw = [0, 0, 4096, 0, 4096, 0, -4096, 0, 0]
    mouth = [0, 20, -100, 13]
    cases = [dict(position=[409600, -409600, 204800], translation=[100, -100, 50],
                  rotation=identity, part=identity + [16, 32, -16], hook=mouth),
             dict(position=[100, -100, 5000], translation=[0, -1, 1],
                  rotation=identity, part=identity + [0, 0, 0], hook=[0, 0, 0, 0]),
             dict(position=[0, 0, 0], translation=[0, 0, 0],
                  rotation=yaw, part=identity + [32760, -32760, 0], hook=[20, -20, 0, 14])]
    randomizer = random.Random(0x46F820)
    words = [-2147483648, -4097, -4096, -1, 0, 1, 4095, 4096, 2147483647]
    shorts = [-32768, -32767, -4096, -1, 0, 1, 4096, 32767]
    for index in range(397):
        cases.append(dict(position=[randomizer.choice(words) for _ in range(3)],
                          translation=[randomizer.randint(-2147483648, 2147483647) for _ in range(3)],
                          rotation=randomizer.choice([identity, yaw, [randomizer.choice(shorts) for _ in range(9)]]),
                          part=[randomizer.choice(shorts) for _ in range(12)],
                          hook=randomizer.choice([mouth, *original[0][3], *original[1][3],
                                                  [randomizer.choice(shorts) for _ in range(3)] + [randomizer.randrange(16)]])))
    requests = "".join(" ".join(map(str, case["position"] + case["translation"] + case["rotation"] +
                                  case["part"] + list(case["hook"]))) + "\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-hook-reference-") as directory:
        path = Path(directory)
        (path / "reference.cpp").write_text(harness)
        flags = ["-m32", "-fno-pie", "-no-pie", "-DVALIDATE_ORIGINAL"] if args.original_exe else []
        subprocess.run(["c++", "-std=c++17", "-O0", "-fwrapv", *flags,
                        "-I", str(root / "src"), "-I", str(root / "tests/runtime"),
                        str(path / "reference.cpp"), "-o", str(path / "reference")], check=True)
        command = [str(path / "reference")]
        if args.original_exe:
            command.append(str(args.original_exe.resolve()))
        result = subprocess.run(command, input=requests, text=True, capture_output=True, check=True)
    for case, line in zip(cases, result.stdout.splitlines(), strict=True):
        case["expected"] = list(map(int, line.split()))
    output = root / "modern/OpenSpidey.Tests/Fixtures/actor-hooks.json"
    output.write_text(json.dumps(dict(packets=packets, cases=cases), separators=(",", ":")) + "\n")
    print(f"Wrote {len(packets)} hook packets and {len(cases)} native affine vectors to {output}")
    if args.original_exe:
        print("All affine stages and dynamic hook positions match original PC 0x46F820 and 0x453EE0 machine code.")


if __name__ == "__main__":
    main()
