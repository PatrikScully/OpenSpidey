"""Capture untouched original PC CM_Normal and SetStartPosition with retained projection globals."""
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
void print(void*p,unsigned offset,int count){for(int i=0;i<count;i++)std::cout<<','<<at<int>(p,offset+4*i);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
*reinterpret_cast<unsigned char*>(0x46CD51)=0xC3;
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
int family,mode,pitch,yaw,roll,look,distance,x,y,z;while(std::cin>>family>>mode>>pitch>>yaw>>roll>>look>>distance>>x>>y>>z){
 alignas(4)unsigned char camera[0x300]={},tripod[0x100]={};
 at<void*>(camera,0xFC)=tripod;at<int>(camera,0x2A0)=mode;at<short>(camera,0x234)=pitch;at<short>(camera,0x236)=yaw;at<short>(camera,0x238)=roll;
 for(unsigned offset:{0x1E4u,0x1F4u})at<int>(camera,offset+12)=4096;
 at<int>(tripod,8)=x;at<int>(tripod,12)=y;at<int>(tripod,16)=z;
 at<int>(camera,0x104)=13;at<int>(camera,0x108)=29;at<int>(camera,0x10C)=43;
 at<int>(camera,8)=91;at<int>(camera,12)=97;at<int>(camera,16)=101;
 at<int>(camera,0x24C)=-13;at<int>(camera,0x250)=-29;at<int>(camera,0x254)=-43;
 *reinterpret_cast<int*>(0x548858)=look;*reinterpret_cast<int*>(0x54885C)=distance;
 if(family==0)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416AA0)(camera);
 else reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x418E00)(camera);
 std::cout<<'['<<at<short>(camera,0x236);print(camera,0x104,3);print(camera,0x24C,3);print(camera,0x1F4,4);
 print(camera,8,3);print(camera,0x1E4,4);std::cout<<','<<*reinterpret_cast<int*>(0x548858)<<','<<*reinterpret_cast<int*>(0x54885C)<<"]\n";
}}
'''
    rng = random.Random(0x416AA0)
    cases = [[family, mode, pitch, yaw, roll, look, distance, -19268 << 12, 4851 << 12, -40748 << 12]
             for family in [0, 1] for mode in [3, 4, 5, 7] for pitch, yaw, roll in [(0, 0, 0), (0, 3243, 0), (97, 1357, -257)]
             for look, distance in [(300, 200), (45, 290), (-2701, -100)]]
    cases += [[rng.randrange(2), rng.choice([3, 4, 5]), *[rng.randint(-32768, 32767) for _ in range(3)],
               rng.randint(-2147483648, 2147483647), rng.randint(-10000, 10000),
               *[rng.randint(-2147483648, 2147483647) for _ in range(3)]] for _ in range(350)]
    with tempfile.TemporaryDirectory(prefix='openspidey-original-camera-start-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in cases) + '\n')
        if completed.returncode: raise RuntimeError(f'Original camera start fixture failed{completed.returncode}: {completed.stderr}')
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    result = [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, output, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-start-position.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC CM_Normal/SetStartPosition cases with unchanged retained projection globals.')


if __name__ == '__main__': main()
