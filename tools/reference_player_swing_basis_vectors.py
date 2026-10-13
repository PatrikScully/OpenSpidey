"""Capture actual PC CalculateSwingWebParameters with full normal/matrix preservation."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
struct Vector{int x,y,z;};template<class T>T&at(void*p,unsigned o){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+o);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);long long v[9];while(std::cin>>v[0]){for(int i=1;i<9;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={};at<Vector>(player,8)={int(v[0]),int(v[1]),int(v[2])};Vector anchor{int(v[3]),int(v[4]),int(v[5])};at<Vector>(player,0xDA0)={int(v[6]),int(v[7]),int(v[8])};at<unsigned short>(player,0xD92)=0xBEEF;at<Vector>(player,0xD94)={123456789,-987654321,213456789};reinterpret_cast<void(__attribute__((thiscall)) *)(void*,Vector*)>(0x4C2F70)(player,&anchor);std::cout<<'[';for(int i=0;i<9;i++){if(i)std::cout<<',';std::cout<<at<short>(player,0xD80+2*i);}std::cout<<','<<at<unsigned short>(player,0xD92);auto&t=at<Vector>(player,0xD94);std::cout<<','<<t.x<<','<<t.y<<','<<t.z<<','<<at<int>(player,0xDB8)<<"]\n";}}
'''
    rng=random.Random(0x4C2F70)
    cases=[[0,0,0,0,0,0,*normal] for normal in [[0,0,0],[0,-4096,0],[4096,0,0],[0,0,4096]]]
    cases += [[*[rng.randint(-20000000,20000000) for _ in range(6)],*[rng.randint(-4096,4096) for _ in range(3)]] for _ in range(500)]
    cases += [[*[rng.randint(-2147483648,2147483647) for _ in range(9)]] for _ in range(100)]
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-basis-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-swing-basis.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} complete unpatched PC CalculateSwingWebParameters cases.')

if __name__=='__main__':main()
