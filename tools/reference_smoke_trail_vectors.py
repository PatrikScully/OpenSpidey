"""Capture original PC CSmokeTrail/CRibbon constructors, ring endpoints, animated children and complete fade sequences."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cstdlib>
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<void*>allocations;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void*allocate(unsigned bytes,int a,int b,void*c,bool d){assert(a==0&&b==1&&!c&&d);void*p=calloc(1,bytes);assert(p);allocations.push_back(p);return p;}
void assertion(unsigned char ok,const char*){assert(ok);}
extern "C" __attribute__((naked)) void ribbonCtor(){asm volatile("sub $0x10,%esp;push %ebx;push %ebp;push %esi;mov %ecx,%esi;push %edi;mov $0x410B3C,%eax;jmp *%eax");}
extern "C" __attribute__((naked)) void ribbonEnd(){asm volatile("mov %esi,%eax;pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x10,%esp;ret $0x1C");}
void printVector(CVector&v){std::cout<<v.vx<<','<<v.vy<<','<<v.vz;}
void capture(void*p){int n=at<int>(p,0x3C);void**bits=at<void**>(p,0x50);auto*points=at<CVector*>(p,0x4C);std::cout<<'['<<at<int>(p,0x48)<<','<<at<int>(p,0x54)<<','<<unsigned(at<unsigned char>(p,0x37))<<','<<*reinterpret_cast<unsigned*>(0x56EB48)<<','<<(*reinterpret_cast<void**>(0x56EAD8)==p?1:0)<<",[";
void*linked=*reinterpret_cast<void**>(0x56EB40);bool comma=false;while(linked){int index=-1;for(int i=0;i<n;i++)if(bits[i]==linked)index=i;assert(index>=0);if(comma)std::cout<<',';comma=true;std::cout<<index;linked=at<void*>(linked,8);}std::cout<<"],[";
for(int i=0;i<=n;i++){if(i)std::cout<<',';std::cout<<'[';printVector(points[i]);std::cout<<']';}std::cout<<"],[";
for(int i=0;i<n;i++){if(i)std::cout<<',';void*b=bits[i];std::cout<<'[';printVector(at<CVector>(b,0x58));std::cout<<',';printVector(at<CVector>(b,0x64));std::cout<<','<<unsigned(at<unsigned char>(b,0x50))<<','<<at<unsigned>(b,0x40)<<','<<at<unsigned short>(b,0x56)<<','<<at<short>(b,0x54)<<','<<unsigned(at<unsigned char>(b,0x53))<<','<<int(at<signed char>(b,0x52))<<','<<unsigned(at<unsigned char>(b,0x51))<<','<<at<short>(b,0x3C)<<','<<unsigned(at<unsigned char>(b,0x3A))<<','<<unsigned(at<unsigned char>(b,0x37))<<']';}std::cout<<"]]";}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4581A0,(void*)&allocate);patch(0x4015B0,(void*)&assertion);patch(0x410B20,(void*)&ribbonCtor);patch(0x410D83,(void*)&ribbonEnd);
int n,r,g,b,frameCount,speed,x,y,z,fadeAt;while(std::cin>>n>>r>>g>>b>>frameCount>>speed>>x>>y>>z>>fadeAt){alignas(4)unsigned char trail[0x58]={},anim[4+8*8]={};anim[0]=frameCount;*reinterpret_cast<void**>(0x56EA6C)=anim+4;*reinterpret_cast<unsigned*>(0x56EB48)=0;*reinterpret_cast<void**>(0x56EAD8)=nullptr;*reinterpret_cast<void**>(0x56EB40)=nullptr;CVector pos(x,y,z);
reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,CVector*,int,int,int,int)>(0x410F50)(trail,&pos,n,r,g,b);void**children=at<void**>(trail,0x50);for(int i=0;i<n;i++)at<short>(children[i],0x54)=speed;
std::cout<<'[';capture(trail);for(int tick=0;tick<36;tick++){CVector next(x+tick*12345,y-tick*23456,z+tick*34567);if(tick%3!=2)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*)>(0x410EB0)(trail,&next);if(tick==fadeAt)at<int>(trail,0x54)=1;for(int i=0;i<n;i++)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x410AE0)(children[i]);reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x411100)(trail);std::cout<<',';capture(trail);}std::cout<<"]\n";
for(void*p:allocations)free(p);allocations.clear();}}
'''
    cases=[[n,r,g,b,frames,speed,12345,-23456,34567,fade]
           for n in [1,4,7] for r,g,b in [(0,0,0),(32,32,64),(64,32,32),(255,254,1),(-19,300,1025)]
           for frames,speed in [(1,128),(3,128),(4,-129)] for fade in [0,5]]
    with tempfile.TemporaryDirectory(prefix='openspidey-smoke-trail-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/smoke-trail.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original PC smoke owners: constructors, exact source child list order, 36 ring/frame/fade updates each.')


if __name__=='__main__':main()
