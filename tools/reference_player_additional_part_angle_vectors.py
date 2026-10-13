"""Capture genuine original M3dUtils_GetPartAngles454040 with real fixed matrix products and atan2, including degenerate/signed rotations."""
import argparse
import json
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
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);int v[18];while(std::cin>>v[0]){for(int i=1;i<18;i++)std::cin>>v[i];alignas(4)unsigned char body[0x1C0]={},pose[24]={};body[4]=4;at<void*>(body,0x184)=pose;for(int i=0;i<9;i++){at<short>(body,0x164+i*2)=v[i];at<short>(pose,i*2)=v[9+i];}*reinterpret_cast<unsigned char*>(0x6B244A)=1;*reinterpret_cast<unsigned short*>(0x6B2478)=1;short angles[4]={};reinterpret_cast<int(*)(void*,int,short*,short*)>(0x454040)(body,0,angles,nullptr);std::cout<<'['<<angles[0]<<','<<angles[1]<<','<<angles[2]<<"]\n";}}
'''
    identity = [4096, 0, 0, 0, 4096, 0, 0, 0, 4096]
    cases = [[*identity, *identity], [*[0] * 9, *identity], [*identity, *[0] * 9]]
    for value in [-32768, -4096, -1, 0, 1, 4096, 32767]:
        for index in range(9):
            actor = identity.copy(); actor[index] = value; cases.append([*actor, *identity])
    rng = random.Random(0x454040)
    cases += [[rng.randint(-32768, 32767) for _ in range(18)] for _ in range(400)]
    with tempfile.TemporaryDirectory(prefix='openspidey-additional-angles-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=rows).decode().splitlines()
    result = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-additional-part-angles.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original M3dUtils part-angle cases with no matrix, square-root, trig or angle-extraction substitute.')


if __name__ == '__main__':
    main()
