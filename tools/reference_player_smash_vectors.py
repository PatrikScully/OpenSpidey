"""Capture the actual PC action1000000 slice at typed pose, animation, stand and trail boundaries."""
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
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) createTrails(void*p){events.push_back(1);at<unsigned>(p,0x584)=at<unsigned>(p,0x588)=1;}
void __attribute__((thiscall)) orient(void*,bool use,CVector*t){events.insert(events.end(),{2,use,t->vx,t->vy,t->vz});}
void __attribute__((thiscall)) animation(void*p,int clip,int first,int last){events.insert(events.end(),{3,clip,first,last});at<unsigned short>(p,0x12A)=clip;}
void __attribute__((thiscall)) fadeTrails(void*p){events.push_back(4);at<unsigned>(p,0x584)=at<unsigned>(p,0x588)=0;}
void __attribute__((thiscall)) stand(void*){events.push_back(5);}
extern "C" __attribute__((naked)) void actionEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void actionStep(void*){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;xor %ebx,%ebx;mov $0x4B631E,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C0D50,(void*)&createTrails);patch(0x4C4BB0,(void*)&orient);patch(0x4C9130,(void*)&animation);patch(0x4C0E60,(void*)&fadeTrails);patch(0x4BE4B0,(void*)&stand);patch(0x4B6E8C,(void*)&actionEnd);
long long v[23];while(std::cin>>v[0]){for(int i=1;i<23;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={};at<CVector>(player,8)=CVector(v[0],v[1],v[2]);at<CVector>(player,0x8CC)=CVector(v[3],v[4],v[5]);at<int>(player,0x8DC)=v[6];at<unsigned short>(player,0x12A)=v[7];player[0x142]=v[8];at<unsigned>(player,0x584)=v[9];at<unsigned>(player,0x588)=v[10];at<unsigned>(player,0x504)=v[11];at<unsigned>(player,0x500)=v[12];*reinterpret_cast<unsigned*>(0x6B4CA8)=v[13];at<unsigned short>(player,0xE0)=v[14];player[0x8D8]=v[15];at<int>(player,0x8C4)=v[16];at<int>(player,0x8C8)=v[17];player[0xAE4]=v[18];player[0xAE5]=v[19];player[0xE8C]=v[20];player[0xE8D]=v[21];at<unsigned>(player,0xE1C)=v[22];at<CVector>(player,0x60)=CVector(12345,-23456,34567);at<short>(player,0xA8)=111;at<short>(player,0xAA)=-222;at<short>(player,0xAC)=333;at<short>(player,0xEA6)=456;
events.clear();actionStep(player);std::cout<<'['<<at<short>(player,0xEA6)<<','<<at<int>(player,0x8DC)<<','<<unsigned(player[0xAE4])<<','<<unsigned(player[0xAE5])<<','<<unsigned(player[0xE8C])<<','<<unsigned(player[0xE8D])<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<(at<unsigned>(player,0x584)?1:0)<<','<<(at<unsigned>(player,0x588)?1:0);
for(unsigned o:{0xA8u,0xAAu,0xACu})std::cout<<','<<at<short>(player,o);auto&vel=at<CVector>(player,0x60);std::cout<<','<<vel.vx<<','<<vel.vy<<','<<vel.vz<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = [[12345, -23456, 34567, 12345+x*4096, -23456+y*4096, 34567+z*4096,
              delay, animation, done, first, 1, last_action, 100, clock, collision, hit, entry, previous, 77, 88, 99, 123, 0x1000000]
             for x,y,z in [(0,100,0),(300,100,-200),(0,0,0),(0,-1,0)]
             for delay,animation,done,first in [(0,129,0,0),(0,129,1,0),(0,133,1,1),(0,17,1,0),(666,0,1,0),(666,130,1,1)]
             for last_action,clock in [(0,100),(0x1000000,105),(0x1000000,106),(0x1000000,99)]
             for collision,hit in [(0,0),(2,0),(0,1)]
             for entry,previous in [(1000,881),(1000,880),(-2147483648,2147483647)]]
    rng=random.Random(0x4B631E)
    cases += [[*[rng.randint(-10000000,10000000) for _ in range(6)],rng.choice([0,0,666]),rng.choice([0,129,133,130]),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.choice([0,0x1000000]),rng.randrange(2**32),rng.randrange(2**32),rng.choice([0,2]),rng.randrange(256),rng.randint(-2147483648,2147483647),rng.randint(-2147483648,2147483647),rng.randrange(256),rng.randrange(256),rng.randrange(256),rng.randrange(256),0x1000000] for _ in range(128)]
    with tempfile.TemporaryDirectory(prefix='openspidey-smash-action-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-smash-action.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original PC smash action slices, exact signed clock branch, normals, velocity and complete boundary order.')


if __name__=='__main__':main()
