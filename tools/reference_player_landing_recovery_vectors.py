"""Capture complete original PC source8 landing recovery and64 jump windup at genuine guard/animation boundaries."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;int accepted,randomResult;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int guard(int index){events.insert(events.end(),{1,index});return accepted==index;}
int __attribute__((thiscall)) ground(void*){return guard(1);}int __attribute__((thiscall)) forwards(void*,bool backwards){assert(backwards);return guard(2);}int __attribute__((thiscall)) jump(void*){return guard(3);}int __attribute__((thiscall)) kick(void*){return guard(4);}unsigned char __attribute__((thiscall)) swing(void*){return guard(5);}unsigned char __attribute__((thiscall)) straightZip(void*){return guard(6);}unsigned char __attribute__((thiscall)) swingZip(void*){return guard(7);}
void __attribute__((thiscall)) play(void*p,int clip,int first,int last){events.insert(events.end(),{2,clip,first,last});at<unsigned short>(p,0x12A)=clip;}
void __attribute__((thiscall)) stand(void*){events.push_back(3);}int rnd(int range){assert(range==2);events.insert(events.end(),{4,range,randomResult});return randomResult;}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void leafStep(void*,int,int){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;mov 0x4C8(%esp),%eax;mov %eax,0x20(%esp);xor %ebx,%ebx;cmpl $0,0x4CC(%esp);jne 1f;mov $0x4B2BF3,%eax;jmp *%eax;1:mov $0x4B269B,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C23A0,(void*)&ground);patch(0x4BF8A0,(void*)&forwards);patch(0x4C2090,(void*)&jump);patch(0x4C00B0,(void*)&kick);patch(0x4C18A0,(void*)&swing);patch(0x4C0EE0,(void*)&straightZip);patch(0x4C1460,(void*)&swingZip);patch(0x4C9130,(void*)&play);patch(0x4BE4B0,(void*)&stand);patch(0x4E5DA0,(void*)&rnd);patch(0x4B6E8C,(void*)&leafEnd);
int family;long long v[14];while(std::cin>>family){for(int i=0;i<14;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},controls[0x200]={},support[0x100]={};at<void*>(player,0xE0C)=controls;at<unsigned short>(player,0x12A)=v[0];player[0x142]=v[1];player[0xE8C]=v[2];controls[0x100]=v[3];at<void*>(player,0xDBC)=v[4]?support:nullptr;at<int>(support,0x64)=v[5];at<int>(player,0xE80)=123456;at<int>(player,0xE84)=v[6];at<int>(player,0xE88)=987654;at<unsigned short>(player,0xE0)=v[7];accepted=v[8];player[0xAE4]=v[9];player[0xAE5]=v[10];player[0xE8D]=v[11];at<unsigned>(player,0xE1C)=v[12];randomResult=v[13];at<int>(player,0xEBC)=999;at<short>(player,0xEA6)=456;events.clear();leafStep(player,v[1],family);
std::cout<<'['<<at<short>(player,0xEA6)<<','<<unsigned(player[0xAE4])<<','<<unsigned(player[0xAE5])<<','<<unsigned(player[0xE8C])<<','<<unsigned(player[0xE8D])<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<at<int>(player,0xE80)<<','<<at<int>(player,0xE84)<<','<<at<int>(player,0xE88)<<','<<at<int>(player,0xEBC)<<','<<unsigned(controls[0x100])<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    recovery=[[animation,completed,e8c,held,present,support,phase,collision,accepted,88,99,77,8,0]
              for animation in [178,180,213] for completed in [65535,178,180,213] for e8c in [0,1,255] for held in [0,255]
              for present,support,phase,collision in [(0,0,0x70000,0),(1,-262143,0x70000,1),(1,-262144,1,0),(1,-262145,-1,1),
                                                       (1,-2147483648,-2147483648,1),(1,2147483647,2147483647,0)] for accepted in range(8)]
    windup=[[animation,finished,255,255,present,support,phase,1,0,88,99,77,64,rnd]
            for animation in [0,210,223,211,224,231] for finished in [0,1,255]
            for present,support,phase in [(0,0,0x70000),(1,-262144,0),(1,-262145,-1),(1,-2147483648,2147483647),(1,2147483647,-2147483648)] for rnd in [0,1]]
    rows=[(0,'recovery',case) for case in recovery]+[(1,'windup',case) for case in windup]
    with tempfile.TemporaryDirectory(prefix='openspidey-landing-recovery-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(str(family)+' '+' '.join(map(str,case)) for family,_,case in rows)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-landing-recovery.json').write_text(json.dumps([dict(kind=kind,input=case,expected=json.loads(line)) for (_,kind,case),line in zip(rows,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(recovery)} complete original PC action8 recovery and {len(windup)} action64 windup cases, all guard priority, protected clips and support/impulse boundaries.')


if __name__=='__main__':main()
