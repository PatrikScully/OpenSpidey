"""Compile original impact ring, CSplat and CWebFrag constructors/Move; optional actual-PC Move comparison."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--original-exe',type=Path);args=p.parse_args()
    root=Path(__file__).resolve().parent.parent;web=(root/'src/game/player/web.cpp').read_text();bits=(root/'src/engine/world/bit.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=CVector;using SVECTOR=CSVector;
VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR,G_WTF_OP12,G_FTW_OP12;
void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}
void print_if_false(bool,const char*){}
std::vector<int>randomEvents;unsigned randomIndex;int Rnd(int n){int v=(randomIndex++*7+1)%n;randomEvents.insert(randomEvents.end(),{n,v});return v;}
void Bit_ReduceRGB(u32*,int);void Utils_CalcPerps(CVector*,CVector*,CVector*);
struct CImpactRing{CVector mPos;int mTargetScale=0,mGrowRate=0,mColorDecay=0;short mScale=0;unsigned mCodeBGR=0x2C808080,mPostScale=0;unsigned short mFrigDeltaZ=0;bool dead=false;int anim=0,frame=0;
CImpactRing(const CVector&,u8,u8,u8,int,int);void Move();void Die(){dead=true;}void SetTint(int r,int g,int b){mCodeBGR=(mCodeBGR&0xff000000)|r|(g<<8)|(b<<16);}void SetAnim(int a){anim=a;}void SetFrame(int f){frame=f;}void SetSemiTransparent(){mCodeBGR|=0x2000000;}};
unsigned Spool_FindTextureChecksum(const char*){return 1;}
struct CQuadBit{CVector mPos,mPosB,mPosC,mPosD;unsigned mTint=0x808080;short mAge=0;bool dead=false;
void OrientUsing(CVector*,SVECTOR*,int,int);void SetTexture(unsigned){}void SetTint(int r,int g,int b){mTint=r|(g<<8)|(b<<16);}void SetSubtractiveTransparency(){}void Die(){dead=true;}};
struct CSplat:CQuadBit{u8 mMode=0;int mType=0;CSplat(const CVector&,const SVECTOR&);void Move();};
struct CGLine{CVector mStart,mEnd;u32 mCodeBGR0=0x50808080,mPadBGR1=0x55808080;int mProtected=0;
void SetRGB0(u8,u8,u8);void SetRGB1(u8,u8,u8);};
struct CWebFrag:CGLine{CVector field_5C,field_68,field_74;CGLine*field_84=nullptr;int mGroundY=0,field_88=0;bool dead=false;
CWebFrag(int,const CVector&,const CVector&,const CVector&,const CVector&,int,int);void Move();void Die(){dead=true;}};
'''
    for sig in ['i32 M3dMaths_SquareRoot0(', 'void gte_ldlvl(', 'void gte_ldopv1(', 'void gte_ldopv2(', 'void gte_op12(', 'void gte_sqr0(']:code+=controller_method(maths,sig)+'\n'
    for sig in ['void CGLine::SetRGB0(', 'void CGLine::SetRGB1(']:code+=controller_method((root/'src/engine/world/bit2.cpp').read_text(),sig)+'\n'
    code+=controller_method((root/'src/compatibility/fixed_vector_bridge.cpp').read_text(),'int CVector::Length(')+'\n'
    code+=controller_method((root/'src/core/utils.cpp').read_text(),'void Utils_CalcPerps(')+'\n'
    code+=controller_method(bits,'void Bit_ReduceRGB(')+'\n'+controller_method(bits,'void CQuadBit::OrientUsing(CVector *a2, SVECTOR *a3, int a4, int a5)')+'\n'
    for sig in ['CImpactRing::CImpactRing(','void CImpactRing::Move(','CSplat::CSplat(','void CSplat::Move(','CWebFrag::CWebFrag(','void CWebFrag::Move(']:code+=controller_method(web,sig)+'\n'
    code+=r'''
void vec(CVector v){std::cout<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
#endif
int mode;while(std::cin>>mode){randomEvents.clear();std::cout<<'[';
if(mode==0){CVector p;int r,g,b,size,grow;std::cin>>p.vx>>p.vy>>p.vz>>r>>g>>b>>size>>grow;CImpactRing s(p,r,g,b,size,grow);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x74]={};*reinterpret_cast<unsigned*>(raw+0x40)=s.mCodeBGR;*reinterpret_cast<int*>(raw+0x68)=size;*reinterpret_cast<int*>(raw+0x6c)=grow;*reinterpret_cast<int*>(raw+0x70)=s.mColorDecay;
#endif
for(int f=0;f<45;f++){if(f){if(!s.dead)s.Move();std::cout<<',';}
#ifdef VALIDATE_ORIGINAL
if(f&&!(raw[0x37]&1))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F98E0)(raw);
assert(s.mScale==*reinterpret_cast<short*>(raw+0x56)&&s.mCodeBGR==*reinterpret_cast<unsigned*>(raw+0x40)&&s.dead==bool(raw[0x37]&1));
#endif
std::cout<<'['<<s.mScale<<','<<s.mCodeBGR<<','<<s.mColorDecay<<','<<s.dead<<']';}}
else if(mode==1){CVector p;SVECTOR n;std::cin>>p.vx>>p.vy>>p.vz>>n.vx>>n.vy>>n.vz;CSplat s(p,n);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x88]={};*reinterpret_cast<unsigned*>(raw+0x6c)=s.mTint;
#endif
std::cout<<'[';vec(s.mPos);std::cout<<',';vec(s.mPosB);std::cout<<',';vec(s.mPosC);std::cout<<',';vec(s.mPosD);std::cout<<']';for(int f=0;f<45;f++){if(f&&!s.dead)s.Move();
#ifdef VALIDATE_ORIGINAL
if(f&&!(raw[0x37]&1))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x420BB0)(raw);
assert(s.mAge==*reinterpret_cast<short*>(raw+0xc)&&s.mMode==raw[0x84]&&s.mTint==*reinterpret_cast<unsigned*>(raw+0x6c)&&s.dead==bool(raw[0x37]&1));
#endif
std::cout<<",["<<unsigned(s.mMode)<<','<<s.mAge<<','<<s.mTint<<','<<s.dead<<']';}}
else{CVector p,a,b,owner;int ground,speed,jitter;std::cin>>ground>>p.vx>>p.vy>>p.vz>>a.vx>>a.vy>>a.vz>>b.vx>>b.vy>>b.vz>>owner.vx>>owner.vy>>owner.vz>>speed>>jitter>>randomIndex;CWebFrag s(ground,p,a,b,owner,speed,jitter);
std::cout<<'[';for(unsigned i=0;i<randomEvents.size();i++){if(i)std::cout<<',';std::cout<<randomEvents[i];}std::cout<<']';for(int f=0;f<140;f++){if(f&&!s.dead)s.Move();std::cout<<",[";vec(s.mStart);std::cout<<',';vec(s.mEnd);std::cout<<',';vec(s.field_84->mStart);std::cout<<',';vec(s.field_84->mEnd);std::cout<<',';vec(s.field_68);std::cout<<',';vec(s.field_5C);std::cout<<',';vec(s.field_74);std::cout<<','<<s.mCodeBGR0<<','<<s.mPadBGR1<<','<<s.field_84->mCodeBGR0<<','<<s.field_84->mPadBGR1<<','<<s.field_88<<','<<s.field_84->mProtected<<','<<s.dead<<']';}delete s.field_84;}
std::cout<<"]\n";}}
'''
    rng=random.Random(0x4f9790);cases=[]
    for size,grow in [(3216,316),(316,316),(632,316),(32767,1),(32767,32767),(32768,32768),(4096,65535),(500,100),(1,1)]:
        for colours in [[200,210,255],[0,0,0],[1,2,3],[255,0,0]]:cases.append([0,12345,-54321,67890,*colours,size,grow])
    for normal in [[0,-4096,0],[0,4096,0],[4096,0,0],[0,0,4096],[0,0,0],[-32768,32767,-32768]]+[[rng.randint(-32768,32767) for _ in range(3)] for i in range(50)]:cases.append([1,*[rng.randint(-2000000000,2000000000) for _ in range(3)],*normal])
    for i in range(60):
        position=[rng.randint(-300000,300000) for _ in range(3)];a=[v+rng.randint(-80000,80000) for v in position];b=[v+rng.randint(-80000,80000) for v in position]
        cases.append([2,rng.choice([position[1]-1,position[1],position[1]+100000]),*position,*a,*b,*position,rng.choice([0,25,100]),i%2,i%3])
    with tempfile.TemporaryDirectory(prefix='openspidey-web-effects-') as d:
        cpp=Path(d)/'reference.cpp';binary=Path(d)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        cmd=[str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-effects.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} compiled-source web effects; original PC ring/splat Move verified when requested.')

if __name__=='__main__':main()
