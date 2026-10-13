"""Compile actual CItemFrag ctor/Move and verify every written motion/quad/RNG field against PC instructions."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent;source=(root/'src/game/effects/exp.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct CBit{unsigned vtable=0,previous=0,next=0;short mAge=0;unsigned short mLifetime=0;CVector mPos,mVel,mAcc;unsigned char friction[3]={1,1,1},dead=0;unsigned short depth=0;unsigned char protectedBit=0,mType=0;void Die(){dead=1;}};
struct CQuadBit:CBit{CVector mPosB,mPosC,mPosD;int texture=0;unsigned mCodeBGR=0x1C0001,field_68=0x2030001,mTint=0x808080,field_70=0,field_74=0,field_78=0,field_7C=0,field_80=0;void SetSemiTransparent(){mCodeBGR|=0x2C0;}};
struct CItemFrag:CQuadBit{int field_84=0;CVector field_88,field_94,field_A0,field_AC;CItemFrag(u32*,CVector*,CVector*,int);void Move();};static_assert(sizeof(CItemFrag)==0xB8);
int cursor;std::vector<int>events;int Rnd(int bound){int value=(cursor++*7+1)%bound;events.insert(events.end(),{bound,value});return value;}
'''+controller_method(source,'CItemFrag::CItemFrag(')+'\n'+controller_method(source,'void CItemFrag::Move(')+r'''
void vec(CVector p){std::cout<<p.vx<<','<<p.vy<<','<<p.vz;}
void output(CItemFrag&s){std::cout<<'[';vec(s.field_88);std::cout<<',';vec(s.field_94);std::cout<<',';vec(s.field_A0);std::cout<<',';vec(s.field_AC);std::cout<<',';vec(s.mPos);std::cout<<',';vec(s.mPosB);std::cout<<',';vec(s.mPosC);std::cout<<',';vec(s.mPosD);std::cout<<',';vec(s.mVel);std::cout<<','<<s.field_84<<','<<s.mAge<<','<<s.mLifetime<<','<<s.mTint<<','<<int(s.mType)<<','<<int(s.dead)<<','<<s.mCodeBGR<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
std::string capture(CItemFrag&s){std::ostringstream t;auto*old=std::cout.rdbuf(t.rdbuf());output(s);std::cout.rdbuf(old);events.clear();return t.str();}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned a,void*t){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(t)-a-5;}
void*__attribute__((thiscall)) quadCtor(void*p){*static_cast<CQuadBit*>(p)=CQuadBit();return p;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x408FA0,reinterpret_cast<void*>(&quadCtor));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x43C827,6},{0x43C82E,7},{0x43C9AF,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
#endif
CVector vertices[4],velocity;int ground,seed;unsigned face[8]={0,0x03020100,0x808080,0x12345678,0x98765432,0x11112222,0x33334444,0x55556666};
while(std::cin>>vertices[0].vx>>vertices[0].vy>>vertices[0].vz>>vertices[1].vx>>vertices[1].vy>>vertices[1].vz>>vertices[2].vx>>vertices[2].vy>>vertices[2].vz>>vertices[3].vx>>vertices[3].vy>>vertices[3].vz>>velocity.vx>>velocity.vy>>velocity.vz>>ground>>seed){events.clear();cursor=seed;CItemFrag source(face,vertices,&velocity,ground);std::vector<std::string>states{capture(source)};for(int frame=1;frame<=34;frame++){if(!source.dead)source.Move();states.push_back(capture(source));}
#ifdef VALIDATE_ORIGINAL
events.clear();cursor=seed;alignas(4)unsigned char raw[0xB8]={};auto&actual=*reinterpret_cast<CItemFrag*>(raw);reinterpret_cast<void(__attribute__((thiscall)) *)(void*,unsigned*,CVector*,CVector*,int)>(0x43C820)(raw,face,vertices,&velocity,ground);
auto compare=[&](int frame){auto value=capture(actual);if(value!=states[frame]){std::cerr<<"ItemFrag frame "<<frame<<"\nsource="<<states[frame]<<"\nactual="<<value<<'\n';abort();}};compare(0);assert(actual.field_70==face[3]&&actual.field_74==face[4]&&actual.field_78==face[5]&&actual.field_7C==face[6]&&actual.field_80==face[7]);for(int frame=1;frame<=34;frame++){if(!actual.dead)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x43C9F0)(raw);compare(frame);}
#endif
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";}}
'''
    rng=random.Random(0x43C9F0);cases=[]
    for ground in [-54322,-54321,-54320,200000]:
        for velocity in [(0,0,0),(4096,-4096,-8192),(-4096,40000,8192)]:
            for seed in [0,1,73]:cases.append([12345,-54321,67890,22345,-44321,87890,32345,-64321,57890,42345,-74321,47890,*velocity,ground,seed])
    for _ in range(28):cases.append([*[rng.randint(-2147483648,2147483647) for _ in range(16)],rng.randrange(4096)])
    with tempfile.TemporaryDirectory(prefix='openspidey-item-fragment-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        flags=['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',*flags,
            '-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/item-burst-fragments.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} CItemFrag ctor/34Move cases; fulloriginalPCmotion/quad/RNG verified={bool(args.original_exe)}.')


if __name__=='__main__':main()
