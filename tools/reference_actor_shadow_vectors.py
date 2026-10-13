"""Compile actual CBody shadow methods and CQuadBit orientation/transparency into bounded numeric gold cases."""
from pathlib import Path
import argparse, json, random, subprocess, tempfile
from reference_ground_response_vectors import controller_method

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--original-exe',type=Path,help='Validate original PC quad blend selection without starting the Windows game.')
args = parser.parse_args()
root = Path(__file__).resolve().parent.parent
body = (root/'src/engine/world/ob.cpp').read_text()
quad = (root/'src/engine/world/bit.cpp').read_text()
code = r'''
#include <cstdint>
#include <iostream>
using i32=int32_t;using u32=uint32_t;using u8=uint8_t;
struct CVector { i32 vx=0,vy=0,vz=0;CVector(){} CVector(i32 x,i32 y,i32 z):vx(x),vy(y),vz(z){} CVector operator+(CVector b)const{return{vx+b.vx,vy+b.vy,vz+b.vz};} CVector operator-(CVector b)const{return{vx-b.vx,vy-b.vy,vz-b.vz};} void operator*=(i32 n){vx*=n;vy*=n;vz*=n;} };
struct SVECTOR{int16_t vx,vy,vz;};struct CSVector:SVECTOR{void Set(int16_t x,int16_t y,int16_t z){vx=x;vy=y;vz=z;}};
void Utils_CalcPerps(CVector*a,CVector*b,CVector*c){*b={0,0,-4096};*c={4096,0,0};}
i32 G_TOTALBITUSAGE=0;constexpr u32 CBODY_HASSHADOW=8;
void* G_QUADBIT_LIST=nullptr;
struct CQuadBit{CQuadBit();void AttachTo(void*){}u32 field_68=0,field_70=0;CVector mPos,mPosB,mPosC,mPosD;u32 mCodeBGR=0,mTint=0;i32 mFrigDeltaZ=0,mProtected=0;void SetTexture(int,int){}void SetSubtractiveTransparency();void SetTransparency(unsigned char);void OrientUsing(CVector*,SVECTOR*,int,int);};
struct CBody{u32 mCBodyFlags=0;CQuadBit*mpShadow=nullptr;CVector mShadowPos;uint16_t mShadowScale=0;int16_t mShadowDist=0,mShadowThreshold=0;void KillShadow();void UpdateShadow();};
'''
code += controller_method(quad, 'CQuadBit::CQuadBit(void)')
code += controller_method(body, 'void CBody::KillShadow(void)')
code += controller_method(body, 'void CBody::UpdateShadow(void)')
code += controller_method(quad, 'void CQuadBit::SetSubtractiveTransparency()')
code += controller_method(quad, 'void CQuadBit::SetTransparency(unsigned char a2)')
code += controller_method(quad, 'void CQuadBit::OrientUsing(CVector *a2, SVECTOR *a3, int a4, int a5)')
code += r'''
int main(){int f,x,y,z,s,d,t;while(std::cin>>f>>x>>y>>z>>s>>d>>t){CBody b;b.mCBodyFlags=f;b.mShadowPos={x,y,z};b.mShadowScale=s;b.mShadowDist=d;b.mShadowThreshold=t;b.UpdateShadow();std::cout<<(b.mpShadow!=nullptr);if(b.mpShadow){for(auto p:{b.mpShadow->mPos,b.mpShadow->mPosB,b.mpShadow->mPosC,b.mpShadow->mPosD})std::cout<<" "<<p.vx<<" "<<p.vy<<" "<<p.vz;std::cout<<" "<<b.mpShadow->mTint<<" "<<b.mpShadow->mCodeBGR<<" "<<b.mpShadow->mFrigDeltaZ<<" "<<b.mpShadow->mProtected;}std::cout<<"\n";b.KillShadow();}}
'''
rng=random.Random(0x409400)
cases=[[8,0,409600,0,50,d,200] for d in [-300,-200,-1,0,1,199,200,201,300]]
for i in range(400):cases.append([rng.randrange(32),*[rng.randrange(-10000000,10000000) for _ in range(3)],rng.randrange(65536),rng.randrange(-32768,32768),rng.choice([-32768,-200,-1,1,200,32767])])
with tempfile.TemporaryDirectory() as temp:
 p=Path(temp);(p/'shadow.cpp').write_text(code)
 subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(p/'shadow.cpp'),'-o',str(p/'shadow')],check=True)
 out=subprocess.run([str(p/'shadow')],input='\n'.join(' '.join(map(str,c)) for c in cases),text=True,capture_output=True,check=True).stdout.splitlines()
result={'source':'CBody::UpdateShadow/KillShadow + CQuadBit::OrientUsing/SetTransparency/SetSubtractiveTransparency; fixed downward normal Utils_CalcPerps independently reduced. Original PC DisplayQuadBitList409F69..409F8C proven selector bits40/80 only.', 'cases':[{'input':c,'output':list(map(int,o.split()))} for c,o in zip(cases,out)]}
(root/'modern/OpenSpidey.Tests/Fixtures/actor-shadows.json').write_text(json.dumps(result,indent=2)+'\n')
print(f'Generated {len(cases)} compiled original shadow cases.')

if args.original_exe:
    selector = r'''
#include "original_executable.h"
#include <iostream>
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);const u8 opcode[]={0xF6,0xC1,0x40};assert(!memcmp(reinterpret_cast<void*>(0x409F69),opcode,3));*reinterpret_cast<u8*>(0x409F8A)=0xC3;unsigned cases=0;for(unsigned code=0;code<256;code++){u32 mode,alpha;asm volatile("xorl %%eax,%%eax\n\tmovl $0xff000000,%%edi\n\tmovl $0x409F69,%%ebx\n\tcall *%%ebx":"=a"(mode),"=D"(alpha):"c"(code):"ebx","memory","cc");assert(mode==((code&0x40)?1+((code&0x80)!=0):0));assert(alpha==(((code&0xC0)==0xC0)?0x80000000:0xFF000000));cases++;}std::cout<<"Passed "<<cases<<" actual PC DisplayQuadBitList selector/alpha cases, including shadow lowbyte0x41.\n";}
'''
    with tempfile.TemporaryDirectory() as temp:
        p = Path(temp); (p/'selector.cpp').write_text(selector)
        subprocess.run(['c++','-std=c++17','-O0','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(p/'selector.cpp'),'-o',str(p/'selector')],check=True)
        subprocess.run([str(p/'selector'),str(args.original_exe.resolve())],check=True)
