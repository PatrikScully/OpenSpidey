"""Compile actual player impact emission and original PCGfx two-pixel line quad geometry."""
import json
import argparse
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root/'src/game/player/spidey.cpp').read_text()
    bits = (root/'src/engine/world/bit.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#include <unordered_map>
#endif
std::vector<long long>events;unsigned rng=0,G_TIMER_RELATED=0;
void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
int Rnd(int bound){rng=rng*1664525u+1013904223u;int result=bound?rng%unsigned(bound):0;emit({1,bound,result});return result;}
int Web_GetGroundY(CVector*p){emit({3,p->vx,p->vy,p->vz});return-12345;}
struct CBouncingRock{CBouncingRock(CVector*p,int ground,unsigned selector){emit({4,p->vx,p->vy,p->vz,ground,selector});Rnd(200);Rnd(4096);Rnd(10);Rnd(20);Rnd(2);}};
struct CCombatImpactRing{CVector mPos;short mAngle=0,mScale=0;int field_68=0,field_6C=0;unsigned field_70=0,mPostScale=0,mCodeBGR=0x2C808080;unsigned short mFrigDeltaZ=0;
 void SetTint(int r,int g,int b){mCodeBGR=(mCodeBGR&0xff000000)|r|(g<<8)|(b<<16);}void SetAnim(int){}void SetSemiTransparent(){mCodeBGR|=0x2000000;}
 CCombatImpactRing(CVector*,u8,u8,u8,int,int,int);
};
struct CGLineParticle{CVector mStart,mEnd,mVel,mAcc;unsigned short mLifetime=0;unsigned field_5C=0,mCodeBGR0=0x50808080,mPadBGR1=0x55808080;
 CGLineParticle(CVector&,CVector&,u16,int);void SetRGB0(int r,int g,int b){mCodeBGR0=(mCodeBGR0&0xff000000)|r|(g<<8)|(b<<16);}void SetRGB1(int r,int g,int b){mPadBGR1=r|(g<<8)|(b<<16);}
};
struct CPlayer{unsigned char raw[0x600]={};char&field_5E8=*reinterpret_cast<char*>(raw+0x5E8);int&field_5AC=*reinterpret_cast<int*>(raw+0x5AC);void CreateCombatImpactEffect(CVector*,int);};
'''
    ring = controller_method(bits,'CCombatImpactRing::CCombatImpactRing(')
    ring = ring.replace('this->SetSemiTransparent();', 'this->SetSemiTransparent();emit({2,mPos.vx,mPos.vy,mPos.vz,mAngle,mScale,field_68,field_6C,field_70,mPostScale,mCodeBGR,mFrigDeltaZ});')
    code += ring + '\n' + controller_method((root/'src/engine/world/bit2.cpp').read_text(),'CGLineParticle::CGLineParticle(') + '\n'
    factory = controller_method(source,'void CPlayer::CreateCombatImpactEffect(')
    factory = factory.replace('pLine->mCodeBGR0 |= 0x2000000;', 'pLine->mCodeBGR0 |= 0x2000000;emit({5,pLine->mStart.vx,pLine->mStart.vy,pLine->mStart.vz,pLine->mVel.vx,pLine->mVel.vy,pLine->mVel.vz,pLine->mLifetime,pLine->mAcc.vy,pLine->mCodeBGR0,pLine->mPadBGR1,pLine->field_5C});')
    code += factory + '\n'
    code += r'''
#ifdef VALIDATE_ORIGINAL
std::vector<void*>allocations;std::unordered_map<void*,CGLineParticle*>originalLines;
void*allocate(int size){void*p=std::calloc(1,size);allocations.push_back(p);return p;}
void makeRocks(CVector*p,unsigned selector){int ground=Web_GetGroundY(p);for(int i=0;i<10;i++)new CBouncingRock(p,ground,selector);}
void*__attribute__((thiscall))ringBoundary(void*raw,CVector*p,u8 red,u8 green,u8 blue,int initial,int maximum,int growth){new CCombatImpactRing(p,red,green,blue,initial,maximum,growth);return raw;}
void*__attribute__((thiscall))lineBoundary(void*raw,CVector*p,CVector*velocity,u16 lifetime,int acceleration){originalLines[raw]=new CGLineParticle(*p,*velocity,lifetime,acceleration);return raw;}
void __attribute__((thiscall))rgb0(void*raw,u8 red,u8 green,u8 blue){originalLines.at(raw)->SetRGB0(red,green,blue);*reinterpret_cast<unsigned*>(static_cast<u8*>(raw)+0x3C)=originalLines.at(raw)->mCodeBGR0;}
void __attribute__((thiscall))rgb1(void*raw,u8 red,u8 green,u8 blue){auto p=originalLines.at(raw);p->SetRGB1(red,green,blue);*reinterpret_cast<unsigned*>(static_cast<u8*>(raw)+0x40)=p->mPadBGR1;
 // The next original instruction ORs the semi-transparent bit; validate that real write after return.
 emit({5,p->mStart.vx,p->mStart.vy,p->mStart.vz,p->mVel.vx,p->mVel.vy,p->mVel.vz,p->mLifetime,p->mAcc.vy,p->mCodeBGR0|0x02000000,p->mPadBGR1,p->field_5C});}
void patch(unsigned address,void*function){u8*p=reinterpret_cast<u8*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void setupOriginal(const char*path){original_executable::loadOriginalImage(path);patch(0x4088A0,reinterpret_cast<void*>(&allocate));patch(0x43B740,reinterpret_cast<void*>(&makeRocks));patch(0x40E530,reinterpret_cast<void*>(&ringBoundary));patch(0x412D40,reinterpret_cast<void*>(&lineBoundary));patch(0x412CF0,reinterpret_cast<void*>(&rgb0));patch(0x412D20,reinterpret_cast<void*>(&rgb1));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
 // Remove only Linux-incompatible Windows FS exception registration; preserve original stack layout.
 auto p=reinterpret_cast<u8*>(0x4C6E10);assert(p[0]==0x64&&p[1]==0xA1);p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);
 for(unsigned address:{0x4C6E24u,0x4C70DDu}){p=reinterpret_cast<u8*>(address);assert(p[0]==0x64&&p[1]==0x89);std::memset(p,0x90,7);}
}
void verifyOriginal(CPlayer&player,CVector position,int kind,unsigned seed,std::vector<long long>expected){events.clear();rng=seed;
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,int)>(0x4C6E10)(&player,&position,kind);
 assert(events==expected);for(auto [raw,line]:originalLines){assert(*reinterpret_cast<unsigned*>(static_cast<u8*>(raw)+0x3C)==0x520000A0);assert(*reinterpret_cast<unsigned*>(static_cast<u8*>(raw)+0x40)==0x20);delete line;}originalLines.clear();for(void*p:allocations)std::free(p);allocations.clear();events=expected;
}
#endif
'''
    pc_source = (root/'src/engine/rendering/PCGfx.cpp').read_text()
    quad = controller_method(pc_source, 'void PCGfx_DrawLine(')
    quad = quad[:quad.index('\tf32 zBias1')] + r'''
 std::cout<<'['<<base1X+perpX<<','<<base1Y+perpY<<','<<base2X+perpX<<','<<base2Y+perpY<<','<<base2X-perpX<<','<<base2Y-perpY<<','<<base1X-perpX<<','<<base1Y-perpY<<']';}
'''
    code += quad + r'''
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);setupOriginal(argv[1]);
#endif
 std::cout.precision(10);int mode;while(std::cin>>mode){if(mode==1){float x,y,a,b;std::cin>>x>>y>>a>>b;PCGfx_DrawLine(x,y,0,0,a,b,0,0,2);std::cout<<'\n';continue;}
 int fire,rocks,anim,kind;CVector position;unsigned clock;std::cin>>fire>>rocks>>anim>>kind>>position.vx>>position.vy>>position.vz>>clock>>rng;unsigned seed=rng;CPlayer player;player.field_5E8=fire;player.field_5AC=rocks;*reinterpret_cast<unsigned short*>(player.raw+0x12A)=anim;G_TIMER_RELATED=clock;events.clear();player.CreateCombatImpactEffect(&position,kind);
#ifdef VALIDATE_ORIGINAL
 verifyOriginal(player,position,kind,seed,events);
#endif
 std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";}}
'''
    cases=[]
    for fire in [0,1,-1,256]:
        for rocks in [0,1,-1]:
            for anim in [99,100,101,102,104,106,107]:
                for kind in [-1,0,1,2,3]:
                    cases.append([0,fire,rocks,anim,kind,-123456,987654,-345678,0xfffffffe,12345])
    rng_py=random.Random(0x4C6E10)
    for _ in range(80):cases.append([0,0,0,100,rng_py.choice([0,1,2]),*[rng_py.randint(-2147483648,2147483647) for _ in range(3)],rng_py.randrange(2**32),rng_py.randrange(2**32)])
    for row in [[0,0,0,0],[0,0,10,0],[0,0,0,10],[10,10,-10,-10],[10,0,0,10],[100.25,-30.5,100.25,-30.5]]:
        cases.append([1,*row])
    for _ in range(64):cases.append([1,*[rng_py.randint(-1024,1024)+rng_py.choice([0,.125,.5]) for _ in range(4)]])
    with tempfile.TemporaryDirectory(prefix='player-impact-source-') as temporary:
        directory=Path(temporary);(directory/'reference.cpp').write_text(code)
        flags=['-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-mfpmath=sse','-msse2',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(directory/'reference.cpp'),'-o',str(directory/'reference')],check=True)
        command=[str(directory/'reference')]+([str(args.original_exe.resolve())] if args.original_exe else [])
        output=subprocess.run(command,input='\n'.join(' '.join(map(str,row)) for row in cases),text=True,capture_output=True,check=True).stdout.splitlines()
    destination=root/'modern/OpenSpidey.Tests/Fixtures/player-impact.json'
    destination.write_text(json.dumps({'scope':'Compiled PC-corrected CPlayer impact factory, CCombatImpactRing constructor and CGLineParticle constructor. Bonus rock callback consumes its exact five source construction RNG boundaries; source PCGfx_DrawLine geometric prefix.',
        'originalPcEmissionVerified':bool(args.original_exe),'cases':[{'input':row,'events':json.loads(out)} for row,out in zip(cases,output,strict=True)]},separators=(',',':'))+'\n')
    print(f'Compiled original player impact requests/quad geometry: {len(cases)} comparisons.')


if __name__=='__main__':main()
