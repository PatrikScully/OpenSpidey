"""Capture original PC SLight matrix products, plain-normal conversion and actual reverse-light/stitch-colour loop."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP=r'''
#include "original_executable.h"
#include <iostream>
#include <cstring>
struct Matrix{short m[9],pad;int t[3];};struct Vertex{float x,y,z;unsigned flags;};
short raw[4];float normalized[3],normals[3][3];Vertex vertices[3];unsigned vertexCount=3;
void __attribute__((naked)) normalLeaf(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x1a0,%esp;movl $raw+2,%edi;movl $normalized+8,%ebp;movl $0x43224f,%eax;call *%eax;addl $0x1a0,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
void __attribute__((naked)) lightLeaf(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x80,%esp;movl $normals,%esi;movl $vertices,%eax;movl %eax,0x44(%esp);movl vertexCount,%eax;decl %eax;movl %eax,0x24(%esp);movl $3,%ebx;xorl %ebp,%ebp;movl $0x477671,%eax;call *%eax;addl $0x80,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
unsigned bits(float x){unsigned value;std::memcpy(&value,&x,4);return value;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
*(unsigned char*)0x432363=0xc3;*(unsigned char*)0x4777b0=0xc3;
Matrix actor{},part{},world{},light{};int v;
while(std::cin>>v){actor.m[0]=v;for(int i=1;i<9;i++){std::cin>>v;actor.m[i]=v;}for(int i=0;i<9;i++){std::cin>>v;part.m[i]=v;}
reinterpret_cast<void(*)(Matrix*,Matrix*,Matrix*)>(0x46cd90)(&actor,&part,&world);
reinterpret_cast<void(*)(Matrix*,Matrix*,Matrix*)>(0x46cd90)((Matrix*)0x548728,&world,&light);
for(int n=0;n<3;n++){for(int axis=0;axis<3;axis++){std::cin>>v;raw[axis]=v;}normalLeaf();std::memcpy(normals[n],normalized,12);}
int first,second,attachment;std::cin>>first>>second>>attachment;vertices[0].flags=first;vertices[1].flags=second;vertices[2].flags=2|(attachment<<16);
*(unsigned*)0x65dfa8=0x64f5d8;*(unsigned*)0x64e510=3;
for(int row=0;row<3;row++)for(int col=0;col<3;col++){
 ((float*)0x628668)[row*3+col]=light.m[row*3+col]/4096.f;
 ((float*)0x64f5a8)[row*3+col]=((short*)0x54873c)[col*3+row]/4096.f;
}
for(int axis=0;axis<3;axis++)((float*)0x660f50)[axis]=((int*)0x548750)[axis]/4096.f;
lightLeaf();std::cout<<"[[";for(int i=0;i<9;i++){if(i)std::cout<<',';std::cout<<light.m[i];}std::cout<<"],[";
for(int n=0;n<3;n++){if(n)std::cout<<',';std::cout<<'[';for(int axis=0;axis<3;axis++){if(axis)std::cout<<',';std::cout<<bits(normals[n][axis]);}std::cout<<']';}std::cout<<"],[";
for(int n=0;n<3;n++){if(n)std::cout<<',';std::cout<<'[';for(int axis=0;axis<3;axis++){if(axis)std::cout<<',';std::cout<<bits(((float*)0x65dfb0)[n*3+axis]);}std::cout<<']';}
std::cout<<"],"<<((*(unsigned*)0x65dfa8-0x64f5d8)/12)<<"]\n";}
}
'''


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;rng=random.Random(0x477690)
    identity=[4096,0,0,0,4096,0,0,0,4096]
    cases=[[*identity,*identity,0,0,0,4096,0,0,0,4096,0,1,1,0]]
    for _ in range(400):cases.append([*[rng.randint(-4096,4096)for _ in range(18)],*[rng.randint(-32768,32767)for _ in range(9)],rng.randrange(2),rng.randrange(2),rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-vertex-lighting-') as folder:
        cpp,binary=Path(folder)/'reference.cpp',Path(folder)/'reference';cpp.write_text(CPP)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,row))for row in cases)+'\n').encode())
    fixtures=[dict(input=row,expected=json.loads(line))for row,line in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/vertex-lighting.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC MulMatrix0/plain-normal/SLight/reverse-light/stitch-colour cases; original instruction ranges kept, unrelated rendering omitted.')

if __name__=='__main__':main()
