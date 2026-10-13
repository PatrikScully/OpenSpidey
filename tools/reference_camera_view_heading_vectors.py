"""Capture actual PC LoadIntoMikeCamera heading23A from the real final display quaternion, including its degenerate retention gate."""
import argparse
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);int x,y,z,w,old;while(std::cin>>x>>y>>z>>w>>old){alignas(4)unsigned char camera[0x2A4]={};int*rotation=reinterpret_cast<int*>(camera+0x214);rotation[0]=x;rotation[1]=y;rotation[2]=z;rotation[3]=w;*reinterpret_cast<short*>(camera+0x23A)=old;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416A20)(camera);std::cout<<*reinterpret_cast<short*>(camera+0x23A)<<'\n';}}
'''
    rng = random.Random(0x416A20)
    cases = [[0, 0, 0, 4096, 777], [0, 0, 0, 0, 777], [2896, 0, 0, 2896, 777], [0, 2896, 0, 2896, 777], [2048, 2048, 2048, 2048, 777]]
    for _ in range(320):
        values = [rng.uniform(-1, 1) for _ in range(4)]; length = math.sqrt(sum(value * value for value in values))
        cases.append([int(value * 4096 / length) for value in values] + [rng.randrange(4096)])
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-view-heading-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-view-heading.json').write_text(json.dumps([dict(input=case, expected=int(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC LoadIntoMikeCamera final-display heading23A cases.')


if __name__ == '__main__': main()
