"""Compile original thug ricochet/attached ping/laser ping leaves and verify PC lifetime routines."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;thug=(root/'src/game/actors/thug.cpp').read_text();bits=(root/'src/engine/world/bit.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=Vector;std::vector<int>events;unsigned randomState;int validUntil=99,update=0;CVector hookPosition;struct CSuper{} actor;
int Rnd(int n){events.insert(events.end(),{1,n});randomState=randomState*1664525u+1013904223u;return n?randomState%unsigned(n):0;}
void SFX_PlayPos(int sound,CVector*p,int volume){events.insert(events.end(),{2,sound,p->vx,p->vy,p->vz,volume});}
struct SHook{CSVector Part;u16 Offset;};struct SHandle{unsigned a=17,b=19;};
SHandle Mem_MakeHandle(CSuper*){events.push_back(3);return {};}
void*Mem_RecoverPointer(SHandle*){events.push_back(4);return update<validUntil?&actor:nullptr;}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CSuper*,SHook*h){events.insert(events.end(),{5,h->Part.vx,h->Part.vy,h->Part.vz,h->Offset});out->vx=hookPosition.vx;out->vy=hookPosition.vy;out->vz=hookPosition.vz;}
#define print_if_false(...) ((void)0)
struct CSimpleAnim{CVector mPos;u32 mCodeBGR=0x2C808080;short mAngle=0,mScale=0;bool dead=false;int frame=0;CSimpleAnim(CVector*,int,int,int,int,int){mCodeBGR|=0x2000000;}void SetFrame(int f){frame=f;}void SetTint(int r,int g,int b){mCodeBGR=(mCodeBGR&0xff000000)|r|(g<<8)|(b<<16);}void Die(){dead=true;}};CVector ZeroVector;
struct CThugPing:CSimpleAnim{SHandle field_70;SHook field_78;CThugPing(CSuper*,SHook*);void SetPosition();void Move();};
struct CThugLaserPing;std::vector<CThugLaserPing*>pings;
struct CThugLaserPing{CVector mPos,mPosB,mPosC,mPosD;int field_84=0;CVector field_88,field_94,field_A0;u32 tint=0;int texture=-1,frame=-1,mType=0;bool dead=false;
 CThugLaserPing(const CVector&,CVector&,CVector&,u8,u8,u8);void Move();void SetTexture(int a,int b){texture=a;frame=b;}void SetSemiTransparent(){}void SetTint(int r,int g,int b){tint=r|(g<<8)|(b<<16);}void Die(){dead=true;}};
struct SLineInfo{CVector StartCoords,EndCoords,Position;CSVector Normal;};
VECTOR G_WTF_OP12,G_FTW_OP12,G_GENERAL_LONG_VECTOR,G_OP12_RESULT;
void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}
unsigned my_abs(int x){return x<0?-x:x;}
'''
    for sig in ['void VectorNormal(', 'void gte_ldopv1(', 'void gte_ldopv2(', 'void gte_op12(']:code+=controller_method(maths,sig)+'\n'
    code+=controller_method(bits,'void Bit_ReduceRGB(')+'\n'
    for sig in ['void CreateThugRicochet(', 'CThugLaserPing::CThugLaserPing(', 'void CThugLaserPing::Move(', 'void CThugPing::SetPosition(', 'CThugPing::CThugPing(', 'void CThugPing::Move(']:
        method=controller_method(thug,sig)
        if sig.startswith('CThugLaserPing::'):method=method.replace('this->field_88 = position;', 'pings.push_back(this);this->field_88 = position;')
        code+=method+'\n'
    projection=bits[bits.index('\t\tif (pBit->mAngle != 0)',bits.index('EXPORT void DisplayFlatBitList')):bits.index('\t\tu16 clut = pTexture->clut;',bits.index('EXPORT void DisplayFlatBitList'))]
    code+='\nstruct AnglePair{short sin,cos;};AnglePair G_RCOSSIN_TBL[4096];struct ProjectionBit{short mAngle;};\n'
    code+='void project(int angle,int scale,int centerX,int centerY){ProjectionBit bit{short(angle)},*pBit=&bit;int halfW=scale,halfH=scale,offX=-12,offY=-12,width=24,height=24;bool highPrecision=false;int outOfRange=0;short cx[4],cy[4];int packedCenter=(centerX&0xffff)|(centerY<<16);\n'+projection+'\nstd::cout<<\'[\';for(int i=0;i<4;i++){if(i)std::cout<<\',\';std::cout<<\'[\'<<cx[i]<<\',\'<<cy[i]<<\']\';}std::cout<<\"]\\n\";}\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void __attribute__((thiscall)) originalDie(void*actor){static_cast<unsigned char*>(actor)[0x37]=1;}
#endif
void vector(CVector v){std::cout<<'['<<v.vx<<','<<v.vy<<','<<v.vz<<']';}
void emit(CThugLaserPing*p){std::cout<<'['<<p->field_84<<','<<p->dead<<','<<p->tint<<','<<p->texture<<','<<p->frame<<','<<p->mType<<',';vector(p->mPos);std::cout<<',';vector(p->mPosB);std::cout<<',';vector(p->mPosC);std::cout<<',';vector(p->mPosD);std::cout<<']';}
void eventJson(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x408930,reinterpret_cast<void*>(&originalDie));patchJump(0x4583F0,reinterpret_cast<void*>(&Mem_RecoverPointer));patchJump(0x453EE0,reinterpret_cast<void*>(&M3dUtils_GetDynamicHookPosition));
#endif
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.0;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.0;}
int mode;while(std::cin>>mode){if(mode==2){int angle,scale,depth,cx,cy;std::cin>>angle>>scale>>depth>>cx>>cy;project(angle,(unsigned(scale)<<7)/unsigned(depth),cx,cy);continue;}events.clear();update=0;pings.clear();std::cin>>randomState;int r,g,b;std::cin>>r>>g>>b;
if(mode==0){SLineInfo l;int nx,ny,nz;std::cin>>l.StartCoords.vx>>l.StartCoords.vy>>l.StartCoords.vz>>l.EndCoords.vx>>l.EndCoords.vy>>l.EndCoords.vz>>l.Position.vx>>l.Position.vy>>l.Position.vz>>nx>>ny>>nz;l.Normal={short(nx),short(ny),short(nz)};
CreateThugRicochet(&l,r,g,b);std::cout<<'[';eventJson();std::cout<<",[";
for(unsigned i=0;i<pings.size();i++){if(i)std::cout<<',';auto p=pings[i];std::cout<<'[';vector(p->field_88);std::cout<<',';vector(p->field_94);std::cout<<',';vector(p->field_A0);std::cout<<",[";emit(p);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0xAC]={};std::memcpy(raw+0x88,&p->field_88,12);std::memcpy(raw+0x94,&p->field_94,12);std::memcpy(raw+0xA0,&p->field_A0,12);
#endif
for(int frame=0;frame<7;frame++){if(!p->dead)p->Move();std::cout<<',';emit(p);
#ifdef VALIDATE_ORIGINAL
if(!raw[0x37])reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x428B90)(raw);assert(*reinterpret_cast<int*>(raw+0x84)==p->field_84&&bool(raw[0x37])==p->dead);
assert(!std::memcmp(raw+0x10,&p->mPos,12)&&!std::memcmp(raw+0x3C,&p->mPosB,12)&&!std::memcmp(raw+0x48,&p->mPosC,12)&&!std::memcmp(raw+0x54,&p->mPosD,12));
#endif
}std::cout<<"]]";delete p;}std::cout<<"]]\n";
}else{SHook h;int x,y,z,part;std::cin>>validUntil>>hookPosition.vx>>hookPosition.vy>>hookPosition.vz>>x>>y>>z>>part;h.Part={short(x),short(y),short(z)};h.Offset=part;CThugPing ping(&actor,&h);
std::cout<<'[';std::cout<<'['<<ping.mPos.vx<<','<<ping.mPos.vy<<','<<ping.mPos.vz<<','<<ping.mCodeBGR<<','<<ping.mAngle<<','<<ping.mScale<<','<<ping.frame<<','<<ping.dead<<',';eventJson();std::cout<<']';
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x80]={};std::memcpy(raw+0x10,&ping.mPos,12);*reinterpret_cast<unsigned*>(raw+0x40)=ping.mCodeBGR;raw[0x37]=ping.dead;std::memcpy(raw+0x78,&h,8);
#endif
for(update=1;update<=22;update++){hookPosition+=CVector(11,-13,17);events.clear();if(!ping.dead)ping.Move();auto sourceEvents=events;
#ifdef VALIDATE_ORIGINAL
if(!raw[0x37]){events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4282A0)(raw);assert(events==sourceEvents);}
assert(*reinterpret_cast<unsigned*>(raw+0x40)==ping.mCodeBGR&&bool(raw[0x37])==ping.dead&&!std::memcmp(raw+0x10,&ping.mPos,12));
#endif
std::cout<<",["<<ping.mPos.vx<<','<<ping.mPos.vy<<','<<ping.mPos.vz<<','<<ping.mCodeBGR<<','<<ping.mAngle<<','<<ping.mScale<<','<<ping.frame<<','<<ping.dead<<',';events=sourceEvents;eventJson();std::cout<<']';}std::cout<<"]\n";}
}}
'''
    rng=random.Random(0x4D2F10);cases=[]
    for _ in range(256):cases.append([0,rng.randrange(2**32),rng.choice([100,255,rng.randrange(256)]),rng.randrange(256),rng.randrange(256),*[rng.randint(-100000000,100000000) for _ in range(9)],*[rng.choice([-4096,0,4096,rng.randint(-32768,32767)]) for _ in range(3)]])
    for direction in [(0,0,0),(4096,0,0),(0,4096,0),(0,0,4096),(4096,4096,4096)]:
        for normal in [(4096,0,0),(0,4096,0),(0,0,4096)]:cases.append([0,0,255,128,0,*direction,0,0,0,123,456,789,*normal])
    for _ in range(128):cases.append([1,rng.randrange(2**32),128,128,128,rng.choice([0,1,2,5,18,19,20,99]),*[rng.randint(-2147483648,2147483647) for _ in range(3)],*[rng.randint(-32768,32767) for _ in range(3)],rng.randrange(65536)])
    for angle in range(-2048,2048,8):cases.append([2,angle,rng.choice([200,400,512]),rng.choice([200,400,1000,2000]),256,120])
    with tempfile.TemporaryDirectory(prefix='openspidey-impact-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-impact.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} original impact cases; PC laser/attached lifetime checked when requested.')

if __name__=='__main__':main()
