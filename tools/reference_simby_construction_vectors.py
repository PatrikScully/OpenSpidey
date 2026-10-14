"""Capture the original CSimby constructor (0x4A7870), Flash (0x4AF260) and FlashUpdate (0x4AF3B0)."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cstring>
#include <iostream>
#include <vector>
std::vector<int> events;int level=0;
void event(std::initializer_list<int>x){events.insert(events.end(),x);}
void patch(unsigned address,void*t){auto*c=(unsigned char*)address;c[0]=0xe9;*(unsigned*)(c+1)=(unsigned)t-address-5;}
void* __attribute__((thiscall)) baddy(void*p){event({1});return p;}
void __attribute__((thiscall)) initItem(void*,const char*name){event({2,!std::strcmp(name,"sym_dark")?1:!std::strcmp(name,"symbi_02")?0:-1});}
void __attribute__((thiscall)) attach(void*,void*list){event({3,int((unsigned)list)});}
void* __attribute__((thiscall)) squirtPos(void*p,short*data){
 auto*b=(unsigned char*)p;int*v=(int*)data;*(int*)(b+8)=v[0]<<12;*(int*)(b+12)=v[1]<<12;*(int*)(b+16)=v[2]<<12;event({4});return (char*)data+12;}
void* __attribute__((thiscall)) squirtAngles(void*p,short*data){
 auto*b=(unsigned char*)p;*(short*)(b+0x14)=data[0];*(short*)(b+0x16)=data[1];*(short*)(b+0x18)=data[2];event({5});return data+3;}
int getLevel(){return level;}
int rnd(int bound){event({6,bound});return bound?bound/3:0;}
void readLinks(void*,void*packet){event({7,int((unsigned)packet)});}
void __attribute__((thiscall)) parseScript(void*,void*){event({8});}
void wibbler(){event({9});}
void diagnostic(int,const char*){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x402c00,(void*)&baddy);patch(0x460020,(void*)&initItem);patch(0x460260,(void*)&attach);
patch(0x460330,(void*)&squirtPos);patch(0x4603a0,(void*)&squirtAngles);patch(0x4de770,(void*)&getLevel);
patch(0x4e5da0,(void*)&rnd);patch(0x453c50,(void*)&readLinks);patch(0x404fd0,(void*)&parseScript);
patch(0x4a2790,(void*)&wibbler);patch(0x4015b0,(void*)&diagnostic);
for(auto*c=(unsigned char*)0x4a7870;c<(unsigned char*)0x4a7ae0;c++){
 if(c[0]==0x64&&c[1]==0xa1&&!*(unsigned*)(c+2)){c[0]=0x31;c[1]=0xc0;std::memset(c+2,0x90,4);}
 if(c[0]==0x64&&c[1]==0x89&&!*(unsigned*)(c+3))std::memset(c,0x90,7);}
char mode;while(std::cin>>mode){
 if(mode=='C'){int node,attack,count,reset;std::cin>>level>>node>>attack>>count>>reset;
  *(int*)0x5fccf4=attack;*(int*)0x682c5c=count;*(int*)0x682c64=reset;events.clear();
  alignas(4) unsigned char b[0x500]={};alignas(4) unsigned char data[20]={};int x;
  for(int i=0;i<3;i++){std::cin>>x;*(int*)(data+i*4)=x;}for(int i=0;i<3;i++){std::cin>>x;*(short*)(data+12+i*2)=x;}
  reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*,int)>(0x4a7870)(b,data,node);
  std::cout<<'['<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x46)<<','<<*(unsigned short*)(b+0x38)<<','<<*(short*)(b+0xe2)<<','
   <<*(unsigned short*)(b+0xdc)<<','<<*(short*)(b+0x21e)<<','<<*(int*)(b+0xd8)<<','<<*(short*)(b+0x216)<<','<<*(int*)(b+0x2a8)<<','
   <<*(int*)(b+0x230)<<','<<*(int*)(b+0x31c)<<','<<*(int*)(b+0x294)<<','<<*(int*)(b+0x298)<<','<<*(int*)(b+0x3ec)<<','
   <<*(int*)(b+0x34c)<<','<<*(int*)(b+0x3b8)<<','<<*(int*)(b+0x3bc)<<','<<*(int*)(b+0x3c0)<<','<<*(int*)(b+0x30c)<<','
   <<*(int*)(b+0x3a0)<<','<<*(int*)(b+0x218)<<','<<*(int*)(b+0x344)<<','<<*(unsigned short*)(b+0xde)<<','
   <<*(int*)0x682c5c<<','<<*(int*)0x682c64<<','<<*(int*)(b+8)<<','<<*(int*)(b+12)<<','<<*(int*)(b+16)<<','
   <<*(short*)(b+0x14)<<','<<*(short*)(b+0x16)<<','<<*(short*)(b+0x18)<<",[";
  for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}
 else if(mode=='F'){int frames,r,g,bb,item,rgb,restore,ff,dr,dg,db;std::cin>>frames>>r>>g>>bb>>item>>rgb>>restore>>ff>>dr>>dg>>db;
  alignas(4) unsigned char body[0x500]={};*(unsigned short*)(body+4)=item;*(int*)(body+0x24)=rgb;*(int*)(body+0x330)=restore;
  *(short*)(body+0x328)=ff;*(unsigned short*)(body+0x32a)=dr;*(unsigned short*)(body+0x32c)=dg;*(short*)(body+0x32e)=db;
  reinterpret_cast<void(__attribute__((thiscall))*)(void*,int,unsigned char,unsigned char,unsigned char)>(0x4af260)(body,frames,r,g,bb);
  std::cout<<'['<<*(unsigned short*)(body+4)<<','<<*(int*)(body+0x24)<<','<<*(short*)(body+0x328)<<','<<*(unsigned short*)(body+0x32a)<<','
   <<*(unsigned short*)(body+0x32c)<<','<<*(short*)(body+0x32e)<<','<<*(int*)(body+0x330)<<"]\n";}
 else{int item,rgb,restore,ff,dr,dg,db;std::cin>>item>>rgb>>restore>>ff>>dr>>dg>>db;
  alignas(4) unsigned char body[0x500]={};*(unsigned short*)(body+4)=item;*(int*)(body+0x24)=rgb;*(int*)(body+0x330)=restore;
  *(short*)(body+0x328)=ff;*(unsigned short*)(body+0x32a)=dr;*(unsigned short*)(body+0x32c)=dg;*(short*)(body+0x32e)=db;
  reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4af3b0)(body);
  std::cout<<'['<<*(unsigned short*)(body+4)<<','<<*(int*)(body+0x24)<<','<<*(short*)(body+0x328)<<','<<*(unsigned short*)(body+0x32a)<<','
   <<*(unsigned short*)(body+0x32c)<<','<<*(short*)(body+0x32e)<<','<<*(int*)(body+0x330)<<"]\n";}}
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x4A7870)
    cases = []
    for level in [0x701, 0x702, 0x803, 0x201]:
        for attack in [0, 59, 60, 155, 1000]:
            for count in [0, 1, 3]:
                for node in [12, 0xFFFF]:
                    cases.append(dict(mode='C', input=[level, node, attack, count, 1 if count else 0, 10, -20, 30, 100, -50, 2000]))
    for _ in range(80):
        cases.append(dict(mode='C', input=[rng.choice([0x701, 0x702, 0x803, 0x804]), rng.choice([0, 12, 0xFFFF]),
                                           rng.randrange(0, 500), rng.randrange(0, 5), rng.randrange(2),
                                           *[rng.randint(-30000, 30000) for _ in range(3)],
                                           *[rng.randint(-32768, 32767) for _ in range(3)]]))
    for frames in [0, -1, -8, 1, 4, 16, 32]:
        for r, g, b in [(0, 0, 0), (32, 32, 32), (255, 0, 0), (10, 20, 30), (255, 255, 255)]:
            for item, rgb, restore in [(0, 0x202020, 0), (0x400, 0x112233, 0), (0x482, 0xAABBCC, 0x112233 | 0x1000000)]:
                cases.append(dict(mode='F', input=[frames, r, g, b, item, rgb, restore, 0, 0, 0, 0]))
    for _ in range(120):
        cases.append(dict(mode='F', input=[rng.choice([0, -1, -7, 1, 5, 20, 100]), rng.randrange(256), rng.randrange(256),
                                           rng.randrange(256), rng.choice([0, 0x400, 0x482, 0x882]),
                                           rng.randrange(1 << 24), rng.choice([0, 0x1000000, 0x3000000 | rng.randrange(1 << 24)]),
                                           rng.randrange(40), rng.randrange(256), rng.randrange(256), rng.randint(-128, 127)]))
    for _ in range(160):
        cases.append(dict(mode='U', input=[rng.choice([0, 0x400, 0x482]), rng.randrange(1 << 24),
                                           rng.choice([0, 0x1000000, 0x3000000 | 0x55]),
                                           rng.randrange(0, 8), rng.randrange(256), rng.randrange(256), rng.randint(-128, 127)]))
    rows = [case['mode'] + ' ' + ' '.join(map(str, case['input'])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-simby-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/simby-construction.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original CSimby constructor and Flash cases.')


if __name__ == '__main__':
    main()
