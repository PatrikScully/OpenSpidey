"""Run the original PC keyboard action-map functions 50A400/50A4A0/50A520 and DXINPUT_PollKeyboard 501B80 with PCINPUT_IsKeyPressed 50A650."""
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
#include <cstring>
struct Record{unsigned ofs,data,time,sequence,app;};
static_assert(sizeof(Record)==0x14);
static Record pending[16];static unsigned pendingCount;
extern "C" __attribute__((stdcall)) long getDeviceData(void*,unsigned size,Record*out,unsigned*count,unsigned flags){
 if(size!=0x14||*count!=16||flags!=0)std::abort();std::memcpy(out,pending,pendingCount*sizeof(Record));*count=pendingCount;return 0;}
static void*vtable[16];static void*device[1]={vtable};
template<class T>T&global(unsigned a){return *reinterpret_cast<T*>(a);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 auto get=reinterpret_cast<void(*)(unsigned,unsigned*)>(0x50A400);auto set=reinterpret_cast<void(*)(unsigned,unsigned)>(0x50A4A0);
 auto restore=reinterpret_cast<void(*)()>(0x50A520);auto poll=reinterpret_cast<int(*)()>(0x501B80);auto pressed=reinterpret_cast<int(*)(unsigned char,int)>(0x50A650);
 vtable[10]=reinterpret_cast<void*>(&getDeviceData);global<void*>(0x6B7A5C)=device;
 auto table=[&]{std::cout<<'[';for(int i=0;i<28;i++)std::cout<<(i?",":"")<<global<unsigned>(0x568398+i*4);std::cout<<']';};
 std::string kind;
 while(std::cin>>kind){
  if(kind=="table"){table();std::cout<<'\n';}
  else if(kind=="get"){unsigned a,k=0x5A5A5A5A;std::cin>>a;get(a,&k);std::cout<<k<<'\n';}
  else if(kind=="set"){unsigned a,k;std::cin>>a>>k;set(a,k);table();std::cout<<'\n';}
  else if(kind=="restore"){restore();table();std::cout<<'\n';}
  else if(kind=="clear"){std::memset(&global<unsigned char>(0x6B792C),0,256);std::cout<<"0\n";}
  else if(kind=="poll"){std::cin>>pendingCount;for(unsigned i=0;i<pendingCount;i++){pending[i]={};std::cin>>pending[i].ofs>>pending[i].data;}
   int result=poll();std::cout<<'['<<result<<",[";for(int i=0;i<256;i++)std::cout<<(i?",":"")<<(int)global<unsigned char>(0x6B792C+i);
   std::cout<<"],[";for(int i=0;i<256;i++)std::cout<<(i?",":"")<<(pressed(i,0)!=0?1:0)+(pressed(i,1)!=0?2:0);std::cout<<"]]\n";}
  else std::abort();
 }
}
'''
    rng=random.Random(0x50A400)
    actions=[1,2,4,8,0x10,0x20,0x40,0x80,0x100,0x200,0x400,0x800,0x1000,0x2000,0x4000,0x8000,0,3,0x10000]
    ops=[['table']]
    for index in range(600):
        choice=rng.randrange(10)
        if choice<5:ops.append(['get',rng.choice(actions)])
        elif choice<9:ops.append(['set',rng.choice(actions),rng.choice([0,0x4000,rng.randrange(256),rng.getrandbits(32)])])
        else:ops.append(['restore'])
    ops+=[['restore']]+[['get',action] for action in actions]+[['clear']]
    keys=[0,1,0x1C,0x47,0x4B,0x48,0xC8,0xCB,0xFF]
    for index in range(800):
        if index%97==0:ops.append(['clear'])
        count=rng.choice([0,0,1,1,2,3,rng.randint(0,16),16])
        records=[]
        for _ in range(count):
            key=rng.choice(keys) if rng.random()<.8 else rng.randrange(256)
            data=rng.choice([0x80,0,0xFF,0x7F,0x180,rng.getrandbits(32)])
            records+=[key,data]
        ops.append(['poll',count]+records)
    with tempfile.TemporaryDirectory(prefix='openspidey-pc-keyboard-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,op)) for op in ops)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/pc-keyboard.json').write_text(json.dumps([dict(input=op,expected=json.loads(line)) for op,line in zip(ops,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(ops)} original PC keyboard mapping and poll operations.')


if __name__=='__main__':main()
