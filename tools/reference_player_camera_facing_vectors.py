"""Capture the whole actual PC4B6E8C..4B70D5 camera-facing slice with genuine Utils_CalcAim math."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;unsigned char target[0x100];short heading;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) behind(void*,int duration){events.insert(events.end(),{1,duration});}
void __attribute__((thiscall)) angle(void*,short wanted,unsigned short duration){events.insert(events.end(),{2,wanted,duration});}
short __attribute__((thiscall)) effective(void*){events.push_back(3);return heading;}
void* recover(void*){events.push_back(4);return target;}
double arc(double v){return std::atan(v);}double square(double v){return std::sqrt(v);}
extern "C" __attribute__((naked)) void finish(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void step(void*){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;xor %ebp,%ebp;mov $0x4B6E8C,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C64A0,(void*)&behind);patch(0x4178E0,(void*)&angle);patch(0x4C6AA0,(void*)&effective);patch(0x4583F0,(void*)&recover);patch(0x529324,(void*)&arc);patch(0x529A44,(void*)&square);patch(0x4B70D5,(void*)&finish);for(int i=0;i<4096;i++){double a=i*0.001536096911877394;at<short>((void*)0x610C48,i*4)=std::sin(a)*4096.;at<short>((void*)0x610C48,i*4+2)=std::cos(a)*4096.;}
long long v[27];while(std::cin>>v[0]){for(int i=1;i<27;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},controls[0x200]={},camera[0x2A4]={};at<void*>(player,0xE0C)=controls;*reinterpret_cast<void**>(0x56F3B8)=camera;player[0xAD6]=v[0];player[0x8EC]=v[1];player[0x54C]=v[2];player[0x8E8]=v[3];at<unsigned>(player,0xE1C)=v[4];controls[0x50]=v[5];at<void*>(player,0xE70)=v[6]?target:nullptr;at<short>(player,0xEA6)=v[7];controls[0x41]=v[8];controls[0x111]=v[9];player[0x8EA]=v[10];player[0xE2D]=v[11];player[0xE2E]=v[12];at<short>(player,0xE32)=v[13];at<short>(camera,0x23A)=v[14];at<short>(camera,0x236)=v[15];player[0x551]=v[16];at<int>(player,0x56C)=v[17];player[0x201]=v[18];player[0x2D1]=v[19];at<CVector>(player,8)=CVector(v[20],v[21],v[22]);at<CVector>(target,8)=CVector(v[23],v[24],v[25]);heading=v[26];events.clear();step(player);std::cout<<'['<<unsigned(player[0x551])<<','<<at<int>(player,0x56C)<<','<<unsigned(player[0x201])<<','<<unsigned(controls[0x41])<<','<<unsigned(controls[0x111])<<','<<unsigned(player[0x2D1])<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    base=[1,0,0,0,16,0,0,0,0,0,0,-127,0,0,1234,2345,123,321,111,222,12345,-23456,34567,-56789,67890,-78901,333]
    cases=[]
    for input_heading in [-32768,-3000,-2897,-2896,-2064,-2063,-2048,-2033,-2032,-1200,-1199,-1,0,1,1199,1200,2032,2033,2048,2063,2064,2896,2897,3000,32767]:
        for camera_heading in [0,1234,-32768,32767]:
            case=base.copy();case[13]=input_heading;case[14]=camera_heading;cases.append(case)
    for apply,lock,clear,wall in [(0,0,0,0),(1,1,0,0),(1,0,1,0),(1,0,0,1),(1,0,0,0)]:
        for action in [1,16,0x200,0x400,0x1000,0x2000,0x3000,0x4000,0x40000,0x20000000]:
            for target_held,target in [(0,0),(1,0),(1,1)]:
                for ea6,latch,web in [(0,0,0),(0,1,0),(0,0,1),(1,0,0),(1,1,1)]:
                    case=base.copy();case[:5]=[apply,lock,clear,wall,action];case[5:10]=[target_held,target,ea6,latch,web];cases.append(case)
    rng=random.Random(0x4B6E8C)
    for _ in range(400):
        case=base.copy();case[:4]=[rng.choice([0,0,1,2]) for _ in range(4)];case[4]=rng.choice([1,16,0x100,0x200,0x400,0x3000,0x40000,0x20000000]);case[5:11]=[rng.randrange(2),rng.randrange(2),rng.choice([0,0,1,-1]),rng.randrange(2),rng.randrange(2),rng.randrange(2)];case[11:13]=[rng.randint(-127,127),rng.randint(-127,127)];case[13:20]=[rng.randint(-32768,32767),rng.randint(-32768,32767),rng.randint(-32768,32767),rng.randrange(256),rng.randint(-2147483648,2147483647),rng.randrange(256),rng.randrange(256)];case[20:26]=[rng.randint(-10000000,10000000) for _ in range(6)];cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-facing-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-camera-facing.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} complete original camera-facing slices, genuine Utils_CalcAim math, source201/551/56C and17/4 edges plus exact heading durations.')


if __name__=='__main__':main()
