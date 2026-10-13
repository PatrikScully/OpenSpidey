"""Capture genuine PC Black Cat cinematic VM, two-tick physics and Utils_TurnTowards."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <iostream>
#include <cstdlib>
#include <vector>
struct Vec{int x,y,z;};struct Ang{short x,y,z,pad;};
// Enter only the genuine initialized heading loop in Port_InitAtStart; omit Windows startup.
void __attribute__((naked)) initHeading(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x28,%esp;xorl %ebx,%ebx;movl $0x46cce6,%eax;call *%eax;addl $0x28,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}

std::vector<int> events;unsigned short links[]={1,7};
void event(std::initializer_list<int>x){events.insert(events.end(),x);}
void* alloc(unsigned bytes,int,int,int,int){return std::calloc(1,bytes);}
void release(void*p){std::free(p);}
void position(Vec*p,int node){*p={node*400*4096,node*-80*4096,node*130*4096};event({1,node});}
unsigned short*getLinks(int node){event({2,node});return links;}
int ground(Vec*p,int a,int b,int flags){event({3,p->x,p->y,p->z,a,b,flags});return 160*4096;}
void __attribute__((thiscall)) run(void*p,int id,int from,int to){auto*b=(unsigned char*)p;*reinterpret_cast<unsigned short*>(b+0x12a)=id;b[0x142]=0;event({4,id,from,to});}
void __attribute__((thiscall)) cycle(void*p,int id,int direction){auto*b=(unsigned char*)p;*reinterpret_cast<unsigned short*>(b+0x12a)=id;b[0x142]=0;event({5,id,direction});}
void voice(int bank,int track,int priority){event({6,bank,track,priority});}
void patch(unsigned address,void*t){auto*c=(unsigned char*)address;c[0]=0xe9;*(unsigned*)(c+1)=(unsigned)t-address-5;}
void vectorOut(Vec v){std::cout<<v.x<<','<<v.y<<','<<v.z;}
void angleOut(Ang a){std::cout<<a.x<<','<<a.y<<','<<a.z;}
void motion(unsigned char*b){std::cout<<'[';vectorOut(*(Vec*)(b+8));std::cout<<',';vectorOut(*(Vec*)(b+0x60));std::cout<<',';vectorOut(*(Vec*)(b+0x6c));std::cout<<',';angleOut(*(Ang*)(b+0x14));std::cout<<',';angleOut(*(Ang*)(b+0x88));std::cout<<',';angleOut(*(Ang*)(b+0x8e));std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46cd51=0xc3;initHeading();
patch(0x4581a0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4e3940,(void*)&position);patch(0x4e3880,(void*)&getLinks);patch(0x4e6840,(void*)&ground);patch(0x460c10,(void*)&run);patch(0x460d00,(void*)&cycle);patch(0x479ee0,(void*)&voice);
char mode;while(std::cin>>mode){alignas(4) unsigned char b[0x400]={};
if(mode=='M'){int ticks,gravity,x;std::cin>>ticks>>gravity;*(int*)(b+0x80)=ticks;*(int*)(b+0x32c)=gravity;
for(int offset:{8,0x60,0x6c})for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+offset+axis*4)=x;}
for(int offset:{0x14,0x88,0x8e})for(int axis=0;axis<3;axis++){std::cin>>x;*(short*)(b+offset+axis*2)=x;}
for(int axis=0;axis<3;axis++){std::cin>>x;b[0x94+axis]=x;}
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x414b10)(b);motion(b);std::cout<<'\n';}
else if(mode=='T'){Ang current{},ideal{},vel{},acc{};int factor,x;for(Ang*p:{&current,&ideal,&vel,&acc})for(int axis=0;axis<3;axis++){std::cin>>x;((short*)p)[axis]=x;}std::cin>>factor;
reinterpret_cast<int(*)(Ang,Ang*,Ang*,Ang,int)>(0x4e63e0)(current,&vel,&acc,ideal,factor);std::cout<<'[';angleOut(vel);std::cout<<',';angleOut(acc);std::cout<<"]\n";}
else{int count,frames,x;std::cin>>count;std::vector<short>script(count);for(auto&word:script){std::cin>>x;word=x;}std::cin>>frames;
*(short*)(b+0x21e)=100;*(unsigned short*)(b+0x12a)=12;b[0x340]=b[0x348]=1;*(short**)(b+0x34c)=script.data();
std::cout<<'[';for(int f=0;f<frames;f++){int ticks,abort,finished,signal;std::cin>>ticks>>abort>>finished>>signal;
for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+8+axis*4)=x;}b[0x142]=finished;*(unsigned short*)(b+0x44)=signal;*(int*)(b+0x80)=ticks;*(unsigned char*)0x60cfc4=abort;events.clear();
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x414050)(b);
if(f)std::cout<<',';std::cout<<'['<<int(b[0x340])<<','<<int(b[0x348])<<','<<*(int*)(b+0x344)<<','<<(*(short**)(b+0x34c)-script.data())*2<<','<<*(int*)(b+0x324)<<','<<*(int*)(b+0x328)<<','<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x12a)<<','<<int(b[0x142])<<','<<*(unsigned short*)(b+0x44)<<',';motion(b);
std::cout<<",[";int n=0;for(int*c=*(int**)(b+0x350);c;c=(int*)c[c[1]-1]){if(n++)std::cout<<',';std::cout<<c[0];}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::cout<<"]\n";while(*(void**)(b+0x350))reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*)>(0x413fe0)(b,*(void**)(b+0x350));}}
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x414050)
    cases = []
    for ticks in [-1, 0, 1, 2, 3, 8]:
        for gravity in [0, 8192, 536870911, -536870913]:
            values = [ticks, gravity, 12345, -67890, 34567, 1024, -31, -512, 7, 9, 11,
                      1023, 4095, -1, -1, 45, -33, 13, 41, -17, 1, 1, 1]
            cases.append(dict(mode='M', input=values))
    for _ in range(150):
        values = [rng.randrange(9), rng.choice([0, 8192, 536870913, -536870913]),
                  *[rng.randint(-2147483647, 2147483647) for _ in range(9)],
                  *[rng.randint(-32768, 32767) for _ in range(9)], *[rng.randrange(5) for _ in range(3)]]
        cases.append(dict(mode='M', input=values))
    for _ in range(150):
        values = [*[rng.randint(-32768, 32767) for _ in range(12)], rng.choice([0, 8, 256, -1])]
        cases.append(dict(mode='T', input=values))
    cases.append(dict(mode='T', input=[0, 100, 0, 0, 100, 0, 5, 6, 7, 8, 9, 10, 8]))
    programs = [[0, 6, 1, 4, 0, 6, 8, 0, 0, 255], [0, 8, 7, 4, 0, 6, 3, 6, 0, 255],
                [0, 1, 3, 0, 4, 2, -1, 5, 0, 255], [0, 15, 12, 0, 255],
                [0, 16, 3, 0, 6, 1, 10, 0, 255], [0, 6, 1, 100, 0, 2, 1, 0, 255],
                [0, 5, 1, 48, 0, 255], [5, 6, 3, 2, 0, 255], [-2, 6, 3, 2, 0, 255],
                [0, 99, 0, 6, 2, 1, 0, 255]]
    for program in programs:
        for variation in range(5):
            frames = []
            for frame in range(9):
                position = [0, 0, 0] if frame < 3 else [400*4096, -80*4096, 130*4096]
                frames.append([variation + 1, int(variation == 4 and frame == 2), int(frame % 2 == 0),
                               int(variation == 3 and frame == 3), *position])
            cases.append(dict(mode='V', program=program, frames=frames))
    rows = []
    for case in cases:
        values = case.get('input')
        if values is None:
            values = [len(case['program']), *case['program'], len(case['frames']),
                      *[v for frame in case['frames'] for v in frame]]
        rows.append(case['mode'] + ' ' + ' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-cinematic-') as folder:
        cpp, binary = Path(folder)/'reference.cpp', Path(folder)/'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/cinematic-actors.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} actual PC Black Cat motion, steering and cinematic sequences; interpreter/math instructions remain untouched.')


if __name__ == '__main__':
    main()
