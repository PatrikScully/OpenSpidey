"""Capture actual PC cinematic MGS footprint math, signed ground placement and existing protected quad stores."""
import argparse
import json
import math
import random
import subprocess
import tempfile
from pathlib import Path

CPP=r'''
#include "original_executable.h"
#include <iostream>
struct Vec{int x,y,z;};struct Hook{short x,y,z;unsigned short part;};
Vec hooks[4];int count;
void hook(Vec*out,void*,Hook*h){int index=h->part==3?0:h->part==6?1:h->part==13?2:h->part==9?3:-1;assert(index>=0);*out=hooks[index];count++;}
void diagnostic(int,const char*){}
void patch(unsigned a,void*t){auto*p=(unsigned char*)a;p[0]=0xe9;*(unsigned*)(p+1)=(unsigned)t-a-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
// Disable Windows SEH bookkeeping only; all original footprint/GTE/quad stores remain.
std::memset((void*)0x414c50,0x90,6);*(unsigned short*)0x414c50=0xc031;std::memset((void*)0x414c5e,0x90,7);std::memset((void*)0x414f8c,0x90,7);
patch(0x453ee0,(void*)&hook);patch(0x4015b0,(void*)&diagnostic);
int ground;while(std::cin>>ground){alignas(4) unsigned char b[0x400]={},quad[0x100]={};int x;
*(short*)(b+0x222)=ground;*(short*)(b+0x21e)=100;*(void**)(b+0x33c)=quad;*(short*)(quad+0x38)=99;
for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+8+axis*4)=x;}
for(int i=0;i<9;i++){std::cin>>x;*(short*)(b+0x164+i*2)=x;}
for(auto&h:hooks)std::cin>>h.x>>h.y>>h.z;count=0;
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x414c50)(b);
std::cout<<'['<<count<<','<<*(short*)(quad+0x38)<<','<<*(unsigned*)(quad+0x64)<<','<<*(unsigned*)(quad+0x6c)<<",[";
int n=0;for(int offset:{0x10,0x3c,0x48,0x54}){if(n++)std::cout<<',';auto v=*(Vec*)(quad+offset);std::cout<<'['<<v.x<<','<<v.y<<','<<v.z<<']';}std::cout<<"]]\n";}
}
'''


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;rng=random.Random(0x414C50);cases=[]
    for angle in [0,512,1024,2048,4095]:
        a=angle*.001536096911877394;s=int(math.sin(a)*4096);c=int(math.cos(a)*4096)
        for limit in [0,32,1024,524288]:
            position=[12345,-67890,34567]
            matrix=[c,0,s,0,4096,0,-s,0,c]
            hooks=[[position[0]+rng.randint(-limit,limit)*4096,position[1]+rng.randint(-limit,limit)*4096,
                    position[2]+rng.randint(-limit,limit)*4096]for _ in range(4)]
            cases.append([rng.choice([-32768,-1,0,160,32767]),*position,*matrix,*[v for h in hooks for v in h]])
    for _ in range(180):
        position=[rng.randint(-2147483647,2147483647)for _ in range(3)]
        matrix=[rng.randint(-4096,4096)for _ in range(9)]
        hooks=[rng.randint(-2147483647,2147483647)for _ in range(12)]
        cases.append([rng.randint(-32768,32767),*position,*matrix,*hooks])
    with tempfile.TemporaryDirectory(prefix='openspidey-cinematic-shadow-') as folder:
        cpp,binary=Path(folder)/'reference.cpp',Path(folder)/'reference';cpp.write_text(CPP)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,row))for row in cases)+'\n').encode())
    fixtures=[dict(input=row,expected=json.loads(line))for row,line in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/cinematic-shadows.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} untouched originalPC414C50 footprint cases including GTE transforms, transparency and corners; only dynamic hooks/SEH leaves adapted.')

if __name__=='__main__':main()
