"""Compile actual chunk/glass constructors and moves, compare numerical state/RNG order with original PC code."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method

def build_reference(root):
    bits=(root/'src/engine/world/bit.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
using VECTOR=Vector;struct SVECTOR{short vx,vy,vz,pad;};
unsigned rng;std::vector<int>ranges;int Rnd(int n){ranges.push_back(n);rng=rng*1664525u+1013904223u;return int(rng%unsigned(n));}
struct CBit{
 unsigned char raw[216]={};CVector&mPos=*reinterpret_cast<CVector*>(raw+0x10);CVector&mVel=*reinterpret_cast<CVector*>(raw+0x1C);
 short&mAge=*reinterpret_cast<short*>(raw+0xC);unsigned short&mLifetime=*reinterpret_cast<unsigned short*>(raw+0xE);unsigned char&mDead=raw[0x37];
 void SetPos(const CVector&p){mPos=p;}void Die(){mDead=1;}void AttachTo(void*){}
};int G_CHUNKBIT_LIST,G_GLASS_LIST;
struct CChunkBit:CBit{
 unsigned char&mType=raw[0x36];SVECTOR&mPosA=*reinterpret_cast<SVECTOR*>(raw+0x3C);SVECTOR&mPosB=*reinterpret_cast<SVECTOR*>(raw+0x44);SVECTOR&mPosC=*reinterpret_cast<SVECTOR*>(raw+0x4C);SVECTOR&mPosD=*reinterpret_cast<SVECTOR*>(raw+0x54);
 CSVector&mAngles=*reinterpret_cast<CSVector*>(raw+0x8C);unsigned&mColorA=*reinterpret_cast<unsigned*>(raw+0xB8);unsigned&mColorB=*reinterpret_cast<unsigned*>(raw+0xBC);unsigned&mColorC=*reinterpret_cast<unsigned*>(raw+0xC0);unsigned&mColorD=*reinterpret_cast<unsigned*>(raw+0xC4);
 CChunkBit(CSVector*,CSVector*,CSVector*);void SetRGB(unsigned char,unsigned char,unsigned char);
};struct CRibbon{void SetPos(CVector&){};};
struct CShatterBit:CChunkBit{
 CSVector&mSpinRate=*reinterpret_cast<CSVector*>(raw+0xCC);CRibbon*mTrailRibbon=nullptr;
 CShatterBit(CSVector const&,CSVector const&,CSVector const&,CVector const&,int);void SetPos(CVector const&);void Move();
};
struct CGlassBit:CBit{
 CVector&mPosA=*reinterpret_cast<CVector*>(raw+0x3C);CVector&mPosB=*reinterpret_cast<CVector*>(raw+0x48);CVector&mPosC=*reinterpret_cast<CVector*>(raw+0x54);int&mGroundY=*reinterpret_cast<int*>(raw+0x60);
 unsigned char&mDefaultR=raw[0x64],&mDefaultG=raw[0x65],&mDefaultB=raw[0x66],&mR=raw[0x67],&mG=raw[0x68],&mB=raw[0x69],&mFadeRate=raw[0x6A];
 CGlassBit(const CVector*,const CVector*,int,unsigned char,unsigned char,unsigned char,int,int,int);void Move();
};
CVector G_SHATTER_FACE_CENTER;short normal[3];short*G_SHATTER_VELOCITY_SCALE=normal;
void CChunkBit_CalculateWorldCoords(CChunkBit*){} // Numerical motion/constructor proof; matrix corners are separately proven pose math.
int CVector::Length(){int x=vx>>12,y=vy>>12,z=vz>>12;int v=x*x+y*y+z*z;return v<=-32768?32768:v<0?0:int(std::sqrt(v));}
'''
    code+=controller_method(maths,'void VectorNormal(')+'\n'
    for sig in ['CChunkBit::CChunkBit(','void CChunkBit::SetRGB(','CShatterBit::CShatterBit(','void CShatterBit::SetPos(','void CShatterBit::Move(','CGlassBit::CGlassBit(','void CGlassBit::Move(']:
        body=controller_method(bits,sig)
        if sig=='void CShatterBit::SetPos(':body=body.replace('this->mVel >>= 1;','this->mVel <<= 1;') # ActualPC48BF20 calls4E7680.
        code+=body+'\n'
    code+=r'''
void patch(unsigned addr,void*fn){unsigned char*p=(unsigned char*)addr;p[0]=0xE9;*(unsigned*)(p+1)=unsigned(fn)-addr-5;}
void __attribute__((thiscall))dead(unsigned char*p){p[0x37]=1;}
void __attribute__((thiscall))noCoords(void*){}
void setup(const char*path){original_executable::loadOriginalImage(path);patch(0x4E5DA0,(void*)&Rnd);patch(0x408930,(void*)&dead);patch(0x40BA20,(void*)&noCoords);patch(0x470430,(void*)&VectorNormal);
 for(unsigned addr:{0x48BDC7u,0x40B577u,0x411167u}){unsigned char*p=(unsigned char*)addr;assert(p[0]==0x64);p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);}
 for(unsigned addr:{0x48BDCEu,0x48BE8Fu,0x40B57Eu,0x40B7A8u,0x41116Eu,0x411370u}){unsigned char*p=(unsigned char*)addr;assert(p[0]==0x64);std::memset(p,0x90,7);}
}
std::vector<long long>snap(unsigned char*p,int kind){std::vector<long long>v;
 for(int off:{0x10,0x14,0x18,0x1C,0x20,0x24})v.push_back(*(int*)(p+off));v.push_back(*(unsigned short*)(p+0xE));v.push_back(p[0x37]==0);
 if(kind==0){for(int off:{0x3C,0x3E,0x40,0x44,0x46,0x48,0x4C,0x4E,0x50,0x54,0x56,0x58,0x8C,0x8E,0x90,0xCC,0xCE,0xD0})v.push_back(*(short*)(p+off));for(int off:{0xB8,0xBC,0xC0,0xC4})v.push_back(*(unsigned*)(p+off));}
 else {for(int off:{0x3C,0x40,0x44,0x48,0x4C,0x50,0x54,0x58,0x5C,0x60})v.push_back(*(int*)(p+off));v.push_back(*(short*)(p+0xC));for(int off=0x64;off<=0x69;off++)v.push_back(p[off]);}
 return v;}
void print(std::vector<long long>v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);setup(argv[1]);int kind;CVector a,b,c,pos,centre,norm,velocity;int ground;unsigned rgb,seed;
 while(std::cin>>kind>>a.vx>>a.vy>>a.vz>>b.vx>>b.vy>>b.vz>>c.vx>>c.vy>>c.vz>>pos.vx>>pos.vy>>pos.vz>>centre.vx>>centre.vy>>centre.vz>>norm.vx>>norm.vy>>norm.vz>>velocity.vx>>velocity.vy>>velocity.vz>>ground>>rgb>>seed){
  unsigned char original[216]={};rng=seed;ranges.clear();std::vector<std::vector<long long>>expected;std::vector<int>expectedRanges;
  CSVector sa(a.vx,a.vy,a.vz),sb(b.vx,b.vy,b.vz),sc(c.vx,c.vy,c.vz);for(int i=0;i<3;i++)normal[i]=((int*)&norm)[i];G_SHATTER_FACE_CENTER=centre;
  std::vector<unsigned char>recovered(216);if(kind==0){CShatterBit s(sa,sb,sc,pos,0);s.SetRGB(rgb,rgb>>8,rgb>>16);expected.push_back(snap(s.raw,kind));for(int frame=0;frame<90;frame++){if(!s.mDead)s.Move();expected.push_back(snap(s.raw,kind));}}
  else {CGlassBit s(&pos,&velocity,ground,rgb,rgb>>8,rgb>>16,100,100,100);expected.push_back(snap(s.raw,kind));for(int frame=0;frame<90;frame++){if(!s.mDead)s.Move();expected.push_back(snap(s.raw,kind));}}
  expectedRanges=ranges;rng=seed;ranges.clear();*(CVector*)0x6A75F8=centre;*(short**)0x6A768C=normal;
  if(kind==0){using Ctor=void(__attribute__((thiscall))*)(void*,CSVector*,CSVector*,CSVector*,CVector*,int);((Ctor)0x48BDC0)(original,&sa,&sb,&sc,&pos,0);using Rgb=void(__attribute__((thiscall))*)(void*,unsigned char,unsigned char,unsigned char);((Rgb)0x40B830)(original,rgb,rgb>>8,rgb>>16);}
  else {using Ctor=void(__attribute__((thiscall))*)(void*,CVector*,CVector*,int,unsigned char,unsigned char,unsigned char,int,int,int);((Ctor)0x411160)(original,&pos,&velocity,ground,rgb,rgb>>8,rgb>>16,100,100,100);}
  assert(snap(original,kind)==expected[0]);std::cout<<"{\"states\":[";print(expected[0]);
  for(int frame=0;frame<90;frame++){if(!original[0x37]){using Move=void(__attribute__((thiscall))*)(void*);((Move)(kind==0?0x48C060:0x4113F0))(original);}assert(snap(original,kind)==expected[frame+1]);std::cout<<',';print(expected[frame+1]);}
  assert(ranges==expectedRanges);std::cout<<"],\"ranges\":[";for(unsigned i=0;i<ranges.size();i++){if(i)std::cout<<',';std::cout<<ranges[i];}std::cout<<"]}\n";
 }
}
'''
    return code

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent;code=build_reference(root)
    cases=[];rng=random.Random(0x48BDC0)
    for kind in [0,1]:
        for index in range(100):
            corners=[rng.randint(-32768,32767) for _ in range(9)];position=[rng.randint(-20000,20000)<<12 for _ in range(3)]
            centre=position if index<5 else [rng.randint(-20000,20000)<<12 for _ in range(3)]
            normal=[rng.randint(-4096,4096) for _ in range(3)];velocity=[rng.randint(-500000,500000) for _ in range(3)]
            cases.append([kind,*corners,*position,*centre,*normal,*velocity,rng.choice([0,position[1],2147483647]),rng.randrange(1<<24),rng.randrange(1<<32)])
    with tempfile.TemporaryDirectory(prefix='shatter-source-') as temp:
        path=Path(temp);(path/'reference.cpp').write_text(code)
        subprocess.run(['g++','-m32','-std=c++17','-O0','-fwrapv','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(path/'reference.cpp'),str(root/'src/core/fixed_vector.cpp'),'-o',str(path/'reference')],check=True)
        inputs='\n'.join(' '.join(map(str,c)) for c in cases);(path/'input').write_text(inputs)
        result=subprocess.run([str(path/'reference'),str(args.original_exe)],input=inputs,text=True,capture_output=True)
        if result.returncode:
            subprocess.run(['gdb','-q','-batch','-ex',f'run {args.original_exe} < {path}/input','-ex','bt','-ex','x/8i $pc','--args',str(path/'reference'),str(args.original_exe)],check=False)
            raise RuntimeError(result.stderr)
    (root/'modern/OpenSpidey.Tests/Fixtures/shatter-fragments.json').write_text(json.dumps({'originalPcNumericalStateAndRandomOrderVerified':True,'cases':[{'input':c,**json.loads(s)} for c,s in zip(cases,result.stdout.splitlines(),strict=True)]},separators=(',',':'))+'\n')
    print(f'Original PC shatter/glass constructors and90updates verified: {len(cases)} trajectories.')
if __name__=='__main__':main()
