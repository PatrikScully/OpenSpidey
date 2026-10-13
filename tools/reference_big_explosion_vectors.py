"""Compile Exp_Big3DExplosion/CWibbling/C3DExplosion; optionally validate original PC emission and constructor/destructor counters."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    exp=(root/'src/game/effects/exp.cpp').read_text();utils=(root/'src/core/utils.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cstdlib>
#include <cstring>
#include <new>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
int G_RND_RELATED_ONE,G_RND_RELATED_TWO,G_RND_RELATED_THREE,G_WIBBLING_EXP_COUNT=0;
std::vector<long long>events;int mask=511,allocationIndex=0;void print_if_false(bool,const char*){}
'''
    code+=controller_method(utils,'int Rnd(i32 n)').replace('int Rnd(','int OriginalRnd(',1)+'\n'
    code+=r'''
int Rnd(int range){int result=OriginalRnd(range);events.insert(events.end(),{1,range,result});return result;}
void*allocate(size_t size){bool accepted=(mask&(1<<allocationIndex))!=0;events.insert(events.end(),{2,allocationIndex++,accepted});return accepted?std::calloc(1,size):nullptr;}
void*G_BULLET_LIST=nullptr;
struct C3DExplosion{void*originalVtable=nullptr;CVector mPos,mVel;CSVector mScale;u16 mFlags=0,mModel=0;u32 mRGB=0;u8 field_F8=0;int field_FC=0,field_100=0,field_104=0,field_108=0,field_10C=0,field_110=0;bool dead=false;
 C3DExplosion(const CVector*,char*,int,int,int,int,int,int,int,int,int);void AI();void AttachTo(void*){}void InitItem(char*){}void Die(){dead=true;}};
struct CWibbling3DExplosion:C3DExplosion{CWibbling3DExplosion(const CVector*,char*,int,int,int,int,int,int,int,int,int);~CWibbling3DExplosion();static void*operator new(size_t n)noexcept{return allocate(n);}static void operator delete(void*p){std::free(p);}};
std::vector<C3DExplosion*>created;
void GetRandomPosition(const CVector*,CVector*,int);
'''
    ctor=controller_method(exp,'C3DExplosion::C3DExplosion(')
    opening=ctor.index('{');ctor=ctor[:opening+1]+'\n events.insert(events.end(),{3,a2->vx,a2->vy,a2->vz,a4,a5,a6,a7,a8,a9,a10,a11,a12});\n'+ctor[opening+1:]
    ctor=ctor[:-1]+'created.insert(created.begin(),this);\n}'
    code+=ctor+'\n'
    for sig in ['void C3DExplosion::AI(',' CWibbling3DExplosion::CWibbling3DExplosion(','CWibbling3DExplosion::~CWibbling3DExplosion(','void GetRandomPosition(','void Exp_Big3DExplosion(']:code+=controller_method(exp,sig).replace('"expgrnd"','const_cast<char*>("expgrnd")')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patch(unsigned addr,void*function){auto p=reinterpret_cast<unsigned char*>(addr);*p=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-addr-5;}
void*__attribute__((thiscall)) modelBoundary(void*self,const CVector*pos,char*name,int a,int b,int c,int d,int e,int f,int g,int h,int i){return ::new(self)C3DExplosion(pos,name,a,b,c,d,e,f,g,h,i);}
void __attribute__((thiscall)) baseDestructor(void*){}
void __attribute__((thiscall)) removeBody(void*,void*){}
#endif
void eventJson(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
void modelJson(C3DExplosion*p,int phase){std::cout<<'['<<p->mPos.vx<<','<<p->mPos.vy<<','<<p->mPos.vz<<','<<p->mScale.vx<<','<<p->mScale.vy<<','<<p->mScale.vz<<','<<p->mVel.vy<<','<<p->mRGB<<','<<p->mFlags<<','<<int(p->field_F8)<<','<<p->field_FC<<','<<p->field_100<<','<<p->dead<<','<<p->mModel<<','<<phase<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patch(0x455390,reinterpret_cast<void*>(&allocate));patch(0x43CBF0,reinterpret_cast<void*>(&modelBoundary));patch(0x43CD30,reinterpret_cast<void*>(&baseDestructor));patch(0x460200,reinterpret_cast<void*>(&baseDestructor));patch(0x460280,reinterpret_cast<void*>(&removeBody));
// Omit only Windows SEH registration/restoration; numerical factory/derived counter instructions remain original.
for(auto p:std::vector<std::pair<unsigned,int>>{{0x43CF97,6},{0x43CF9E,7},{0x43D47F,7},{0x43CF27,6},{0x43CF2E,7},{0x43CF7E,7}})std::memset(reinterpret_cast<void*>(p.first),0x90,p.second);
#endif
CVector pos;int count,disposeFrame;while(std::cin>>pos.vx>>pos.vy>>pos.vz>>mask>>G_RND_RELATED_ONE>>G_RND_RELATED_TWO>>G_RND_RELATED_THREE>>count>>disposeFrame){int seedA=G_RND_RELATED_ONE,seedB=G_RND_RELATED_TWO,seedC=G_RND_RELATED_THREE;G_WIBBLING_EXP_COUNT=count;allocationIndex=0;created.clear();events.clear();Exp_Big3DExplosion(&pos);auto sourceEvents=events;int expectedCount=G_WIBBLING_EXP_COUNT;int expectedA=G_RND_RELATED_ONE,expectedB=G_RND_RELATED_TWO,expectedC=G_RND_RELATED_THREE;
#ifdef VALIDATE_ORIGINAL
for(C3DExplosion*p:created)delete static_cast<CWibbling3DExplosion*>(p);created.clear();allocationIndex=0;G_RND_RELATED_ONE=seedA;G_RND_RELATED_TWO=seedB;G_RND_RELATED_THREE=seedC;*reinterpret_cast<int*>(0x5FA954)=count;events.clear();reinterpret_cast<void(*)(const CVector*)>(0x43CF90)(&pos);assert(events==sourceEvents&&*reinterpret_cast<int*>(0x5FA954)==expectedCount&&G_RND_RELATED_ONE==expectedA&&G_RND_RELATED_TWO==expectedB&&G_RND_RELATED_THREE==expectedC);
// Actual derived destructor decrements global count; base model cleanup is an explicit host boundary.
for(C3DExplosion*p:created)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x43CF20)(p);assert(*reinterpret_cast<int*>(0x5FA954)==count);
G_WIBBLING_EXP_COUNT=expectedCount;
#endif
std::vector<int>phases(created.size(),0);std::cout<<'[';eventJson();std::cout<<",["<<expectedA<<','<<expectedB<<','<<expectedC<<"],[";
for(int frame=0;frame<27;frame++){if(frame==disposeFrame&&frame>0){for(unsigned i=0;i<created.size();i++)if(phases[i]!=4){delete static_cast<CWibbling3DExplosion*>(created[i]);phases[i]=4;}}
else if(frame>0){for(unsigned i=0;i<created.size();i++){if(phases[i]==0){created[i]->AI();if(created[i]->dead)phases[i]=2;}else if(phases[i]==2)phases[i]=3;else if(phases[i]==3){delete static_cast<CWibbling3DExplosion*>(created[i]);phases[i]=4;}}}
if(frame)std::cout<<',';std::cout<<'['<<G_WIBBLING_EXP_COUNT<<",[";bool comma=false;for(unsigned i=0;i<created.size();i++)if(phases[i]!=4){if(comma)std::cout<<',';comma=true;modelJson(created[i],phases[i]);}std::cout<<"]]";}std::cout<<"]]\n";for(unsigned i=0;i<created.size();i++)if(phases[i]!=4)delete static_cast<CWibbling3DExplosion*>(created[i]);}}
'''
    rng=random.Random(0x43cf90);cases=[]
    for mask in [0,1,2,255,256,510,511]:
        for counter in [0,7,2147483647,-2147483648]:cases.append([0,0,0,mask,1,2,3,counter,0])
    for i in range(100):cases.append([*[rng.randint(-2147483648,2147483647) for _ in range(3)],rng.randrange(512),*[rng.randint(-2147483648,2147483647) for _ in range(3)],rng.randint(-2147483648,2147483647),rng.choice([0,0,0,1,5,10,21])])
    with tempfile.TemporaryDirectory(prefix='openspidey-big-explosion-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code);flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-fcheck-new',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else []);lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,row)) for row in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/big-explosion.json').write_text(json.dumps([dict(input=row,expected=json.loads(line)) for row,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n');print(f'Generated {len(cases)} source nine-model sequences; original PC factory/derived counts={bool(args.original_exe)}, allocation failures preserve placement RNG.')

if __name__=='__main__':main()
