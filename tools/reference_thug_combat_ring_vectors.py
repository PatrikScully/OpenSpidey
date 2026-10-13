"""Compile CThug combat-ring requests and original clock/short growth; compare PC0x40E730 updates."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;bits=(root/'src/engine/world/bit.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
int G_TIMER_RELATED=0;unsigned randomState;std::vector<int>events;
int Rnd(int n){events.push_back(n);randomState=randomState*1664525u+1013904223u;return n?randomState%unsigned(n):0;}
struct CCombatImpactRing;std::vector<CCombatImpactRing*>rings;
struct CCombatImpactRing{CVector mPos;short mAngle=0,mScale=0;int field_68=0,field_6C=0,field_70=0;u32 mPostScale=0x10001000,mCodeBGR=0x2C808080;u16 mFrigDeltaZ=0;int frame=0,anim=-1;bool dead=false;
 CCombatImpactRing(CVector*,u8,u8,u8,int,int,int);void Move();void Die(){dead=true;}void SetTint(int r,int g,int b){mCodeBGR=(mCodeBGR&0xff000000)|r|(g<<8)|(b<<16);}void SetAnim(int a){anim=a;frame=0;}void SetSemiTransparent(){mCodeBGR|=0x2000000;}};
struct CThug{int mType;void CreateCombatImpactEffect(CVector*,int);};
'''
    code+=controller_method(bits,'void CCombatImpactRing::Move(')+'\n'
    code+=controller_method(bits,'CCombatImpactRing::CCombatImpactRing(').replace('this->mPos = *a2;', 'rings.push_back(this);this->mPos = *a2;')+'\n'
    code+=controller_method((root/'src/game/actors/thug.cpp').read_text(),'void CThug::CreateCombatImpactEffect(')+'\n'
    projection=bits[bits.index('\t\tif (pBit->mAngle != 0)',bits.index('EXPORT void DisplayFlatBitList')):bits.index('\t\tu16 clut = pTexture->clut;',bits.index('EXPORT void DisplayFlatBitList'))]
    code+='\nstruct AnglePair{short sin,cos;};AnglePair G_RCOSSIN_TBL[4096];struct ProjectionBit{short mAngle;};\n'
    code+='void project(int angle,int scale,int centerX,int centerY){ProjectionBit bit{short(angle)},*pBit=&bit;int halfW=scale,halfH=(scale*2560)>>12,offX=-16,offY=-16,width=32,height=32;bool highPrecision=false;int outOfRange=0;short cx[4],cy[4];int packedCenter=(centerX&0xffff)|(centerY<<16);\n'+projection+'\nstd::cout<<\'[\';for(int i=0;i<4;i++){if(i)std::cout<<\',\';std::cout<<\'[\'<<cx[i]<<\',\'<<cy[i]<<\']\';}std::cout<<\"]\\n\";}\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void noAssert(int,const char*){}
#endif
void emit(CCombatImpactRing*p){std::cout<<'['<<p->mPos.vx<<','<<p->mPos.vy<<','<<p->mPos.vz<<','<<p->mAngle<<','<<p->mScale<<','<<p->field_68<<','<<p->field_6C<<','<<unsigned(p->field_70)<<','<<p->mPostScale<<','<<p->mCodeBGR<<','<<p->mFrigDeltaZ<<','<<p->anim<<','<<p->frame<<','<<p->dead<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4015B0,reinterpret_cast<void*>(&noAssert));
#endif
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.0;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.0;}
int mode;while(std::cin>>mode){if(mode==2){int angle,scale,depth,cx,cy;std::cin>>angle>>scale>>depth>>cx>>cy;project(angle,(unsigned(scale)<<7)/unsigned(depth),cx,cy);continue;}
rings.clear();events.clear();CVector position;unsigned clock;int type,r,g,b,initial,maximum,growth,flags;std::cin>>type>>position.vx>>position.vy>>position.vz>>r>>g>>b>>initial>>maximum>>growth>>clock>>randomState>>flags;G_TIMER_RELATED=clock;
if(mode==0){CThug t{type};t.CreateCombatImpactEffect(&position,flags);}else new CCombatImpactRing(&position,r,g,b,initial,maximum,growth);
unsigned clocks[18];for(auto&value:clocks)std::cin>>value;std::cout<<'[';for(unsigned i=0;i<rings.size();i++){if(i)std::cout<<',';auto p=rings[i];std::cout<<'[';emit(p);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x74]={};*reinterpret_cast<short*>(raw+0x56)=p->mScale;*reinterpret_cast<int*>(raw+0x68)=p->field_68;*reinterpret_cast<int*>(raw+0x6C)=p->field_6C;*reinterpret_cast<int*>(raw+0x70)=p->field_70;
#endif
for(auto clock:clocks){G_TIMER_RELATED=clock;if(!p->dead)p->Move();std::cout<<',';emit(p);
#ifdef VALIDATE_ORIGINAL
*reinterpret_cast<unsigned*>(0x6B4CA8)=clock;if(!raw[0x37])reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x40E730)(raw);
assert(*reinterpret_cast<short*>(raw+0x56)==p->mScale&&*reinterpret_cast<int*>(raw+0x70)==p->field_70&&bool(raw[0x37])==p->dead);
#endif
}std::cout<<']';delete p;}std::cout<<"]\n";
}}
'''
    rng=random.Random(0x40E730);cases=[]
    for _ in range(256):
        clock=rng.choice([0,0xfffffffc,rng.randrange(2**32)])
        case=[rng.randrange(2),rng.choice([304,312,999]),*[rng.randint(-2147483648,2147483647) for _ in range(3)],*[rng.randrange(256) for _ in range(3)],
              rng.choice([-32768,-1,0,192,384,32767,32768]),rng.choice([-32768,-1,0,896,1792,32767,32768]),rng.choice([-2147483648,-32768,-1,0,72,144,32767,2147483647]),clock,rng.randrange(2**32),rng.randint(-2147483648,2147483647)]
        for frame in range(18):clock=(clock+rng.choice([0,1,2,100,65535,2147483648]))&0xffffffff;case.append(clock)
        cases.append(case)
    for kind in [304,312]:cases.append([0,kind,0,0,0,0,0,0,384,1792,144,0,0,0,*range(2,38,2)])
    for angle in range(0,4096,8):cases.append([2,angle,rng.choice([192,384,896,1792]),rng.choice([1000,2000,4000]),256,120])
    with tempfile.TemporaryDirectory(prefix='openspidey-combat-ring-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-combat-ring.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} combat ring cases; original PC clock/short/lifetime checked when requested.')

if __name__=='__main__':main()
