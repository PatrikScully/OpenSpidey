"""Capture untouched original PC camera boss mode 4192F0 with its CM_Normal tail and retained globals."""
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
void print(void*p,unsigned offset,int count){for(int i=0;i<count;i++)std::cout<<','<<at<int>(p,offset+4*i);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
*reinterpret_cast<unsigned char*>(0x46CD51)=0xC3;
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
long long v[20];while(std::cin>>v[0]){for(int i=1;i<20;i++)std::cin>>v[i];
 alignas(4)unsigned char camera[0x300]={},tripod[0x100]={},boss[0x100]={};
 at<void*>(camera,0xFC)=v[1]?tripod:nullptr;at<void*>(camera,0x13C)=reinterpret_cast<void*>(0x1234);
 at<short>(camera,0x234)=v[2];at<short>(camera,0x236)=v[3];at<short>(camera,0x238)=v[4];
 at<int>(camera,0x104)=v[5];at<int>(camera,0x108)=v[6];at<int>(camera,0x10C)=v[7];
 at<int>(boss,8)=v[8];at<int>(boss,12)=v[9];at<int>(boss,16)=v[10];
 at<int>(camera,0x2A4)=v[11];at<int>(camera,0x2A8)=v[12];
 mem<int>(0x56F260)=v[13];mem<int>(0x56F264)=v[14];mem<int>(0x56F268)=v[15];
 mem<int>(0x548860)=v[16];mem<int>(0x548864)=v[17];mem<int>(0x548858)=v[18];mem<int>(0x54885C)=v[19];
 for(unsigned offset:{0x1E4u,0x1F4u})at<int>(camera,offset+12)=4096;
 at<int>(camera,0x24C)=-13;at<int>(camera,0x250)=-29;at<int>(camera,0x254)=-43;
 mem<void*>(0x56E998)=v[0]?boss:nullptr;
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4192F0)(camera);
 void*focus=at<void*>(camera,0x13C);
 std::cout<<'['<<(focus==boss?2:focus==tripod?1:focus==nullptr?0:-1)<<','<<at<short>(camera,0x236);
 print(camera,0x104,3);print(camera,0x24C,3);print(camera,0x1F4,4);
 std::cout<<','<<mem<int>(0x56F260)<<','<<mem<int>(0x56F264)<<','<<mem<int>(0x56F268)<<','<<mem<int>(0x548858)<<','<<mem<int>(0x54885C)<<"]\n";
}}
'''
    rng = random.Random(0x4192F0)
    focus = [-19268 << 12, 4851 << 12, -40748 << 12]
    cases = []
    for has_boss in [0, 1]:
        for has_tripod in [0, 1]:
            for heading in [0, 1000, 3243, 4095, -1, 5000]:
                for dx, dz in [(0, 0), (100, 0), (0, -100), (-3000, 2000), (2, 1), (600, 600)]:
                    for spin, radius in [(31, 512), (127, 0), (5, 10000)]:
                        cases.append([has_boss, has_tripod, 0, heading, 0, *focus,
                                      focus[0] + (dx << 12), focus[1] + (40 << 12), focus[2] + (dz << 12),
                                      spin, radius, 11 << 12, 22 << 12, 33 << 12, 528, 160, 300, 200])
    for _ in range(600):
        position = [rng.randint(-2 ** 28, 2 ** 28) for _ in range(3)]
        cases.append([rng.randrange(2), rng.randrange(2), rng.choice([0, 0, rng.randint(-32768, 32767)]),
                      rng.randint(-32768, 32767), rng.choice([0, 0, rng.randint(-32768, 32767)]), *position,
                      *[value + rng.randint(-2 ** 24, 2 ** 24) for value in position],
                      rng.choice([31, 127, 0, -7, rng.randint(-4096, 4096)]), rng.choice([512, 0, rng.randint(-100, 20000)]),
                      *[rng.randint(-2 ** 31, 2 ** 31 - 1) for _ in range(3)], rng.randint(-2000, 2000), rng.randint(-2000, 2000),
                      rng.randint(-5000, 5000), rng.randint(-1000, 1000)])
    with tempfile.TemporaryDirectory(prefix='openspidey-original-camera-boss-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in cases) + '\n')
        if completed.returncode: raise RuntimeError(f'Original camera boss fixture failed{completed.returncode}: {completed.stderr}')
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    result = [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, output, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-boss-mode.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC camera boss-mode cases: focus owner, bounded yaw, offset X/Z clearing, projection globals and CM_Normal tail.')


if __name__ == '__main__': main()
