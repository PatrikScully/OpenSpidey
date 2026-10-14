"""Capture the original CTorch script interpreter, scaled physics, AI and constructor."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <vector>
struct Vec{int x,y,z;};struct Ang{short x,y,z,pad;};
void __attribute__((naked)) initHeading(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x28,%esp;xorl %ebx,%ebx;movl $0x46cce6,%eax;call *%eax;addl $0x28,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
std::vector<int> events;unsigned short links[]={1,7};int level=0;alignas(4) unsigned char player[0x100]={};alignas(4) unsigned char joints[0x200]={};
void event(std::initializer_list<int>x){events.insert(events.end(),x);}
void* alloc(unsigned bytes,int,int,int,int){return std::calloc(1,bytes);}
void release(void*p){std::free(p);}
void position(Vec*p,int node){*p={node*400*4096,node*-80*4096,node*130*4096};event({1,node});}
unsigned short*getLinks(int node){event({2,node});return links;}
int ground(Vec*p,int a,int b,int flags){event({3,p->x,p->y,p->z,a,b,flags});return 160*4096;}
void __attribute__((thiscall)) run(void*p,int id,int from,int to){auto*b=(unsigned char*)p;*(unsigned short*)(b+0x12a)=id;b[0x142]=0;event({4,id,from,to});}
void __attribute__((thiscall)) cycle(void*p,int id,int direction){auto*b=(unsigned char*)p;*(unsigned short*)(b+0x12a)=id;b[0x142]=0;event({5,id,direction});}
void voice(int bank,int track,int priority){event({6,bank,track,priority});}
void pulse(unsigned short*p){for(int i=1;i<=p[0];i++)event({8,p[i]});}
void build(void*){event({9});}
void __attribute__((thiscall)) shadow(void*){event({10});}
void __attribute__((thiscall)) pose(void*p,void*packet){auto*b=(unsigned char*)p;if(!*(void**)(b+0x188))*(void**)(b+0x188)=joints;event({11,int((unsigned)packet)});}
void __attribute__((thiscall)) cleanup(void*p,int all,int keep){*(void**)((unsigned char*)p+0x290)=nullptr;event({12,all,keep});}
void wibble(int region){event({13,region});}
int getLevel(){return level;}
void* __attribute__((thiscall)) baddy(void*p){event({14});return p;}
void __attribute__((thiscall)) initItem(void*,const char*name){event({15,!std::strcmp(name,"torch")});}
void __attribute__((thiscall)) attach(void*,void*list){event({16,int((unsigned)list)});}
void diagnostic(int,const char*){}
void patch(unsigned address,void*t){auto*c=(unsigned char*)address;c[0]=0xe9;*(unsigned*)(c+1)=(unsigned)t-address-5;}
void vectorOut(Vec v){std::cout<<v.x<<','<<v.y<<','<<v.z;}
void angleOut(Ang a){std::cout<<a.x<<','<<a.y<<','<<a.z;}
void motion(unsigned char*b){std::cout<<'[';vectorOut(*(Vec*)(b+8));std::cout<<',';vectorOut(*(Vec*)(b+0x60));std::cout<<',';vectorOut(*(Vec*)(b+0x6c));std::cout<<',';angleOut(*(Ang*)(b+0x14));std::cout<<',';angleOut(*(Ang*)(b+0x88));std::cout<<',';angleOut(*(Ang*)(b+0x8e));std::cout<<']';}
void eventsOut(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
void commandsOut(unsigned char*b){std::cout<<'[';int n=0;for(int*c=*(int**)(b+0x350);c;c=(int*)c[c[1]-1]){if(n++)std::cout<<',';std::cout<<c[0];}std::cout<<']';}
void killCommands(unsigned char*b){while(*(void**)(b+0x350))reinterpret_cast<void*(__attribute__((thiscall))*)(void*,void*)>(0x413fe0)(b,*(void**)(b+0x350));}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46cd51=0xc3;initHeading();
patch(0x4581a0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4e3940,(void*)&position);patch(0x4e3880,(void*)&getLinks);patch(0x4e6840,(void*)&ground);patch(0x460c10,(void*)&run);patch(0x460d00,(void*)&cycle);patch(0x479ee0,(void*)&voice);
patch(0x4dfd30,(void*)&pulse);patch(0x475f50,(void*)&build);patch(0x4dd760,(void*)&shadow);patch(0x460e80,(void*)&pose);patch(0x403a90,(void*)&cleanup);patch(0x475fb0,(void*)&wibble);patch(0x4de770,(void*)&getLevel);
patch(0x402c00,(void*)&baddy);patch(0x460020,(void*)&initItem);patch(0x460260,(void*)&attach);patch(0x4015b0,(void*)&diagnostic);
// Only the Windows SEH registration reads/writes of fs:0 are removed from the constructor.
for(unsigned char*c=(unsigned char*)0x4dc4f0;c<(unsigned char*)0x4dc5f0;c++){if(c[0]==0x64&&c[1]==0xa1&&!*(unsigned*)(c+2)){c[0]=0x31;c[1]=0xc0;std::memset(c+2,0x90,4);}
if(c[0]==0x64&&c[1]==0x89&&!*(unsigned*)(c+3)){std::memset(c,0x90,7);}}
*(void**)0x6a9038=player;
char mode;while(std::cin>>mode){alignas(4) unsigned char b[0x400]={};int x;events.clear();std::memset(joints,0,sizeof joints);
if(mode=='P'){int step,gravity;std::cin>>step>>gravity;*(int*)(b+0x338)=step;*(int*)(b+0x32c)=gravity;
for(int offset:{8,0x60,0x6c})for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+offset+axis*4)=x;}
for(int offset:{0x14,0x88,0x8e})for(int axis=0;axis<3;axis++){std::cin>>x;*(short*)(b+offset+axis*2)=x;}
for(int axis=0;axis<3;axis++){std::cin>>x;b[0x94+axis]=x;}
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4dd5f0)(b);motion(b);std::cout<<'\n';}
else if(mode=='C'){int abort,node;std::cin>>level>>abort>>node;alignas(4) unsigned char data[20]={};for(int i=0;i<3;i++){std::cin>>x;*(int*)(data+i*4)=x;}for(int i=0;i<3;i++){std::cin>>x;*(short*)(data+12+i*2)=x;}*(unsigned char*)0x60cfc4=abort;
reinterpret_cast<void*(__attribute__((thiscall))*)(void*,short*,int)>(0x4dc4f0)(b,(short*)data,node);
std::cout<<'['<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x46)<<','<<*(short*)(b+0x21e)<<','<<*(int*)(b+0x330)<<','<<*(int*)(b+0x334)<<','<<*(unsigned short*)(b+0x12a)<<','
<<*(unsigned*)(b+0x3c)<<','<<*(short*)(b+0x38)<<','<<*(int*)(b+0x31c)<<','<<*(unsigned short*)(b+0xde)<<','<<*(unsigned short*)(b+0xdc)<<','<<(*(unsigned char**)(b+0x34c)-data)<<','<<int(b[0x211])<<',';motion(b);std::cout<<',';eventsOut();std::cout<<"]\n";}
else if(mode=='V'){int count,frames;std::cin>>count;std::vector<short>script(count);for(auto&word:script){std::cin>>x;word=x;}std::cin>>frames;
*(short*)(b+0x21e)=100;*(unsigned short*)(b+0x12a)=12;b[0x340]=b[0x348]=1;*(short**)(b+0x34c)=script.data();
std::cout<<'[';for(int f=0;f<frames;f++){int ticks,abort,finished,signal;std::cin>>ticks>>abort>>finished>>signal;
for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+8+axis*4)=x;}b[0x142]=finished;*(unsigned short*)(b+0x44)=signal;*(int*)(b+0x80)=ticks;*(unsigned char*)0x60cfc4=abort;events.clear();
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4dcb60)(b);
if(f)std::cout<<',';std::cout<<'['<<int(b[0x340])<<','<<int(b[0x348])<<','<<*(int*)(b+0x344)<<','<<(*(short**)(b+0x34c)-script.data())*2<<','<<*(int*)(b+0x324)<<','<<*(int*)(b+0x328)<<','<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x12a)<<','<<int(b[0x142])<<','<<*(unsigned short*)(b+0x44)<<',';motion(b);
std::cout<<',';commandsOut(b);std::cout<<',';eventsOut();std::cout<<']';}
std::cout<<"]\n";killCommands(b);}
else{int count,frames,stage,sub,anim,frame,behavior,item,gravity,groundY,previous,older,hasJoints,pan,tilt,message,node,combat,px,pz;
std::cin>>level>>stage>>sub>>anim>>frame>>behavior>>item>>gravity>>groundY>>previous>>older>>hasJoints>>pan>>tilt>>message>>node>>combat>>px>>pz;
*(int*)(player+8)=px;*(int*)(player+16)=pz;*(int*)(b+0x31c)=stage;*(int*)(b+0x320)=sub;*(unsigned short*)(b+0x12a)=anim;*(short*)(b+0x128)=frame;*(unsigned*)(b+0x218)=behavior;
*(unsigned short*)(b+4)=item;*(int*)(b+0x32c)=gravity;*(short*)(b+0x222)=groundY;*(int*)(b+0x330)=previous;*(int*)(b+0x334)=older;*(short*)(b+0x21e)=100;b[0x1f]=7;
if(hasJoints){*(void**)(b+0x188)=joints;*(short*)(joints+36)=pan;*(short*)(joints+38)=tilt;}*(void**)(b+0x290)=message?(void*)b:nullptr;*(unsigned short*)(b+0xde)=node;*(unsigned*)(b+0x2a8)=combat;b[0x94]=b[0x95]=b[0x96]=1;
for(int offset:{8,0x60})for(int axis=0;axis<3;axis++){std::cin>>x;*(int*)(b+offset+axis*4)=x;}
std::cin>>count;std::vector<short>script(count);for(auto&word:script){std::cin>>x;word=x;}*(short**)(b+0x34c)=script.data();std::cin>>frames;
std::cout<<'[';for(int f=0;f<frames;f++){int ticks,abort,finished,signal;std::cin>>ticks>>abort>>finished>>signal;b[0x142]=finished;*(unsigned short*)(b+0x44)=signal;*(int*)(b+0x80)=ticks;*(unsigned char*)0x60cfc4=abort;events.clear();
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4dc6b0)(b);unsigned char*j=*(unsigned char**)(b+0x188);
if(f)std::cout<<',';std::cout<<'['<<*(int*)(b+0x31c)<<','<<*(int*)(b+0x320)<<','<<*(int*)(b+0x32c)<<','<<*(short*)(b+0x222)<<','<<*(int*)(b+0x338)<<','<<*(int*)(b+0x330)<<','<<*(int*)(b+0x334)<<','
<<*(unsigned*)(b+0x218)<<','<<*(unsigned short*)(b+4)<<','<<*(unsigned short*)(b+0x46)<<','<<*(unsigned short*)(b+0x12a)<<','<<int(b[0x142])<<','<<int(b[0x340])<<','<<int(b[0x348])<<','<<*(int*)(b+0x344)<<','
<<(*(short**)(b+0x34c)-script.data())*2<<','<<(j?*(short*)(j+36):-99999)<<','<<(j?*(short*)(j+38):-99999)<<','<<int(b[0x211])<<','<<*(unsigned short*)(b+0x44)<<',';motion(b);std::cout<<',';commandsOut(b);std::cout<<',';eventsOut();std::cout<<']';}
std::cout<<"]\n";killCommands(b);}}
}
'''

PROGRAMS = [[0, 6, 1, 4, 0, 6, 8, 0, 0, 255], [0, 8, 7, 4, 0, 6, 3, 6, 0, 255],
            [0, 1, 3, 0, 4, 2, -1, 5, 0, 255], [0, 15, 12, 0, 255],
            [0, 16, 3, 0, 6, 1, 10, 0, 255], [0, 6, 1, 100, 0, 2, 1, 0, 255],
            [0, 5, 1, 48, 0, 255], [5, 6, 3, 2, 0, 255], [-2, 6, 3, 2, 0, 255],
            [0, 99, 0, 6, 2, 1, 0, 255], [0, 16, 2, 0, 2, 4, 0, 255], [0, 2, 0, 0, 2, 2, 0, 255]]
AI_PROGRAMS = [[0, 255], [0, 6, 1, 6, 0, 255], [0, 15, 1, 0, 255], [0, 16, 2, 0, 6, 3, 9, 0, 255], [0, 2, 1, 0, 255]]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x328)
    cases = []
    for step in [0, 1365 * 6, 1365 * 2, 1365 * 9, 4096, 70000]:
        for gravity in [0, 8192, 536870911, -536870913]:
            cases.append(dict(mode='P', input=[step, gravity, 12345, -67890, 34567, 1024, -31, -512, 7, 9, 11,
                                               1023, 4095, -1, -1, 45, -33, 13, 41, -17, 1, 1, 1]))
    for _ in range(200):
        cases.append(dict(mode='P', input=[1365 * rng.randrange(13), rng.choice([0, 8192, 536870913, -536870913]),
                                           *[rng.randint(-2147483647, 2147483647) for _ in range(9)],
                                           *[rng.randint(-32768, 32767) for _ in range(9)], *[rng.randrange(5) for _ in range(3)]]))
    for level in [0x201, 0x803]:
        for abort in [0, 1]:
            for node in [135, 0xFFFF]:
                cases.append(dict(mode='C', input=[level, abort, node, 12, -34, 56, 100, -200, 3000]))
    for program in PROGRAMS:
        for variation in range(5):
            frames = []
            for frame in range(9):
                position = [0, 0, 0] if frame < 3 else [400*4096, -80*4096, 130*4096]
                frames.append([variation + 1, int(variation == 4 and frame == 2), int(frame % 2 == 0),
                               int(variation == 3 and frame == 3), *position])
            cases.append(dict(mode='V', program=program, frames=frames))
    starts = [(1, 0), (1, 1), (1, 2), (2, 0), (2, 1), (4, 0)]
    for program in AI_PROGRAMS:
        for stage, sub in starts:
            for variant in range(6):
                anim = [0, 1, 1, 5, 4, 0][variant]
                header = [0x803 if variant in (2, 5) else 0x201, stage, sub, anim, [0, 17, 18, 30, 2, 0][variant],
                          [0, 0, 1, 1, 0, 0][variant], [0x482, 0x486, 0x482, 0x486, 0x486, 0x482][variant],
                          [0, 0x2000, 0, 0x2000, 0, 0][variant], [0, 160, 160, 160, 90, 0][variant],
                          [2, 2, 3, 1, 2, 0][variant], [2, 2, 1, 2, 0, 0][variant], int(variant != 4),
                          [0, 30, -70, 2047, 0, 0][variant], [0, -40, 0, 100, 0, 0][variant],
                          int(variant in (1, 3)), [135, 135, 0xFFFF, 135, 135, 135][variant],
                          [0, 0, 0, 0x4000, 0, 0][variant], 9000*4096 - variant * 30000, -4000*4096 + variant * 77777,
                          10*4096, 50*4096 + variant * 4096 * 20, -30*4096, 0, variant * 4096, 0]
                frames = [[[2, 1, 3, 2, 4, 2, 2, 1][f], int(variant == 5 and f == 4), int(f % 3 == 2), int(f == 5)]
                          for f in range(8)]
                cases.append(dict(mode='A', header=header, program=program, frames=frames))
    rows = []
    for case in cases:
        if case['mode'] in ('P', 'C'):
            values = case['input']
        elif case['mode'] == 'V':
            values = [len(case['program']), *case['program'], len(case['frames']), *[v for f in case['frames'] for v in f]]
        else:
            values = [*case['header'], len(case['program']), *case['program'], len(case['frames']),
                      *[v for f in case['frames'] for v in f]]
        rows.append(case['mode'] + ' ' + ' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-torch-') as folder:
        cpp, binary = Path(folder)/'reference.cpp', Path(folder)/'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/torch.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} actual PC CTorch constructor, physics, interpreter and AI cases.')


if __name__ == '__main__':
    main()
