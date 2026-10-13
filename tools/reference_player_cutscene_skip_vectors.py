"""Capture untouched PC manual skip gate, commands222/223, first-camera latch and cleanup ordering."""
import argparse
import itertools
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
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long> events;unsigned short links[2];int target[3];int level;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
extern "C" __attribute__((naked)) void aiEnd(){asm volatile("pop %edi;pop %esi;pop %ebx;ret");}
extern "C" __attribute__((naked)) void commandEnd(){asm volatile("pop %ebx;ret");}
extern "C" __attribute__((naked)) void command(void*,void*,int){asm volatile("push %ebx;mov 8(%esp),%eax;mov %eax,0x6A9038;mov 12(%esp),%ebx;mov 16(%esp),%eax;cmp $222,%eax;mov $0x4E14A5,%eax;je 1f;mov $0x4E14EC,%eax;1:jmp *%eax");}
void stop(){events.push_back(0);}
int getLevel(){return level;}
void __attribute__((thiscall)) mode(void*p,int value){events.insert(events.end(),{1,value});at<int>(p,0x2A0)=value;}
unsigned short*getLinks(int node){events.insert(events.end(),{7,node});return links;}
void getPosition(void*out,int node){events.insert(events.end(),{8,node});std::memcpy(out,target,12);}
void __attribute__((thiscall)) orient(void*p,int use,void*direction){auto*d=static_cast<int*>(direction);events.insert(events.end(),{2,use,d[0],d[1],d[2]});}
void __attribute__((thiscall)) distance(void*,int x,int y,int z){events.insert(events.end(),{3,x,y,z});}
void __attribute__((thiscall)) animation(void*,int id,int from,int to){events.insert(events.end(),{4,id,from,to});}
void __attribute__((thiscall)) stand(void*){events.push_back(5);}
void __attribute__((thiscall)) start(void*){events.push_back(6);}
void __attribute__((thiscall)) behind(void*,int duration){events.insert(events.end(),{10,duration});}
void __attribute__((thiscall)) xOffset(void*,short value,unsigned short duration){events.insert(events.end(),{11,value,duration});}
void __attribute__((thiscall)) yOffset(void*,short value,unsigned short duration){events.insert(events.end(),{12,value,duration});}
void __attribute__((thiscall)) zOffset(void*,short value,unsigned short duration){events.insert(events.end(),{13,value,duration});}
void __attribute__((thiscall)) horizontal(void*,unsigned short value,unsigned short duration){events.insert(events.end(),{14,value,duration});}
void __attribute__((thiscall)) vertical(void*,short value,unsigned short duration){events.insert(events.end(),{15,value,duration});}
void printEvents(){std::cout<<"[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]";}
void printPad(unsigned char*p){std::cout<<"[";for(int i=0;i<20;i++)for(int j=0;j<3;j++){if(i||j)std::cout<<',';std::cout<<static_cast<unsigned>(p[16*i+j]);}std::cout<<"]";}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);int family=atoi(argv[2]);
*reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;*reinterpret_cast<unsigned char*>(0x46CD51)=0xC3;
if(family==0)patch(0x4C66AD,(void*)&aiEnd);
if(family==1)patch(0x4E2B3F,(void*)&commandEnd);
if(family==2){patch(0x479E30,(void*)&stop);patch(0x4DE770,(void*)&getLevel);patch(0x4167F0,(void*)&mode);
 patch(0x4E3880,(void*)&getLinks);patch(0x4E3940,(void*)&getPosition);patch(0x4C4BB0,(void*)&orient);
 patch(0x4B9420,(void*)&distance);patch(0x460C10,(void*)&animation);patch(0x4BE4B0,(void*)&stand);patch(0x416AA0,(void*)&start);}
if(family==3){patch(0x4C6654,(void*)&aiEnd);patch(0x416AA0,(void*)&start);patch(0x4C64A0,(void*)&behind);
 patch(0x417AE0,(void*)&xOffset);patch(0x417B60,(void*)&yOffset);patch(0x417BE0,(void*)&zOffset);
 patch(0x4179F0,(void*)&horizontal);patch(0x417A70,(void*)&vertical);}
int modeValue;while(std::cin>>modeValue){alignas(4)unsigned char player[0xEC0]={},camera[0x300]={},pad[0x140]={};events.clear();
*reinterpret_cast<void**>(0x56F3B8)=camera;at<void*>(player,0xE0C)=pad;at<int>(camera,0x2A0)=modeValue;
if(family==0){int synth,allow,force,difficulty,abort,v;std::cin>>synth>>allow>>force>>difficulty>>abort;
 player[0x53C]=1;player[0x1AC]=synth;player[0x1A4]=allow;*reinterpret_cast<int*>(0x68293C)=force;*reinterpret_cast<int*>(0x54D474)=difficulty;*reinterpret_cast<unsigned char*>(0x60CFC4)=abort;
 for(int i=0;i<20;i++)for(int j=0;j<3;j++){std::cin>>v;pad[16*i+j]=v;}
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C65C0)(player);
 std::cout<<"["<<static_cast<int>(*reinterpret_cast<unsigned char*>(0x60CFC4))<<',';printPad(pad);std::cout<<"]\n";}
if(family==1){int value;std::cin>>value;unsigned short word=value;player[0x1A4]=19;at<unsigned>(player,0x34C)=0xABCD0000;
 command(player,&word,modeValue);std::cout<<"["<<static_cast<int>(player[0x1A4])<<','<<at<unsigned>(player,0x34C)<<"]\n";}
if(family==2){int cp,count,e00,v;std::cin>>level>>cp>>count>>e00;at<int>(player,0x1A8)=cp;links[0]=count;links[1]=77;at<int>(player,0xE00)=e00;
 for(unsigned offset:{8u,0xC6Cu})for(int i=0;i<3;i++){std::cin>>v;at<int>(player,offset+4*i)=v;}
 for(int i=0;i<3;i++)std::cin>>target[i];for(int i=0;i<20;i++)for(int j=0;j<3;j++){std::cin>>v;pad[16*i+j]=v;}
 *reinterpret_cast<void**>(0x6A82B8)=nullptr;*reinterpret_cast<unsigned char*>(0x60CFC4)=1;
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4BC020)(player);
 std::cout<<"["<<at<int>(camera,0x2A0)<<','<<at<unsigned>(player,0xE00)<<','<<static_cast<int>(*reinterpret_cast<unsigned char*>(0x60CFC4))<<',';
 printPad(pad);std::cout<<',';printEvents();std::cout<<"]\n";}
if(family==3){int exists,initialized,floor;std::cin>>exists>>initialized>>floor;player[0x53C]=initialized;at<int>(player,0x540)=floor;
 *reinterpret_cast<void**>(0x56F3B8)=exists?camera:nullptr;
 *reinterpret_cast<short*>(0x6A81DC)=-13;*reinterpret_cast<short*>(0x6A81DE)=29;*reinterpret_cast<short*>(0x6A81E0)=43;
 *reinterpret_cast<unsigned short*>(0x6A81FA)=65535;*reinterpret_cast<short*>(0x6A8274)=-128;
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C65C0)(player);
 std::cout<<"["<<static_cast<int>(player[0x53C])<<','<<at<int>(player,0x540)<<',';printEvents();std::cout<<"]\n";}
}}
'''
    rng = random.Random(0x4BC020)
    def pad(): return [rng.randrange(256) for _ in range(60)]
    gates = []
    for synth, allow, force, difficulty, x, circle, start in itertools.product([0, 1], [0, 1], [0, 1], [0, 1, -1], [0, 9], [0, 255], [0, 7]):
        values = pad(); values[3 * 3 + 1] = x; values[2 * 3 + 1] = circle; values[14 * 3 + 1] = start
        gates.append([3, synth, allow, force, difficulty, rng.choice([0, 1]), *values])
    commands = [[opcode, value] for opcode in [222, 223] for value in [0, 1, 2, 255, 256, 32767, 32768, 65535]]
    cleanups = [[mode, level, cp, linked, 123, *[rng.randint(-100000000, 100000000) for _ in range(9)], *pad()]
                for mode, level, cp, linked in itertools.product([3, 4, 5], [257, 514], [0, 19], [0, 1])]
    startups = [[mode, exists, initialized, -1] for mode, exists, initialized in itertools.product([3, 4, 5, 7], [0, 1], [0, 1])]
    output = []
    with tempfile.TemporaryDirectory(prefix='openspidey-original-cutscene-skip-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        for family, name, cases in [(0, 'gate', gates), (1, 'command', commands), (2, 'cleanup', cleanups), (3, 'startup', startups)]:
            completed = subprocess.run([str(binary), str(args.original_exe.resolve()), str(family)],
                                       input='\n'.join(' '.join(map(str, row)) for row in cases) + '\n', text=True, capture_output=True)
            if completed.returncode: raise RuntimeError(f'Original {name} fixture failed {completed.returncode}: {completed.stderr}')
            rows = completed.stdout.splitlines()
            if len(rows) != len(cases): raise RuntimeError(f'Original {name}: expected{len(cases)} rows, got{len(rows)}')
            output += [dict(kind=name, input=case, expected=json.loads(row)) for case, row in zip(cases, rows, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-cutscene-skip.json').write_text(json.dumps(output, separators=(',', ':')) + '\n')
    print(f'Captured {len(gates)} original manual gates, {len(commands)} command 222/223 writes, {len(cleanups)} cleanups and {len(startups)} first-camera latches.')


if __name__ == '__main__': main()
