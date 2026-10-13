"""Capture whole original4B9740 initial lookaround pitch setter, including signed value extension and source no-write branches."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);long long v[7];while(std::cin>>v[0]){for(int i=1;i<7;i++)std::cin>>v[i];*reinterpret_cast<int*>(0x6A8260)=v[3];*reinterpret_cast<int*>(0x6A81FC)=v[4];*reinterpret_cast<int*>(0x6A8208)=v[5];*reinterpret_cast<int*>(0x6A818C)=v[6];reinterpret_cast<void(__attribute__((stdcall)) *)(unsigned short,unsigned short,short)>(0x4B9740)(v[0],v[1],v[2]);std::cout<<'['<<*reinterpret_cast<int*>(0x6A8260)<<','<<*reinterpret_cast<int*>(0x6A81FC)<<','<<*reinterpret_cast<int*>(0x6A8208)<<','<<*reinterpret_cast<int*>(0x6A818C)<<"]\n";}}
'''
    cases = [[surface, parameter, pitch, 170, 170, 170, 123456] for surface in [0, 1, 2, 3, 65535]
             for parameter in [0, 1, 65535] for pitch in [-32768, -800, 0, 100, 170, 32767]]
    rng = random.Random(0x4B9740)
    for _ in range(300):
        cases.append([rng.choice([0, 1, 2, rng.randrange(65536)]), rng.choice([0, rng.randrange(65536)]),
                      rng.randrange(-32768, 32768), *[rng.randint(-2147483648, 2147483647) for _ in range(4)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-lookaround-camera-value-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-lookaround-camera-value.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} unpatched original initial lookaround pitch writes: floor/wall/ceiling selection, signed16 extension, parameter/nonexistent-profile no-write and unchanged active pitch.')


if __name__ == '__main__':
    main()
