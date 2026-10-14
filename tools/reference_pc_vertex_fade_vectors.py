"""Run the original PC DCModel 4774CD/477F3D and DC_PSXModel 478FE0 vertex-fade fragments over D3D ARGB colours."""
import argparse
import json
from pathlib import Path
import random
import struct
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <cstring>
extern "C" {unsigned savedEsp,entryAddr,entryEax,entryEdx;}
alignas(16) static unsigned char stackArea[0x20000];
struct Vertex{unsigned char pad[0xC];float rhw;unsigned char pad2[8];unsigned colour;unsigned pad3;};
static_assert(sizeof(Vertex)==0x20);
Vertex vertices[16];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
extern "C" __attribute__((naked)) void fragmentEnd(){asm volatile("mov savedEsp,%esp;pop %ebp;pop %edi;pop %esi;pop %ebx;ret");}
extern "C" __attribute__((naked)) void fragmentRun(void*){asm volatile("push %ebx;push %esi;push %edi;push %ebp;mov 20(%esp),%ecx;mov %esp,savedEsp;mov %ecx,%esp;mov entryEax,%eax;mov entryEdx,%edx;xor %ebp,%ebp;jmp *entryAddr");}
template<class T>T&global(unsigned a){return *reinterpret_cast<T*>(a);}
unsigned char*run(unsigned entry,unsigned eax,unsigned edx){unsigned char*frame=stackArea+0x10000;entryAddr=entry;entryEax=eax;entryEdx=edx;fragmentRun(frame);return frame;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x477526,(void*)&fragmentEnd);patch(0x478018,(void*)&fragmentEnd);patch(0x47907F,(void*)&fragmentEnd);
 unsigned short control=0x27F;asm volatile("fldcw %0"::"m"(control));
 int start,end,superFade,fogEnabled,lowGraphics,count;unsigned superFactor,corners[4];
 while(std::cin>>start>>end>>superFade>>fogEnabled>>lowGraphics>>superFactor>>count){
  unsigned rhw[16],colour[16];for(int i=0;i<count;i++)std::cin>>rhw[i]>>colour[i];for(unsigned&c:corners)std::cin>>c;
  volatile float nearValue=1.0f/(float)end;volatile float farValue=1.0f/(float)start;volatile float scale=255.0f/(farValue-nearValue);
  global<float>(0x550078)=nearValue;global<float>(0x55007C)=farValue;global<float>(0x550080)=scale;
  global<unsigned char>(0x660FE1)=superFade;global<int>(0x54D384)=fogEnabled;global<int>(0x6B78F8)=lowGraphics;global<unsigned>(0x550060)=superFactor;global<int>(0x65DFAC)=0;
  global<Vertex*>(0x55002C)=vertices;
  auto load=[&]{std::memset(vertices,0,sizeof vertices);for(int i=0;i<count;i++){std::memcpy(&vertices[i].rhw,&rhw[i],4);vertices[i].colour=colour[i];}};
  load();unsigned char*frame=run(0x4774CD,0,count-1);int modelFaded=*reinterpret_cast<int*>(frame+0x20);
  *reinterpret_cast<int*>(frame+0x58)=count;run(0x477F3D,0,0);
  unsigned bits[3];std::memcpy(&bits[0],(const void*)&nearValue,4);std::memcpy(&bits[1],(const void*)&farValue,4);std::memcpy(&bits[2],(const void*)&scale,4);
  std::cout<<'['<<bits[0]<<','<<bits[1]<<','<<bits[2]<<','<<modelFaded<<",[";for(int i=0;i<count;i++)std::cout<<(i?",":"")<<vertices[i].colour;std::cout<<"],";
  load();frame=stackArea+0x10000;for(int i=0;i<4;i++)frame[0x18+i]=corners[i];*reinterpret_cast<int*>(frame+0x34)=0x5A5A5A5A;
  run(0x478FE0,fogEnabled,0);std::cout<<*reinterpret_cast<int*>(frame+0x34)<<",[";for(int i=0;i<count;i++)std::cout<<(i?",":"")<<vertices[i].colour;std::cout<<"]]\n";
 }
}
'''
    rng=random.Random(0x4774CD)
    bits=lambda value:struct.unpack('<I',struct.pack('<f',value))[0]
    def colour():return rng.choice([0xFF000000,0x80000000,0xFFFFFFFF,0x80FFFFFF,0,rng.getrandbits(32),(rng.choice([0xFF,0x80])<<24)|rng.getrandbits(24)])
    cases=[]
    for index in range(1200):
        start=rng.choice([10,100,1000,6000,8000,rng.randint(10,30000)]);end=start+rng.choice([5,6,100,2048,4096,rng.randint(5,20000)])
        count=rng.randint(1,8);values=[]
        for _ in range(count):
            kind=rng.randrange(10)
            if kind==0:value=bits(1.0/start)
            elif kind==1:value=bits(1.0/end)
            elif kind==2:value=rng.choice([0,0x80000000,0x7FC00000,0x7F800000,bits(-1.0/start)])
            elif kind==3:value=bits(1.0/rng.uniform(1,start))
            else:value=bits(1.0/rng.uniform(start*0.9,end*1.6))
            values.append((value,colour()))
        superFade=int(index%11==0);fog=int(index%7!=0);low=int(index%13==0)
        corners=[rng.randrange(count) for _ in range(4)]
        cases.append([start,end,superFade,fog,low,rng.choice([0,1,127,128,255,256,rng.getrandbits(32)]),count]+[v for pair in values for v in pair]+corners)
    with tempfile.TemporaryDirectory(prefix='openspidey-pc-vertex-fade-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/pc-vertex-fade.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original PC DCModel model and DC_PSXModel face vertex-fade cases.')


if __name__=='__main__':main()
