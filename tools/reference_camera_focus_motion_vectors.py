"""Capture the focus phase of untouched original PC camera AI 417CB0 in front mode 7."""
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
#include <cmath>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
long long v[16];while(std::cin>>v[0]){for(int i=1;i<16;i++)std::cin>>v[i];
 alignas(4)unsigned char camera[0x300]={},target[0x100]={};
 at<unsigned>(camera,0x2A0)=7;at<unsigned char>(camera,0xF8)=1;mem<int>(0x5FAE98)=0;
 for(int i=0;i<4;i++){at<int>(camera,0x1E4+4*i)=i==3?4096:0;at<int>(camera,0x1F4+4*i)=i==3?4096:0;}
 at<int>(camera,0x168)=v[0];at<int>(camera,0x80)=v[1];at<void*>(camera,0x13C)=v[2]?target:nullptr;at<unsigned char>(camera,0x140)=v[3];
 for(int i=0;i<3;i++){at<int>(camera,0x144+4*i)=v[4+i];at<int>(camera,0x150+4*i)=v[7+i];at<int>(camera,0x15C+4*i)=v[10+i];at<int>(target,8+4*i)=v[13+i];}
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x417CB0)(camera);
 std::cout<<'['<<at<int>(camera,0x168);for(int i=0;i<3;i++)std::cout<<','<<at<int>(camera,0x144+4*i);std::cout<<"]\n";
}}
'''
    rng = random.Random(0x418068)
    cases = []
    for remaining in [0, 1, 2, 3, 100, -1, -5]:
        for elapsed in [0, 1, 2, 3, -1]:
            for has_target in [0, 1]:
                for tracking in [0, 1, 255]:
                    cases.append([remaining, elapsed, has_target, tracking, 1000, -2000, 3000, 7000, 8000, -9000,
                                  11, -22, 33, -40000, 50000, 60000])
    for _ in range(500):
        cases.append([rng.choice([0, 0, 1, rng.randint(-2 ** 31, 2 ** 31 - 1), rng.randint(0, 200)]),
                      rng.choice([1, 2, 3, 4, rng.randint(-10, 300)]), rng.randrange(2), rng.choice([0, 1, 255]),
                      *[rng.randint(-2 ** 31, 2 ** 31 - 1) for _ in range(6)], *[rng.randint(-2 ** 20, 2 ** 20) for _ in range(3)],
                      *[rng.randint(-2 ** 31, 2 ** 31 - 1) for _ in range(3)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-original-camera-focus-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in cases) + '\n')
        if completed.returncode:
            done = len(completed.stdout.splitlines())
            raise RuntimeError(f'Original camera focus fixture failed{completed.returncode} at case{done}: {completed.stderr}')
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    result = [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, output, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-focus-motion.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC camera AI focus phases: unsigned countdown, timed motion end, rate stepping and live target tracking.')


if __name__ == '__main__': main()
