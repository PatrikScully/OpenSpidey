"""Compile original CBaddy count statements and optionally compare their actual PC instructions.

The PC constructor runs unchanged except for its CSuper base constructor. The
destructor comparison executes only its exact counter load/decrement/store
instructions; Windows exception bookkeeping, diagnostics and child cleanup are
outside this arithmetic fixture.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/actors/baddy.cpp').read_text()
    constructor = controller_method(source, 'CBaddy::CBaddy(void)')
    destructor = controller_method(source, 'CBaddy::~CBaddy(void)')
    increment = next(line.strip() for line in constructor.splitlines() if 'G_NUM_BADDIES++' in line)
    decrement = next(line.strip() for line in destructor.splitlines() if '--G_NUM_BADDIES' in line)
    code = r'''
#include <cassert>
#include <cstdint>
#include <cstring>
#include <iostream>
#include <memory>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
int32_t G_NUM_BADDIES;
struct CBaddy {
    uint8_t field_21D;
    CBaddy();
    ~CBaddy();
};
'''
    code += 'CBaddy::CBaddy() {' + increment + '}\n'
    code += 'CBaddy::~CBaddy() {' + decrement + '}\n'
    code += r'''
#ifdef VALIDATE_ORIGINAL
void __attribute__((thiscall)) pcSuperConstructor(void*) {}
void patch(unsigned address, void* target) {
    auto* bytes = reinterpret_cast<unsigned char*>(address);
    bytes[0] = 0xE9;
    *reinterpret_cast<unsigned*>(bytes + 1) = reinterpret_cast<unsigned>(target) - address - 5;
}
using PcConstructor = void (__attribute__((thiscall)) *)(void*);
#endif
struct Lease {
    int ordinal;
    std::unique_ptr<CBaddy> actor;
    alignas(4) unsigned char pcActor[0x324] = {};
};
int main(int argc, char** argv) {
#ifdef VALIDATE_ORIGINAL
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    patch(0x460720, reinterpret_cast<void*>(&pcSuperConstructor));
    auto* counter = reinterpret_cast<int32_t*>(0x56E98C);
    // Actual 402DA1 load, then 402DAA decrement and store. Omit only add esp,8.
    void* decrementCode = mmap(nullptr, 4096, PROT_READ | PROT_WRITE | PROT_EXEC,
        MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    assert(decrementCode != MAP_FAILED);
    auto* bytes = static_cast<unsigned char*>(decrementCode);
    const unsigned char load[] = {0x8B,0x0D,0x8C,0xE9,0x56,0x00};
    const unsigned char decrease[] = {0x49,0x89,0x0D,0x8C,0xE9,0x56,0x00};
    assert(std::memcmp(reinterpret_cast<void*>(0x402DA1), load, sizeof(load)) == 0);
    assert(std::memcmp(reinterpret_cast<void*>(0x402DAA), decrease, sizeof(decrease)) == 0);
    std::memcpy(bytes, reinterpret_cast<void*>(0x402DA1), sizeof(load));
    std::memcpy(bytes + sizeof(load), reinterpret_cast<void*>(0x402DAA), sizeof(decrease));
    bytes[sizeof(load) + sizeof(decrease)] = 0xC3;
#endif
    int initial, length;
    while (std::cin >> initial >> length) {
        G_NUM_BADDIES = initial;
#ifdef VALIDATE_ORIGINAL
        *counter = initial;
#endif
        std::vector<std::unique_ptr<Lease>> leases;
        std::cout << '[';
        for (int operationIndex = 0; operationIndex < length; ++operationIndex) {
            int operation;
            std::cin >> operation;
            int ordinal;
            unsigned phase;
            if (operation == 0) {
                auto lease = std::make_unique<Lease>();
                ordinal = lease->ordinal = G_NUM_BADDIES;
                lease->actor = std::make_unique<CBaddy>();
                phase = lease->actor->field_21D;
#ifdef VALIDATE_ORIGINAL
                reinterpret_cast<PcConstructor>(0x402C00)(lease->pcActor);
                assert(*counter == G_NUM_BADDIES && lease->pcActor[0x21D] == phase);
#endif
                leases.push_back(std::move(lease));
            } else {
                Lease& lease = *leases.at(operation - 1);
                assert(lease.actor);
                ordinal = lease.ordinal;
                phase = lease.actor->field_21D;
                lease.actor.reset();
#ifdef VALIDATE_ORIGINAL
                reinterpret_cast<void(*)()>(decrementCode)();
                assert(*counter == G_NUM_BADDIES);
#endif
            }
            if (operationIndex) std::cout << ',';
            std::cout << '[' << G_NUM_BADDIES << ',' << ordinal << ',' << phase << ']';
        }
        std::cout << "]\n";
        // Any remaining actual source objects are destroyed, outside the observed sequence.
    }
}
'''
    rng = random.Random(0x402C00)
    cases = []
    initials = [-2147483648, -2147483647, -257, -256, -255, -1, 0, 1,
                254, 255, 256, 257, 2147483646, 2147483647]
    for initial in initials + [rng.randint(-2147483648, 2147483647) for _ in range(256)]:
        operations = []
        live = []
        created = 0
        for index in range(32):
            if not live or index < 4 or rng.randrange(3) != 0:
                operations.append(0)
                created += 1
                live.append(created)
            else:
                chosen = rng.choice(live)
                live.remove(chosen)
                operations.append(chosen)
        cases.append(dict(initial=initial, operations=operations))
    with tempfile.TemporaryDirectory(prefix='openspidey-baddy-construction-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        flags = ['-m32', '-fno-pie', '-no-pie', '-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        rows = [' '.join(map(str, [case['initial'], len(case['operations']), *case['operations']])) for case in cases]
        output = subprocess.check_output(command, input=('\n'.join(rows) + '\n').encode()).decode().splitlines()
    for case, result in zip(cases, output, strict=True):
        case['expected'] = json.loads(result)
    target = root / 'modern/OpenSpidey.Tests/Fixtures/baddy-construction.json'
    target.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} construction/deletion sequences, {sum(len(case["operations"]) for case in cases)} operations; ' +
          ('actual PC count/phase instructions match recovered statements.' if args.original_exe else 'compiled original count/phase statements.'))


if __name__ == '__main__':
    main()
