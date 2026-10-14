"""Capture the original CMJ/CLizard/CSubmariner constructors, AI and shared CBaddy::Die(0)."""
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
std::vector<int> events;unsigned short links[]={2,9,4};
void event(std::initializer_list<int>x){events.insert(events.end(),x);}
void patch(unsigned address,void*t){auto*c=(unsigned char*)address;c[0]=0xe9;*(unsigned*)(c+1)=(unsigned)t-address-5;}
void* __attribute__((thiscall)) baddy(void*p){event({1});return p;}
void __attribute__((thiscall)) initItem(void*,const char*name){event({2,!std::strcmp(name,"mj")?0:!std::strcmp(name,"lizard")?1:!std::strcmp(name,"mariner")?2:-1});}
void __attribute__((thiscall)) cycle(void*p,int id,int direction){*(unsigned short*)((unsigned char*)p+0x12a)=id;event({3,id,direction});}
void __attribute__((thiscall)) attach(void*,void*list){event({4,int((unsigned)list)});}
void __attribute__((thiscall)) cleanup(void*p,int all,int keep){*(void**)((unsigned char*)p+0x290)=nullptr;event({5,all,keep});}
void build(void*){event({6});}
unsigned short*getLinks(int node){event({7,node});return links;}
void pulse(unsigned short*p){for(int i=1;i<=p[0];i++)event({8,p[i]});}
void diagnostic(int,const char*){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x402c00,(void*)&baddy);patch(0x460020,(void*)&initItem);patch(0x460d00,(void*)&cycle);patch(0x460260,(void*)&attach);
patch(0x403a90,(void*)&cleanup);patch(0x475f50,(void*)&build);patch(0x4e3880,(void*)&getLinks);patch(0x4dfd30,(void*)&pulse);patch(0x4015b0,(void*)&diagnostic);
const unsigned constructors[]={0x459330,0x44a290,0x4cba80},ais[]={0x459450,0x44a3a0,0x4cbba0};
// Only the Windows SEH registration reads/writes of fs:0 are removed from the constructors.
for(unsigned start:constructors)for(auto*c=(unsigned char*)start;c<(unsigned char*)start+0x110;c++){
 if(c[0]==0x64&&c[1]==0xa1&&!*(unsigned*)(c+2)){c[0]=0x31;c[1]=0xc0;std::memset(c+2,0x90,4);}
 else if(c[0]==0x64&&c[1]==0x89&&!*(unsigned*)(c+3))std::memset(c,0x90,7);}
char mode;int kind;while(std::cin>>mode>>kind){alignas(4) unsigned char b[0x400]={};int itemFlags,bodyFlags,combat,pulsed,node,abort;
std::cin>>itemFlags>>bodyFlags>>combat>>pulsed>>node>>abort;*(unsigned short*)(b+4)=itemFlags;*(unsigned short*)(b+0x46)=bodyFlags;
*(unsigned*)(b+0x2a8)=combat;b[0x211]=pulsed;*(unsigned short*)(b+0xde)=node;*(unsigned char*)0x60cfc4=abort;events.clear();
if(mode=='C'){alignas(4) int data[6];short*angles=(short*)(data+3);int x;for(int axis=0;axis<3;axis++){std::cin>>data[axis];}
for(int axis=0;axis<3;axis++){std::cin>>x;angles[axis]=x;}int ctorNode;std::cin>>ctorNode;
reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*,int)>(constructors[kind])(b,data,ctorNode);}
else{int anim,input,message;std::cin>>anim>>input>>message;*(unsigned short*)(b+0x12a)=anim;*(unsigned short*)(b+0x44)=input;
*(void**)(b+0x290)=message?(void*)b:nullptr;reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(ais[kind])(b);}
std::cout<<'['<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x46)<<','<<int(b[0x211])<<','<<*(unsigned short*)(b+0x44)<<','<<*(unsigned short*)(b+0x12a)<<','
<<*(unsigned short*)(b+0x38)<<','<<*(unsigned short*)(b+0xde)<<','<<*(unsigned short*)(b+0xdc)<<','<<*(unsigned*)(b+0x3c)<<','
<<*(int*)(b+8)<<','<<*(int*)(b+12)<<','<<*(int*)(b+16)<<','<<*(short*)(b+0x14)<<','<<*(short*)(b+0x16)<<','<<*(short*)(b+0x18)<<','<<(*(void**)(b+0x290)!=nullptr)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x459450)
    cases = []
    for kind in range(3):
        for abort in [0, 1]:
            for body in [0x216, 0x256]:
                for combat, pulsed, node in [(0, 0, 12), (0x4000, 0, 12), (0, 1, 12), (0, 0, 0xFFFF)]:
                    cases.append(['C', kind, 2, body, combat, pulsed, 0xFFFF, abort, 400, -80, 130, 1024, -31, 4095, node])
                    for anim in [0, 1, 5]:
                        for signal in [0, 1, 0x8003]:
                            for message in [0, 1]:
                                cases.append(['A', kind, 0x482, body, combat, pulsed, node, abort, anim, signal, message])
    for _ in range(300):
        kind = rng.randrange(3)
        common = [kind, rng.randrange(65536), rng.choice([0x216, 0x256, rng.randrange(65536)]),
                  rng.choice([0, 0x4000, rng.randrange(1 << 31)]), rng.randrange(2), rng.choice([0xFFFF, rng.randrange(65536)]),
                  rng.randrange(2)]
        if rng.randrange(3) == 0:
            cases.append(['C', *common, *[rng.randint(-30000, 30000) for _ in range(3)],
                          *[rng.randint(-32768, 32767) for _ in range(3)], rng.randrange(65536)])
        else:
            cases.append(['A', *common, rng.randrange(8), rng.randrange(65536), rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-idle-npc-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode())
    fixture = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/idle-npc.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original CMJ/CLizard/CSubmariner constructor and AI cases with CBaddy::Die(0).')


if __name__ == '__main__':
    main()
