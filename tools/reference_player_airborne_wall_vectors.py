"""Capture whole original CheckStickToWall4BFEC0 with genuine ground-height/orientation/animation boundaries."""
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
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;int ground;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int height(CVector*p,int above,int below,void*body){assert(!body);events.insert(events.end(),{1,p->vx,p->vy,p->vz,above,below});return ground;}
void __attribute__((thiscall)) orient(void*,bool targeted,CVector*p){events.insert(events.end(),{2,targeted,p->vx,p->vy,p->vz});}
void __attribute__((thiscall)) animation(void*p,int clip,int first,int last){events.insert(events.end(),{3,clip,first,last});at<short>(p,0x12A)=clip;}
void sound(unsigned id,short volume,int pitch){events.insert(events.end(),{4,id,volume,pitch});}
void noAssert(int,const char*){}
void vector(void*p,unsigned off){auto&v=at<CVector>(p,off);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
void shortVector(void*p,unsigned off){for(unsigned component=0;component<3;component++)std::cout<<','<<at<short>(p,off+component*2);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4E6840,(void*)&height);patch(0x4C4BB0,(void*)&orient);patch(0x460C10,(void*)&animation);patch(0x471C50,(void*)&sound);patch(0x4015B0,(void*)&noAssert);long long v[19];while(std::cin>>v[0]){for(int i=1;i<19;i++)std::cin>>v[i];alignas(4)unsigned char player[0xF20]={};unsigned face[4]={0,0,0,static_cast<unsigned>(v[9])};at<unsigned>(player,0xE1C)=v[0];at<unsigned short>(player,0xE0)=v[1];at<void*>(player,0xB74)=v[2]?reinterpret_cast<void*>(1):nullptr;at<CVector>(player,0xB78)=CVector(v[3],v[4],v[5]);at<short>(player,0xB84)=v[6];at<short>(player,0xB86)=v[7];at<short>(player,0xB88)=v[8];at<void*>(player,0xB8C)=face;ground=v[10];at<unsigned short>(player,0xEA8)=v[11];at<short>(player,0x12A)=v[12];at<CVector>(player,8)=CVector(v[13],v[14],v[15]);at<CVector>(player,0x60)=CVector(123,-456,789);at<short>(player,0xA8)=111;at<short>(player,0xAA)=-222;at<short>(player,0xAC)=333;player[0xAD4]=v[16];at<int>(player,0xDF8)=v[17];events.clear();int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(0x4BFEC0)(player);std::cout<<'['<<result<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<unsigned(player[0xAD4])<<','<<at<int>(player,0xDF8);for(unsigned off:{8u,0x60u})vector(player,off);shortVector(player,0xA8);std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    base=[4,1,1,123456,-234567,345678,3500,0,-1000,0,-1,96,232,12345,-23456,34567,0,88,0]
    cases=[]
    for action in [1,2,4,0x40004]:
        for collision in [0,1,2,3]:
            for item in [0,1]:
                for normal_y in [-2601,-2600,0,3400,3401]:
                    for flags in [0,0x40000]:
                        for height in [-1,0]:
                            case=base.copy();case[:3]=[action,collision,item];case[7]=normal_y;case[9]=flags;case[10]=height;cases.append(case)
    rng=random.Random(0x4BFEC0)
    for _ in range(200):
        case=base.copy();case[3:6]=[rng.randint(-2147483648,2147483647) for _ in range(3)];case[6:9]=[rng.randint(-32768,32767) for _ in range(3)];case[11]=rng.randrange(65536);case[12]=rng.choice([0,231,232,276]);case[13:16]=[rng.randint(-2147483648,2147483647) for _ in range(3)];case[16]=rng.randrange(2);cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-air-wall-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,row)) for row in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-airborne-wall.json').write_text(json.dumps([dict(input=row,expected=json.loads(line))for row,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} complete original airborne-wall gates, short normals, unsignedclearance, exactgroundsentinel andsourcewrites/callbackorder.')


if __name__=='__main__':main()
