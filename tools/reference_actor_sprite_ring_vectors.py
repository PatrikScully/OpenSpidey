"""Compile actor ring placement/emission and machine-confirmed CMotionBlur lifetime.

Bit_MakeSpriteRing's recovered allocation names CFlatBit. PC0x410951 overwrites its vtable
with0x53B3CC whose Move entry is CMotionBlur0x40FC70, so this isolated reference corrects
that class identity before compiling the original lifetime method.
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_brain_vectors import literal_header


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    baddy=(root/'src/game/actors/baddy.cpp').read_text();bits=(root/'src/engine/world/bit.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=literal_header(root/'tools/reference_actor_body_vectors.py')
    code=code.replace('#include <iostream>','#include <iostream>\n#include <vector>\n#include <cstdlib>\n#ifdef VALIDATE_ORIGINAL\n#include "original_executable.h"\n#endif')
    code=code.replace('void DoPhysics(int);','void DoPhysics(int);void GetLocalPos(CVector*,CVector*,CSVector*);int MakeSpriteRing(CVector*);')
    code+=r'''
struct SSinCos{short sin,cos;};SSinCos G_RCOSSIN_TBL[4096];
int ground=0;std::vector<int>groundEvents;int animationFrames=3;
int Utils_GetGroundHeight(CVector*p,int above,int below,void*){groundEvents.insert(groundEvents.end(),{p->vx,p->vy,p->vz,above,below});return ground;}
struct SAnimFrame{char OffX=0,OffY=0;unsigned char Width=0,Height=0;void*pTexture=nullptr;};SAnimFrame animation[258],*G_ANIM_TABLE[29];
struct CFlatBit;std::vector<CFlatBit*>particles;
enum{NUM_ANIM_ENTRIES=29};
#define ASSERT(value,message) do{if(!(value))std::abort();}while(0)
struct CFT4Bit{CVector mPos,mVel;short mAge=0,mTransDecay=0,mScale=0,mAnimSpeed=0;unsigned short field_3E=0,mFrigDeltaZ=0;
 unsigned mCodeBGR=0;unsigned char mDead=0,mProtected=0,mDeleteAnimOnDestruction=0,mNumFrames=0,mFrameFrac=0; signed char mFrame=0;
 SAnimFrame *mpPSXAnim=nullptr,*mpPSXFrame=nullptr;
 CFT4Bit();void SetAnim(int);void SetScale(unsigned short);void SetSemiTransparent();void SetTransDecay(int);void Die(){mDead=1;}
};
struct CFlatBit:CFT4Bit{short mAngle=0;unsigned short mAngFric=1;unsigned mPostScale=0x10001000;unsigned char mSemiTransparencyRate=32;
 CFlatBit(){particles.push_back(this);} };
struct CMotionBlur:CFlatBit{void Move();};
'''
    for signature in ['MATRIX* RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)', 'MATRIX* M3dMaths_RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)', 'EXPORT void  FixedXForm(']:code+=controller_method(maths,signature)+'\n'
    code+='void gte_rtir(){FixedXForm(rotation.m,&input,&result);}\n'
    for signature in ['CFT4Bit::CFT4Bit(', 'void CFT4Bit::SetAnim(', 'void CFT4Bit::SetScale(', 'void CFT4Bit::SetSemiTransparent(', 'void CFT4Bit::SetTransDecay(', 'void CMotionBlur::Move(']:code+=controller_method(bits,signature)+'\n'
    code+=controller_method(bits,'i32 Bit_MakeSpriteRing(').replace('new CFlatBit()','new CMotionBlur()')+'\n'
    code+=controller_method(baddy,'void CBaddy::GetLocalPos(')+'\n'+controller_method(baddy,'int CBaddy::MakeSpriteRing(')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
std::vector<void*>originalParticles;
void* originalAllocate(int size,int,int,int,int){auto p=std::calloc(1,size);originalParticles.push_back(p);return p;}
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void verify(CFlatBit*p,unsigned char*raw){assert(!std::memcmp(raw+0x10,&p->mPos,12)&&!std::memcmp(raw+0x1C,&p->mVel,12));
 assert(*reinterpret_cast<short*>(raw+0x56)==p->mScale&&*reinterpret_cast<unsigned short*>(raw+0x3E)==p->field_3E);
 assert(*reinterpret_cast<short*>(raw+0x3C)==p->mTransDecay&&*reinterpret_cast<unsigned*>(raw+0x40)==p->mCodeBGR);
 assert(raw[0x37]==p->mDead&&*reinterpret_cast<short*>(raw+0xC)==p->mAge&&static_cast<signed char>(raw[0x52])==p->mFrame);
 assert(*reinterpret_cast<unsigned short*>(raw+0x38)==p->mFrigDeltaZ&&*reinterpret_cast<unsigned*>(raw+0x60)==p->mPostScale&&raw[0x65]==p->mSemiTransparencyRate);}
#endif
void emit(CFlatBit*p){std::cout<<'['<<p->mPos.vx<<','<<p->mPos.vy<<','<<p->mPos.vz<<','<<p->mScale<<','<<p->mCodeBGR<<','<<p->mAge<<','<<int(p->mFrame)<<','<<int(p->mDead)<<']';}
int main(int argc,char**argv){for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.0;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.0;}
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);assert(*reinterpret_cast<unsigned*>(0x53B3D0)==0x40FC70);
 std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));patchJump(0x4581A0,reinterpret_cast<void*>(&originalAllocate));
 patchJump(0x4E6840,reinterpret_cast<void*>(&Utils_GetGroundHeight));
#endif
 int mode;while(std::cin>>mode){particles.clear();groundEvents.clear();CBaddy actor{};CVector offset;int has,frames,scale,decay,colorDecay,frig;
 std::cin>>has>>ground>>frames>>actor.mPos.vx>>actor.mPos.vy>>actor.mPos.vz>>actor.mAngles.vx>>actor.mAngles.vy>>actor.mAngles.vz
 >>offset.vx>>offset.vy>>offset.vz>>scale>>decay>>colorDecay>>frig;
 animation[0].pTexture=reinterpret_cast<void*>(frames);G_ANIM_TABLE[1]=animation+1;int result=0;
 if(mode==0)result=actor.MakeSpriteRing(has?&offset:nullptr);else{auto p=new CMotionBlur;p->mPos=actor.mPos;p->mVel=offset;p->SetAnim(1);p->SetSemiTransparent();p->SetScale(scale);p->field_3E=decay;p->SetTransDecay(colorDecay);p->mFrigDeltaZ=frig;}
 auto expectedGround=groundEvents;
#ifdef VALIDATE_ORIGINAL
 originalParticles.clear();unsigned char rawActor[0x300]={},rawAnimation[8+256*8]={};rawAnimation[4]=frames;
 *reinterpret_cast<void**>(0x56EA68)=rawAnimation+8;*reinterpret_cast<void**>(0x56EA50)=nullptr;
 if(mode==0){std::memcpy(rawActor+8,&actor.mPos,12);std::memcpy(rawActor+0x14,&actor.mAngles,6);groundEvents.clear();
 using Function=int(__attribute__((thiscall)) *)(void*,CVector*);int originalResult=reinterpret_cast<Function>(0x403FC0)(rawActor,has?&offset:nullptr);assert(originalResult==result&&groundEvents==expectedGround);}
 else{originalParticles.push_back(std::calloc(1,0x68));auto raw=static_cast<unsigned char*>(originalParticles[0]);
 std::memcpy(raw+0x10,&actor.mPos,12);std::memcpy(raw+0x1C,&offset,12);*reinterpret_cast<short*>(raw+0x56)=scale;*reinterpret_cast<short*>(raw+0x3E)=decay;
 *reinterpret_cast<short*>(raw+0x3C)=colorDecay;*reinterpret_cast<unsigned*>(raw+0x40)=0x2E808080;*reinterpret_cast<short*>(raw+0x38)=frig;
 raw[0x51]=frames;*reinterpret_cast<void**>(raw+0x48)=rawAnimation+8;*reinterpret_cast<unsigned*>(raw+0x60)=0x10001000;raw[0x65]=32;}
 assert(originalParticles.size()==particles.size());for(unsigned i=0;i<particles.size();i++)verify(particles[i],static_cast<unsigned char*>(originalParticles[i]));
#endif
 std::cout<<'['<<result<<",[";for(unsigned i=0;i<expectedGround.size();i++){if(i)std::cout<<',';std::cout<<expectedGround[i];}std::cout<<"],[";
 for(unsigned i=0;i<particles.size();i++){if(i)std::cout<<',';auto p=particles[i];std::cout<<'[';emit(p);std::cout<<",["<<p->mVel.vx<<','<<p->mVel.vy<<','<<p->mVel.vz<<','<<p->field_3E<<','<<p->mTransDecay<<','<<p->mFrigDeltaZ<<','<<p->mPostScale<<','<<int(p->mSemiTransparencyRate)<<"],[";
 for(int update=0;update<20;update++){if(!p->mDead)static_cast<CMotionBlur*>(p)->Move();if(update)std::cout<<',';emit(p);
#ifdef VALIDATE_ORIGINAL
 auto raw=static_cast<unsigned char*>(originalParticles[i]);if(!raw[0x37])reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x40FC70)(raw);verify(p,raw);
#endif
 }std::cout<<"]]";}
 std::cout<<"]]\n";for(auto p:particles)delete static_cast<CMotionBlur*>(p);
#ifdef VALIDATE_ORIGINAL
 for(auto p:originalParticles)std::free(p);
#endif
 }}
'''
    rng=random.Random(0x403FC0);cases=[]
    for _ in range(128):
        cases.append([0,rng.randrange(2),rng.choice([-1,-2147483648,2147483647,rng.randint(-100000000,100000000)]),rng.choice([0,1,3,8,255]),
                      *[rng.randint(-100000000,100000000) for _ in range(3)],*[rng.randint(-32768,32767) for _ in range(3)],
                      *[rng.choice([0,4095,-4095,409600,-122880,-204800,204800,rng.randint(-100000000,100000000)]) for _ in range(3)],512,32,16,0])
    for _ in range(100):
        cases.append([1,0,0,rng.choice([0,1,3,8,128,255]),*[rng.randint(-2147483648,2147483647) for _ in range(3)],0,0,0,
                      *[rng.randint(-1000000,1000000) for _ in range(3)],rng.choice([-32768,-1,0,1,31,32,512,32767]),
                      rng.choice([0,1,32,32768,65535]),rng.choice([-32768,-257,-1,0,1,16,255,32767]),rng.randrange(65536)])
    with tempfile.TemporaryDirectory(prefix='openspidey-sprite-ring-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)];
        if args.original_exe:command.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    result=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-sprite-ring.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(f'Generated {len(result)} ring placement/particle lifetime cases.')
    if args.original_exe:print('Original PC0x403FC0 placement,0x410890 emission and0x40FC70 lifetime all match.')


if __name__=='__main__':main()
