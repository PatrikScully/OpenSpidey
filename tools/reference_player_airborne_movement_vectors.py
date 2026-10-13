"""Capture complete original PC action2/4 dispatch and CheckLanded4C24E0, with genuine proved guard/Hit/animation boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;unsigned guards;int level,randomValue,afterHealth,afterClip;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) animation(void*p,int clip,int first,int last){events.insert(events.end(),{10,clip,first,last});at<short>(p,0x12A)=clip;at<short>(p,0x128)=first;at<unsigned char>(p,0x142)=0;}
int guard(int index){events.push_back(index);return(guards>>index)&1;}
#define GUARD(name,index) int __attribute__((thiscall))name(void*){return guard(index);}
GUARD(ceiling,1)GUARD(smash,2)GUARD(swing,3)GUARD(r1,4)GUARD(r2,5)GUARD(landed,6)GUARD(wall,7)GUARD(flail,8)
void __attribute__((thiscall)) death(void*,bool mode){events.insert(events.end(),{9,mode});}
int levelId(){events.push_back(11);return level;}
int rnd(int bound){events.insert(events.end(),{12,bound});return randomValue;}
int sound(unsigned id,CVector*p,int pitch){events.insert(events.end(),{13,id,p->vx,p->vy,p->vz,pitch});return 0;}
void actuator(unsigned char pad,unsigned short time,unsigned char argument,unsigned char strength){events.insert(events.end(),{14,pad,time,argument,strength});}
int __attribute__((thiscall)) hit(void*p,void*info){events.insert(events.end(),{15,at<unsigned char>(info,0),at<unsigned short>(info,8),at<CVector>(info,12).vx,at<CVector>(info,12).vy,at<CVector>(info,12).vz});at<short>(p,0xE2)=afterHealth;if(afterClip>=0)at<short>(p,0x12A)=afterClip;return 0;}
extern "C" __attribute__((naked)) void finish(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void step(void*,int,int){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;mov 0x4C8(%esp),%eax;mov %eax,0x24(%esp);cmpl $0,0x4CC(%esp);mov $0x4B274D,%eax;je 1f;mov $0x4B28D9,%eax;1:jmp *%eax");}
void fields(void*p){std::cout<<'['<<at<unsigned>(p,0xE1C)<<','<<at<unsigned short>(p,0x12A)<<','<<at<short>(p,0x128)<<','<<unsigned(at<unsigned char>(p,0x142))<<','<<at<short>(p,0xEA6)<<','<<at<int>(p,0xE80)<<','<<at<int>(p,0xE84)<<','<<at<int>(p,0xE88)<<','<<at<int>(p,0x60)<<','<<at<int>(p,0x64)<<','<<at<int>(p,0x68)<<','<<unsigned(at<unsigned char>(p,0x54E))<<','<<unsigned(at<unsigned char>(p,0x54D))<<','<<unsigned(at<unsigned char>(p,0xAE5))<<','<<unsigned(at<unsigned char>(p,0xAE6))<<','<<at<int>(p,0xE38)<<','<<at<int>(p,0xE3C)<<','<<*reinterpret_cast<int*>(0x60CFA4)<<','<<at<short>(p,0xE2)<<','<<unsigned(at<unsigned char>(p,0xE8C))<<','<<unsigned(at<unsigned char>(p,0x2C1));auto*controls=at<unsigned char*>(p,0xE0C);std::cout<<','<<unsigned(controls[0x101])<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);int family=atoi(argv[2]);patch(0x4C9130,(void*)&animation);patch(0x460C10,(void*)&animation);if(family<2){patch(0x4BFCE0,(void*)&ceiling);patch(0x4C1EB0,(void*)&smash);patch(0x4C18A0,(void*)&swing);patch(0x4C0EE0,(void*)&r1);patch(0x4C1460,(void*)&r2);patch(0x4C24E0,(void*)&landed);patch(0x4BFEC0,(void*)&wall);patch(0x4BDF10,(void*)&flail);patch(0x4BDFF0,(void*)&death);patch(0x4DE770,(void*)&levelId);patch(0x4B6E8C,(void*)&finish);}else{patch(0x4E5DA0,(void*)&rnd);patch(0x471EA0,(void*)&sound);patch(0x4794D0,(void*)&actuator);}
unsigned table[4]={0,0,0,reinterpret_cast<unsigned>(&hit)};long long v[35];while(std::cin>>v[0]){for(int i=1;i<35;i++)std::cin>>v[i];alignas(4)unsigned char player[0xF20]={},controls[0x200]={};at<void*>(player,0)=table;at<void*>(player,0xE0C)=controls;at<unsigned>(player,0xE1C)=family==0?2:4;at<short>(player,0xE2)=v[0];at<short>(player,0x12A)=v[1];at<short>(player,0x128)=v[2];player[0x142]=v[3];at<short>(player,0xEA6)=77;at<int>(player,0xE80)=v[4];at<int>(player,0xE84)=v[5];at<int>(player,0xE88)=v[6];at<CVector>(player,0x60)=CVector(v[7],v[8],v[9]);controls[0x100]=v[10];*reinterpret_cast<int*>(0x54D474)=v[11];level=v[12];guards=v[13];at<unsigned>(player,0x504)=v[14];at<unsigned>(player,0x500)=v[15];*reinterpret_cast<unsigned*>(0x6B4CA8)=v[16];player[0x54E]=v[17];player[0x54D]=v[18];player[0xAE5]=v[19];player[0xAE6]=v[20];at<CVector>(player,8)=CVector(12345,v[21],34567);at<int>(player,0xE38)=v[22];at<int>(player,0xE3C)=v[23];at<unsigned short>(player,0xE8E)=v[24];*reinterpret_cast<int*>(0x60CFA4)=99;at<unsigned short>(player,0xE0)=v[26];at<int>(player,0x34C)=v[27];randomValue=v[28];at<int>(player,0xE40)=v[29];at<int>(player,0xE44)=v[30];at<int>(player,0xEF0)=100;*reinterpret_cast<unsigned char*>(0x6828D3)=v[31];player[0xE2D]=v[32];player[0xE2E]=v[33];player[0xE8C]=v[34];player[0x2C1]=123;controls[0x101]=122;afterHealth=v[0]-1;afterClip=-1;events.clear();if(family<2)step(player,v[25],family);else{int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(0x4C24E0)(player);std::cout<<result<<' ';}fields(player);}}
'''
    base=[50,231,7,0,-245760,0x70000,0xA8000,12345,-12345,34567,1,2,0x101,0,0,900,1000,123,122,1,2,-100000,123000,777,0,-20000,0,0,1,0,2048,0,0,0,1]
    cases=[]
    for clip in [0,127,175,176,211,212,215,216,218,219,221,222,224,225,226,228,231,232,233,235,238,239,240,241,276,278,280]:
        for finished in [0,1]:
            for before,after in [(-12345,-34567),(-12345,34567),(12345,-34567),(12345,34567)]:
                for pending in [0,1,2]:
                    case=base.copy();case[1]=clip;case[3]=finished;case[8]=after;case[18]=pending;case[25]=before;cases.append(case)
    for guard in range(1,9):
        for health in [-1,0,50]:
            case=base.copy();case[0]=health;case[13]=1<<guard;cases.append(case)
    for phase,hold in [(-1,-1),(0,0),(1,0),(0,1),(2147483647,-2147483648)]:
        for held in [0,1]:
            for difficulty,level in [(0,0x101),(0,0x705),(1,0x705),(2,0x101)]:
                case=base.copy();case[5]=phase;case[6]=hold;case[10]=held;case[11]=difficulty;case[12]=level;cases.append(case)
    rng=random.Random(0x4B274D)
    for _ in range(400):
        case=base.copy();case[0]=rng.choice([-1,0,1,50]);case[1]=rng.choice([0,127,175,176,215,216,231,232,276,280]);case[3]=rng.randrange(2);case[5:7]=[rng.choice([-1,0,1,0x70000]),rng.choice([-1,0,1,0xA8000])];case[7:10]=[rng.randint(-2147483648,2147483647) for _ in range(3)];case[10]=rng.randrange(2);case[13]=rng.choice([0,1<<rng.randrange(1,9)]);case[14]=rng.choice([0,2,4,6]);case[15]=rng.choice([0,995,994,999,1000,1001,0xFFFFFFFF]);case[16]=rng.choice([0,1000,0xFFFFFFFF]);case[17:21]=[rng.randrange(3) for _ in range(4)];case[21:24]=[rng.randint(-2147483648,2147483647) for _ in range(3)];case[24]=rng.choice([0,480,481,65535]);case[25]=rng.randint(-2147483648,2147483647);cases.append(case)
    landing=[]
    for clip in [0,127,175,176,211,212,215,216,225,226,228,231,232,233,235,276,278,280]:
        for collide in [0,2]:
            for hard in [0,1]:
                for vibration in [0,1]:
                    for move in [(0,0),(1,0),(0,-1)]:
                        for running in [0,1]:
                            case=base.copy();case[1]=clip;case[26]=collide;case[27]=hard;case[31]=vibration;case[32:34]=move;case[34]=running;landing.append(case)
    for start,full in [(128,512),(1,2),(128,-512),(-1,0)]:
        for drop in [-1,0,1,127,128,129,256,512,513,4096]:
            case=base.copy();case[21]=drop<<12;case[22]=0;case[26]=2;case[29]=start;case[30]=full;case[31]=1;landing.append(case)
    for health in [-1,0,1,50]:
        case=base.copy();case[0]=health;case[21]=256<<12;case[22]=0;case[26]=2;case[29]=128;case[30]=512;case[31]=1;landing.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-airborne-movement-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        output=[]
        for family,name,rows in [(0,'jumping',cases),(1,'falling',cases),(2,'landed',landing)]:
            lines=subprocess.check_output([str(binary),str(args.original_exe.resolve()),str(family)],input=('\n'.join(' '.join(map(str,row)) for row in rows)+'\n').encode()).decode().splitlines()
            for row,line in zip(rows,lines,strict=True):
                if family==2:
                    result,fields=line.split(' ',1);output.append(dict(kind=name,input=row,result=int(result),expected=json.loads(fields)))
                else:output.append(dict(kind=name,input=row,expected=json.loads(line)))
    (root/'modern/OpenSpidey.Tests/Fixtures/player-airborne-movement.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original jumping and {len(cases)} wholefalling cases plus{len(landing)} actual CheckLanded sequences.')


if __name__=='__main__':main()
