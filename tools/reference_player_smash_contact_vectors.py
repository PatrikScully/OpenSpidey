"""Capture exact original PC smash post-movement contact slice, source type table and SHitInfo boundary."""
import argparse
import json
from pathlib import Path
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
std::vector<long long>events;unsigned char bodies[2][0x100];int contacts,callCount,damage;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void vec(CVector&v){events.insert(events.end(),{v.vx,v.vy,v.vz});}
int identity(void*p){return p==bodies[0]?1:p==bodies[1]?2:0;}
void*trace(CVector*start,CVector*end,CVector*out,void*,void*ignore,int scale){events.push_back(1);vec(*start);vec(*end);events.insert(events.end(),{identity(ignore),scale});if(callCount>=contacts)return nullptr;*out=*start+CVector(111,222,333);return bodies[callCount++];}
int __attribute__((thiscall)) getDamage(void*,int base){events.insert(events.end(),{2,base});return damage;}
int __attribute__((thiscall)) hit(void*p,void*h){events.insert(events.end(),{3,identity(p),at<unsigned char>(h,0),at<int>(h,4),at<unsigned short>(h,8)});vec(at<CVector>(h,12));events.insert(events.end(),{at<short>(h,24),at<unsigned short>(h,26)});return 0;}
int sound(unsigned id,CVector*pos,int pitch){events.insert(events.end(),{4,id});vec(*pos);events.push_back(pitch);return 77;}
extern "C" __attribute__((naked)) void contactEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void contactStep(void*){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;lea 0x10(%esp),%edi;mov $0x12C,%ecx;xor %eax,%eax;rep stosl;mov 0x4C4(%esp),%esi;xor %ebx,%ebx;xor %ebp,%ebp;mov $0x4B78A8,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x452C30,(void*)&trace);patch(0x4BD830,(void*)&getDamage);patch(0x471EA0,(void*)&sound);patch(0x4B7A74,(void*)&contactEnd);unsigned vt[]={0,0,0,reinterpret_cast<unsigned>(&hit)};long long v[18];while(std::cin>>v[0]){for(int i=1;i<18;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={};memset(bodies,0,sizeof bodies);for(int i=0;i<2;i++){at<void*>(bodies[i],0)=vt;at<unsigned short>(bodies[i],0x38)=v[2+i];}contacts=v[0];damage=v[1];callCount=0;at<CVector>(player,0xE8)=CVector(v[4],v[5],v[6]);at<CVector>(player,8)=CVector(v[7],v[8],v[9]);at<CVector>(player,0xC6C)=CVector(v[10],v[11],v[12]);at<int>(player,0x528)=v[13];player[0x8D8]=v[14];at<unsigned>(player,0xE1C)=v[15];at<int>(player,0x534)=v[16];at<int>(player,0x52C)=v[17];at<CVector>(player,0x60)=CVector(12345,-23456,34567);events.clear();contactStep(player);std::cout<<'['<<unsigned(player[0x8D8])<<','<<at<int>(player,0x534)<<','<<at<int>(player,0x52C);auto&vel=at<CVector>(player,0x60);std::cout<<','<<vel.vx<<','<<vel.vy<<','<<vel.vz<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[[contacts,damage,first,second,12345,-23456,34567,12345+delta,-23456+delta,34567-delta,forward,77,-4096,scalar,hit,action,88,99]
           for first,second,contacts in [(kind,304,1) for kind in range(302,328)]+[(305,304,2),(315,304,2),(305,315,2),(315,305,2),(304,304,0)]
           for damage,scalar,forward in [(20,0,0),(-1,2147483647,4096),(65539,-2147483648,-12345)]
           for delta,hit,action in [(409600,0,0x1000000),(0,0,0x1000000),(409600,1,0x1000000),(409600,0,0)]]
    with tempfile.TemporaryDirectory(prefix='openspidey-smash-contacts-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-smash-contacts.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original PC smash contact paths, all types302..327 and repeated pass-through with ignored identity.')


if __name__=='__main__':main()
