"""Capture untouched original PC 416B10 for camera modes 16/17 without item hiding."""
import argparse
import json
import math
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
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
int diagnostics;
void diagnostic(int condition,const char*message){if((condition&255)==0){assert(!strcmp(message,"No valid tripod"));diagnostics++;}}
void print(void*p,unsigned offset,int count){for(int i=0;i<count;i++)std::cout<<','<<at<int>(p,offset+4*i);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4015B0,(void*)&diagnostic);
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
long long v[26];while(std::cin>>v[0]){for(int i=1;i<26;i++)std::cin>>v[i];
 alignas(4)unsigned char camera[0x300]={},tripod[0x100]={},player[0x1000]={};diagnostics=0;
 at<unsigned>(camera,0x2A0)=v[0];at<void*>(camera,0xFC)=v[1]?tripod:nullptr;at<unsigned char>(camera,0x100)=v[2];mem<int>(0x5FAE98)=v[3];
 at<int>(camera,0x104)=v[4];at<int>(camera,0x108)=v[5];at<int>(camera,0x10C)=v[6];
 mem<int>(0x56F260)=v[7];mem<int>(0x56F264)=v[8];mem<int>(0x56F268)=v[9];
 at<int>(camera,8)=v[10];at<int>(camera,12)=v[11];at<int>(camera,16)=v[12];
 for(int i=0;i<4;i++){at<int>(camera,0x1E4+4*i)=v[13+i];at<int>(camera,0x1F4+4*i)=v[17+i];}
 at<int>(camera,0x1D0)=v[21];mem<int>(0x54885C)=v[22];at<unsigned char>(player,5)=v[23];
 at<unsigned char>(camera,0x180)=255;at<unsigned char>(camera,0x1A4)=255;at<int>(camera,0x1D4)=v[24];at<unsigned char>(camera,0xF9)=0;
 for(int i=0;i<3;i++)at<int>(camera,0x60+4*i)=v[25]+i;
 mem<void*>(0x6A9038)=player;
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416B10)(camera);
 std::cout<<'['<<diagnostics;print(camera,8,3);print(camera,0x60,3);std::cout<<','<<at<int>(camera,0x1D0)<<','<<at<int>(camera,0x1D4);
 print(camera,0x1E4,4);print(camera,0x204,4);print(camera,0x214,4);
 std::cout<<','<<unsigned(at<unsigned char>(camera,0x180))<<','<<unsigned(at<unsigned char>(camera,0x1A4))<<','<<unsigned(at<unsigned char>(player,5))<<"]\n";
}}
'''
    rng = random.Random(0x416B10)

    def quaternion():
        values = [rng.gauss(0, 1) for _ in range(4)]
        length = math.sqrt(sum(value * value for value in values))
        return [round(value / length * 4096) for value in values]

    focus = [-19268 << 12, 4851 << 12, -40748 << 12]
    identity = [0, 0, 0, 4096]
    cases = []
    for mode in [16, 17]:
        for tripod in [0, 1]:
            for tracking in [0, 1]:
                for water in [0, 1]:
                    for smoothed, distance in [(0, 200), (200, 200), (1000, 200), (199, 150), (300, 1), (-50, 4000)]:
                        cases.append([mode, tripod, tracking, water, *focus, 11 << 12, 22 << 12, 33 << 12, *[f + 5000 for f in focus],
                                      *identity, *identity, smoothed, distance, 0x40, 77, 900])
    for _ in range(800):
        current = quaternion()
        desired = rng.choice([current, quaternion(), [-value for value in current]])
        cases.append([rng.choice([16, 17]), rng.randrange(2), rng.choice([0, 1, 255]), rng.choice([0, 0, 0, 1]),
                      *[rng.randint(-2 ** 28, 2 ** 28) for _ in range(3)], *[rng.randint(-2 ** 20, 2 ** 20) for _ in range(3)],
                      *[rng.randint(-2 ** 28, 2 ** 28) for _ in range(3)], *current, *desired,
                      rng.randint(-3000, 6000), rng.randint(-2000, 8000), rng.randrange(256), rng.randint(-5000, 5000),
                      rng.randint(-2 ** 28, 2 ** 28)])
    with tempfile.TemporaryDirectory(prefix='openspidey-original-camera-user-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in cases) + '\n')
        if completed.returncode:
            done = len(completed.stdout.splitlines())
            raise RuntimeError(f'Original camera user-placement fixture failed{completed.returncode} at case{done} {cases[done]}: {completed.stderr}')
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    result = [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, output, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-user-placement.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC mode16/17 placements: tripod diagnostic, post-water return, slerp, smoothed distance, player hiding and velocity.')


if __name__ == '__main__': main()
