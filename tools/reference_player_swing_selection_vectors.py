"""Capture complete original PC swing4C18A0 and swing-button zip4C1460 at proved typed world/availability/Web boundaries."""
import argparse
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
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;unsigned face[4],pattern;int probe,currentKind,randomValue;unsigned char web[0x140];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void vec(std::vector<long long>&e,CVector p){e.insert(e.end(),{p.vx,p.vy,p.vz});}
void __attribute__((thiscall)) animation(void*,int clip,int first,int last){events.insert(events.end(),{4,clip,first,last});}
void world(void*p,int mode){int index=probe++;currentKind=index<8?(pattern>>(index*4))&15:0;events.insert(events.end(),{6,index});vec(events,at<CVector>(p,0));vec(events,at<CVector>(p,12));events.push_back(mode);at<void*>(p,0x68)=currentKind?reinterpret_cast<void*>(1):nullptr;at<void*>(p,0x80)=currentKind?face:nullptr;if(currentKind){face[3]=(currentKind==3||currentKind==4)?0x40000:0;at<CVector>(p,0x6C)=at<CVector>(p,12)-CVector(111+index,222+index,333+index);at<short>(p,0x78)=-123;at<short>(p,0x7A)=currentKind==3?-2600:currentKind==4?-2601:-4096;at<short>(p,0x7C)=-3456;at<int>(p,0x88)=777+index;}}
unsigned char __attribute__((thiscall)) swingAvailable(void*p,void*line){events.push_back(7);vec(events,at<CVector>(line,0x6C));at<CVector>(p,0xDC0)=at<CVector>(line,0x6C);at<CVector>(p,0xDA0)=CVector(probe*11,-probe*12,probe*13);return currentKind!=2;}
unsigned char __attribute__((thiscall)) zipAvailable(void*,void*,int range){events.insert(events.end(),{7,range});return currentKind!=2;}
int rnd(int bound){assert(bound==512);events.insert(events.end(),{11,bound});return randomValue;}
void*allocate(int size){assert(size==0x13C);events.push_back(8);memset(web,0,sizeof web);return web;}
void* __attribute__((thiscall)) constructor(void*p){events.push_back(9);return p;}
int __attribute__((thiscall)) fire(void*p,bool held,int cost,CVector*target,bool hit,short*normal){events.insert(events.end(),{10,held,cost});vec(events,*target);events.insert(events.end(),{hit,normal[0],normal[1],normal[2],at<unsigned char>(p,0x8F8),at<unsigned char>(p,0xE10)});return 0;}
void noAssert(int,const char*){}
extern "C" __attribute__((naked)) void zipEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0xFC,%esp;ret");}
extern "C" __attribute__((naked)) unsigned char zipStep(void*){asm volatile("sub $0xFC,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x110(%esp),%esi;xor %ebx,%ebx;mov $0x4C1483,%eax;jmp *%eax");}
void emitEvents(){std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
void emitVector(void*p,unsigned off){auto&v=at<CVector>(p,off);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;at<short>((void*)0x610C48,i*4)=std::sin(angle)*4096.;at<short>((void*)0x610C48,i*4+2)=std::cos(angle)*4096.;}int family=atoi(argv[2]);patch(0x460C10,(void*)&animation);patch(0x4549A0,(void*)&world);patch(0x4015B0,(void*)&noAssert);
if(family){patch(0x4C30D0,(void*)&zipAvailable);patch(0x4E5DA0,(void*)&rnd);patch(0x455390,(void*)&allocate);patch(0x4F5DA0,(void*)&constructor);patch(0x4C5DD0,(void*)&fire);patch(0x4C1843,(void*)&zipEnd);}else patch(0x4C31D0,(void*)&swingAvailable);
long long v[34];while(std::cin>>v[0]){for(int i=1;i<34;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},controls[0x200]={};at<void*>(player,0xE0C)=controls;at<CVector>(player,8)=CVector(v[0],v[1],v[2]);at<CVector>(player,0xC6C)=CVector(v[3],v[4],v[5]);at<CVector>(player,0xC84)=CVector(v[6],v[7],v[8]);at<CVector>(player,0xC78)=CVector(v[9],v[10],v[11]);at<unsigned>(player,0xE1C)=v[12];controls[0x70]=v[13];*reinterpret_cast<unsigned char*>(0x60CFC7)=v[14];player[0x1AC]=v[15];player[0xE8D]=v[16];controls[0x100]=v[17];player[0x8EA]=v[18];at<void*>(player,0xE48)=v[19]?reinterpret_cast<void*>(1):nullptr;player[0x550]=v[20];at<int>(player,0x5CC)=v[21];at<int>(player,0x5C8)=v[22];pattern=v[23];randomValue=v[24];at<int>(player,100)=v[25];player[0xAD4]=v[26];player[0x8ED]=v[27];at<int>(player,0xDF8)=v[28];player[0x54C]=v[29];player[0x201]=v[30];controls[0x41]=v[31];player[0x5E8]=v[32];player[0xE10]=v[33];player[0x8F8]=3;at<int>(player,0xE20)=99;at<CVector>(player,0xDC0)=CVector(11,-12,13);at<CVector>(player,0xDA0)=CVector(21,-22,23);at<CVector>(player,0xDAC)=CVector(31,-32,33);at<CVector>(player,0x558)=CVector(41,-42,43);probe=0;events.clear();unsigned char result=family?zipStep(player):reinterpret_cast<unsigned char(__attribute__((thiscall)) *)(void*)>(0x4C18A0)(player);std::cout<<'['<<unsigned(result)<<','<<at<unsigned>(player,0xE1C)<<','<<unsigned(player[0x8ED])<<','<<unsigned(player[0xAD4])<<','<<at<int>(player,0xDF8)<<','<<unsigned(player[0x54C])<<','<<at<int>(player,100)<<','<<at<int>(player,0xE20)<<','<<unsigned(player[0x201])<<','<<unsigned(controls[0x41])<<','<<at<int>(player,0x5CC)<<','<<at<int>(player,0x5C8)<<','<<unsigned(player[0x8F8])<<','<<unsigned(player[0xE10])<<','<<(at<void*>(player,0xE6C)?1:0)<<','<<(at<void*>(player,0xE6C)?at<short>(web,0x102):-1)<<','<<(at<void*>(player,0xE6C)?at<int>(web,0xF8):-1);for(unsigned o:{0xDC0u,0xDA0u,0xDACu,0x558u})emitVector(player,o);emitEvents();std::cout<<"]\n";}}
'''
    rng = random.Random(0x4C18A0)
    base = [12345,-23456,34567, 0,0,-4096, 0,-4096,0, 4096,0,0, 4,1,0,0,1,1,0,0,0, 0,0, 1,17,12345,1,77,88,99,123,122,1,5]
    cases = []
    for action in [1,2,4,8,0x10,0x40000,0x400]:
        for cursor in range(6):
            for pattern in [0,1,2,3,4,0x10,0x20,0x30,0x40,0x100,0x110,0x210,0x310,0x11111110,0x21111110,0x11111120]:
                case=base.copy();case[12]=action;case[21]=cursor;case[22]=5-cursor;case[23]=pattern;cases.append(case)
    for _ in range(500):
        case=base.copy()
        case[:12]=[rng.randint(-1000000,1000000) for _ in range(12)]
        case[12]=rng.choice([1,2,4,8,0x10,0x40000,0x400])
        case[13:21]=[rng.choice([0,1,2]) for _ in range(8)]
        case[21:23]=[rng.randrange(6),rng.randrange(6)]
        case[23]=sum(rng.randrange(5)<<(i*4) for i in range(8))
        case[24:]=[rng.randrange(512),rng.randint(-2147483648,2147483647),rng.randrange(2),rng.randrange(256),rng.randint(-2147483648,2147483647),rng.randrange(256),rng.randrange(256),rng.randrange(256),rng.randrange(2),rng.randrange(256)]
        cases.append(case)
    # Each individual practice/physical-input and held/aim/latch gate is observed.
    for practice in [0,1,2]:
        for pressed in [0,1]:
            for synth in [0,1]:
                for release in [0,1]:
                    for jump in [0,1]:
                        for gate in [None,18,19,20]:
                            case=base.copy();case[14]=practice;case[13]=pressed;case[15]=synth;case[16]=release;case[17]=jump
                            if gate is not None:case[gate]=1
                            cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-selection-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        output=[]
        requests=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()
        for family,name in [(0,'swing'),(1,'swing_zip')]:
            lines=subprocess.check_output([str(binary),str(args.original_exe.resolve()),str(family)],input=requests).decode().splitlines()
            output += [dict(kind=name,input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-swing-selection.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} whole original PC swing selectors and {len(cases)} complete R2 zip selectors with ordered fan probes, practice gates, genuine random bounds and shared Web/animation writes.')


if __name__ == '__main__':main()
