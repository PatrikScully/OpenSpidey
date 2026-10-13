"""Compile original CKnottedWeb constructor/Move and camera-facing helper; optional actual-PC Move oracle."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def build_reference(root):
    bits=(root/'src/engine/world/bit2.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text();utils=(root/'src/core/utils.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=CVector;struct SinCos{short sin,cos;};SinCos G_RCOSSIN_TBL[4096];
VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR,G_WTF_OP12,G_FTW_OP12;
void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}
struct Camera{CVector Position;}G_MIKE_CAMERA[1];int G_TIMER_RELATED;
std::vector<long long>events;unsigned randomIndex;int Rnd(int n){int v=(randomIndex++*7+1)%n;events.insert(events.end(),{1,n,v});return v;}
void Reloc_CallUserFunction(const char*,int id,unsigned*args,unsigned*result){auto*p=(CVector*)args[0];events.insert(events.end(),{2,id,p->vx,p->vy,p->vz,args[1]});*result=Rnd(7);}
void patchJump(unsigned address,void*target){auto*p=(unsigned char*)address;p[0]=0xE9;*(int*)(p+1)=(unsigned)target-address-5;}
void print_if_false(bool,const char*){}void*DCMem_New(int n,int,int,int,int){return calloc(1,n);}
CVector ZeroVector;
struct SLineSeg{CVector End;u8 r=0,g=0,b=0,code=0;};
struct CFlatBit{unsigned char pad[0x10]={};CVector mPos;unsigned char pad2[0x24]={};u32 mCodeBGR=0x2C808080;int anim=-1,scale=0,mProtected=0;
void SetSemiTransparent(){mCodeBGR|=0x2000000;}void SetAnim(int a){anim=a;}void SetScale(int s){scale=s;}void SetTint(u8 r,u8 g,u8 b){mCodeBGR=(mCodeBGR&0xff000000)|r|(g<<8)|(b<<16);}};
struct CGPolyLine{unsigned char pad[0x3C]={};int mNumSegs;SLineSeg*mSegs;CVector mStart;u8 mStartR,mStartG,mStartB,mCode;unsigned char tail[4]={};
CGPolyLine(int);void SetStartAndEnd(CVector*,CVector*);void AttachTo(void*){}void SetSemiTransparent(){}int mProtected=0;};void*G_GPOLYLINE_LIST=nullptr;
struct SKnottedWebSeg{CVector mPos;u8 field_C,field_D;short field_E;u8 field_10,field_11;short pad=0;CFlatBit*mpBit;int tail=0;};
struct CKnottedWeb:CGPolyLine{int field_58,field_5C,field_60;SKnottedWebSeg*mpExtraSegs;CGPolyLine*mpInnerLine;u8 field_6C=0,field_6D=0,field_6E=0,field_6F=0,field_70=0;u8 pad2[3]={};int field_74=0;
CKnottedWeb(const CVector&,const CVector&);void Move();};
'''
    # Remove extra stub-only protection field from CGPolyLine footprint; source accesses it through a macro alias.
    code=code.replace('int mProtected=0;};void*G_GPOLYLINE_LIST', '};void*G_GPOLYLINE_LIST')
    # Protected bit is a base byte; tail[0] is not its real offset but no Move reads protection.
    code+='#define mProtected tail[0]\n'
    for sig in ['i32 M3dMaths_SquareRoot0(', 'void gte_ldlvl(', 'void gte_ldopv1(', 'void gte_ldopv2(', 'void gte_op0(', 'void gte_sqr0(']:code+=controller_method(maths,sig)+'\n'
    code+=controller_method((root/'src/compatibility/fixed_vector_bridge.cpp').read_text(),'int CVector::Length(')+'\n'
    code+=controller_method(utils,'void Utils_CalcUnitFacingCamera(')+'\n'
    for sig in ['CGPolyLine::CGPolyLine(', 'void CGPolyLine::SetStartAndEnd(', 'static i32 KnottedWeb_NumSegs(', 'CKnottedWeb::CKnottedWeb(', 'void CKnottedWeb::Move(']:
        method=controller_method(bits,sig)
        if 'CKnottedWeb::CKnottedWeb' in sig:method=method.replace('pBit->mProtected = 1;', 'pBit->anim = 4;')
        code+=method+'\n'
    return code


def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--original-exe',type=Path);args=p.parse_args()
    root=Path(__file__).resolve().parent.parent;bits=(root/'src/engine/world/bit2.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text();utils=(root/'src/core/utils.cpp').read_text()
    code=build_reference(root)
    code+=r'''
#undef mProtected
void state(CKnottedWeb&s){std::cout<<'['<<s.mStart.vx<<','<<s.mStart.vy<<','<<s.mStart.vz<<','<<s.field_58<<','<<s.field_5C<<','<<s.field_60<<','<<unsigned(s.field_6E)<<','<<unsigned(s.field_6F)<<','<<unsigned(s.mStartR|(s.mStartG<<8)|(s.mStartB<<16)|(unsigned(s.mCode)<<24))<<','<<s.mpInnerLine->mStart.vx<<','<<s.mpInnerLine->mStart.vy<<','<<s.mpInnerLine->mStart.vz<<','<<unsigned(s.mpInnerLine->mStartR|(s.mpInnerLine->mStartG<<8)|(s.mpInnerLine->mStartB<<16)|(unsigned(s.mpInnerLine->mCode)<<24));
for(int i=0;i<s.mNumSegs;i++){auto&e=s.mSegs[i];auto&k=s.mpExtraSegs[i];std::cout<<','<<e.End.vx<<','<<e.End.vy<<','<<e.End.vz<<','<<unsigned(e.r|(e.g<<8)|(e.b<<16)|(unsigned(e.code)<<24))<<','<<k.mPos.vx<<','<<k.mPos.vy<<','<<k.mPos.vz<<','<<unsigned(k.field_C)<<','<<unsigned(k.field_D)<<','<<k.field_E<<','<<int((signed char)k.field_10)<<','<<unsigned(k.field_11)<<','<<k.mpBit->mPos.vx<<','<<k.mpBit->mPos.vy<<','<<k.mpBit->mPos.vz<<','<<k.mpBit->mCodeBGR;}
for(int i=0;i<2*s.mNumSegs;i++){auto&e=s.mpInnerLine->mSegs[i];std::cout<<','<<e.End.vx<<','<<e.End.vy<<','<<e.End.vz<<','<<unsigned(e.r|(e.g<<8)|(e.b<<16)|(unsigned(e.code)<<24));}std::cout<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E5DA0,(void*)&Rnd);
#endif
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.;}
#ifdef VALIDATE_ORIGINAL
std::memcpy((void*)0x610C48,G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));
#endif
CVector start,end;unsigned clock,seed;int flicker,wobble,pulse,decay,fire;
while(std::cin>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>G_MIKE_CAMERA[0].Position.vx>>G_MIKE_CAMERA[0].Position.vy>>G_MIKE_CAMERA[0].Position.vz>>clock>>seed>>flicker>>wobble>>pulse>>decay>>fire){events.clear();randomIndex=seed;CKnottedWeb s(start,end);s.SetStartAndEnd(&start,&end);Utils_CalcUnitFacingCamera(&start,&end,(CVector*)&s.field_58);for(int i=0;i<s.mNumSegs;i++)s.mpExtraSegs[i].mPos=s.mSegs[i].End;s.field_6C=flicker;s.field_6D=wobble;s.field_6E=pulse;s.field_70=decay;s.field_74=fire;
std::cout<<"{\"count\":"<<s.mNumSegs<<",\"states\":[";state(s);
for(int f=0;f<12;f++){clock+=2;G_TIMER_RELATED=clock;
#ifdef VALIDATE_ORIGINAL
if(!fire){*(unsigned*)0x6B4CA8=clock;unsigned saved=randomIndex;auto old=events;CKnottedWeb gold=s;auto extra=std::vector<SKnottedWebSeg>(s.mpExtraSegs,s.mpExtraSegs+s.mNumSegs);auto segs=std::vector<SLineSeg>(s.mSegs,s.mSegs+s.mNumSegs);CGPolyLine inner=*s.mpInnerLine;auto innerSegs=std::vector<SLineSeg>(inner.mSegs,inner.mSegs+inner.mNumSegs);std::vector<CFlatBit>flatGold;flatGold.reserve(s.mNumSegs);for(int i=0;i<s.mNumSegs;i++)flatGold.push_back(*extra[i].mpBit);for(int i=0;i<s.mNumSegs;i++)extra[i].mpBit=&flatGold[i];gold.mSegs=segs.data();gold.mpExtraSegs=extra.data();gold.mpInnerLine=&inner;inner.mSegs=innerSegs.data();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F9150)(&gold);auto goldRandom=randomIndex;auto goldEvents=events;randomIndex=saved;events=old;s.Move();assert(randomIndex==goldRandom&&events==goldEvents);if(std::memcmp(segs.data(),s.mSegs,s.mNumSegs*sizeof(SLineSeg))){std::cerr<<"flags "<<flicker<<","<<wobble<<","<<pulse<<" clock "<<clock<<" count "<<s.mNumSegs<<" sizeofbase "<<sizeof(CGPolyLine)<<"\n";for(int i=0;i<s.mNumSegs;i++)std::cerr<<"seg "<<i<<" actual "<<segs[i].End.vx<<","<<segs[i].End.vy<<","<<segs[i].End.vz<<" source "<<s.mSegs[i].End.vx<<","<<s.mSegs[i].End.vy<<","<<s.mSegs[i].End.vz<<"\n";abort();}assert(!std::memcmp(innerSegs.data(),s.mpInnerLine->mSegs,2*s.mNumSegs*sizeof(SLineSeg)));assert(s.field_6F==gold.field_6F&&s.field_6E==gold.field_6E);for(int i=0;i<s.mNumSegs;i++){auto&actual=s.mpExtraSegs[i];assert(!std::memcmp(&actual.mpBit->mPos,&flatGold[i].mPos,sizeof(CVector)));assert(actual.field_10==extra[i].field_10&&actual.field_11==extra[i].field_11);}}
else s.Move();
#else
s.Move();
#endif
std::cout<<',';state(s);}std::cout<<"],\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";for(int i=0;i<s.mNumSegs;i++)delete s.mpExtraSegs[i].mpBit;free(s.mpExtraSegs);free(s.mSegs);free(s.mpInnerLine->mSegs);delete s.mpInnerLine;}}
'''
    rng=random.Random(0x4f9150);cases=[]
    for i in range(100):
        start=[rng.randint(-2000000,2000000) for _ in range(3)];length=rng.choice([0,79,80,159,160,3200,4000]);end=[v+length*4096*rng.choice([-1,0,1]) for v in start]
        cases.append([*start,*end,*[rng.randint(-10000,10000) for _ in range(3)],rng.choice([0,4294967290]),rng.randrange(10),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.choice([0,1,3,20,21]),int(i%10==0)])
    with tempfile.TemporaryDirectory(prefix='openspidey-knotted-web-') as d:
        cpp=Path(d)/'reference.cpp';binary=Path(d)/'reference';cpp.write_text(code)
        flags=['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else []),input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/knotted-web.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} original knotted-web cases; nonfire Move checked against actual PC when requested.')

if __name__=='__main__':main()
