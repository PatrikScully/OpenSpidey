"""Compile source tracer/barrel/CalcScreenNormal leaves; optionally compare mapped PC width/move/normal routines."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text();bits=(root/'src/engine/world/bit2.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <cmath>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
#define print_if_false(...) ((void)0)
unsigned randomState;std::vector<int>events;
int Rnd(int n){events.push_back(n);randomState=randomState*1664525u+1013904223u;return n?randomState%unsigned(n):0;}
int M3dMaths_SquareRoot0(int x){if(x<=-32768)return32768;if(x<0)return 0;return int(std::sqrt(x));}
u32 Utils_Dist(const CVector&a,const CVector&b){auto v=(a-b)>>12;return M3dMaths_SquareRoot0(v.vx*v.vx+v.vy*v.vy+v.vz*v.vz);}
struct SRibbonPoint{CVector Pos;u8 r=0,g=0,b=0,pad=0;u16 Width=0,WidthB=0;u8 rB=0,gB=0,bB=0,pad2=0;int Last1Scr=0,Last2Scr=0,Last3Scr=0;};
struct CGouraudRibbon{int mProtected=0;SRibbonPoint*mpPoints;CGouraudRibbon(int n,int){mpPoints=new SRibbonPoint[n]{};}~CGouraudRibbon(){delete[]mpPoints;}void SetRGB(u8 r,u8 g,u8 b){for(int i=0;i<5;i++){mpPoints[i].r=r;mpPoints[i].g=g;mpPoints[i].b=b;}}};
struct CSuper{};struct SLineInfo{};struct SHook{};
void CreateThugRicochet(SLineInfo*,u8,u8,u8){}bool Web_CollideWithSuper(CSuper*,const CVector*,const CVector*,SHook*,int){return false;}struct CThugPing{CThugPing(CSuper*,SHook*){}};
struct CThugBulletTracer{CGouraudRibbon*mpRibbon=nullptr,*mpRibbon2=nullptr;unsigned mMaxWidth=0;short mAge=0;bool dead=false;
 CThugBulletTracer(const CVector&,const CVector&,CSuper*,SLineInfo*,u8,u8,u8);~CThugBulletTracer(){delete mpRibbon;delete mpRibbon2;}void SetWidth();void Move();void Die(){dead=true;}};
struct SLineSeg{CVector End;u8 r=255,g=255,b=255,code=0x50;};CVector ZeroVector;
struct CGPolyLine{int mNumSegs=1;SLineSeg*mSegs;CVector mStart;u8 mStartR=255,mStartG=255,mStartB=255,mCode=0x50;CGPolyLine(int){mSegs=new SLineSeg;}~CGPolyLine(){delete mSegs;}void SetSemiTransparent();void SetStartAndEnd(CVector*,CVector*);};
struct CThug{void SetUpLaser(CGPolyLine**,CVector*,CVector*);};
struct SCalcBuffer{int field_0=0,field_4=0,pad[4]={},field_18=0,pad2=0,field_20=0,pad3[5]={},field_38=0;};
'''.replace('return32768','return 32768')
    for signature in ['CThugBulletTracer::CThugBulletTracer(', 'void CThugBulletTracer::Move(', 'void CThugBulletTracer::SetWidth(', 'void CThug::SetUpLaser(']:code+=controller_method(thug,signature)+'\n'
    for signature in ['void CGPolyLine::SetStartAndEnd(', 'void CGPolyLine::SetSemiTransparent(']:code+=controller_method(bits,signature)+'\n'
    code+=controller_method((root/'src/game/player/weapons.cpp').read_text(),'void CalcScreenNormal(').replace('(pBuffer->field_20 >> 16) - ((pBuffer->field_0 << 16) >> 16)', '(pBuffer->field_20 >> 16) - (pBuffer->field_0 >> 16)')+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void __attribute__((thiscall)) originalDie(void*actor){static_cast<unsigned char*>(actor)[0x37]=1;}
#endif
void emit(CThugBulletTracer&t){std::cout<<'['<<t.mMaxWidth<<','<<t.mAge<<','<<t.dead<<",[";for(int bank=0;bank<2;bank++){if(bank)std::cout<<',';std::cout<<'[';for(int i=0;i<5;i++){if(i)std::cout<<',';auto&p=(bank?t.mpRibbon2:t.mpRibbon)->mpPoints[i];std::cout<<'['<<p.Pos.vx<<','<<p.Pos.vy<<','<<p.Pos.vz<<','<<int(p.r)<<','<<int(p.g)<<','<<int(p.b)<<','<<p.Width<<']';}std::cout<<']';}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patchJump(0x408930,reinterpret_cast<void*>(&originalDie));
// Only Windows SEH registration is omitted; no arithmetic/branch/state instructions change.
std::memset(reinterpret_cast<void*>(0x4D6260),0x90,6);std::memset(reinterpret_cast<void*>(0x4D626E),0x90,7);std::memset(reinterpret_cast<void*>(0x4D6320),0x90,7);
#endif
int mode;while(std::cin>>mode){if(mode==2){SCalcBuffer b;int x,y;std::cin>>b.field_0>>b.field_20>>b.field_18>>b.field_38;CalcScreenNormal(&b,&x,&y,2);
#ifdef VALIDATE_ORIGINAL
int px,py;reinterpret_cast<void(*)(SCalcBuffer*,int*,int*,int)>(0x4F2AB0)(&b,&px,&py,2);assert(x==px&&y==py);
#endif
std::cout<<'['<<x<<','<<y<<"]\n";continue;}
CVector start,end;unsigned seed;int red,green,blue;std::cin>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>red>>green>>blue>>seed;
if(mode==1){CGPolyLine*line=nullptr;CThug t;t.SetUpLaser(&line,&start,&end);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char rawLine[0x58]={};SLineSeg segment{};void*rawPointer=rawLine;*reinterpret_cast<int*>(rawLine+0x3C)=1;*reinterpret_cast<SLineSeg**>(rawLine+0x40)=&segment;
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void**,CVector*,CVector*)>(0x4D6260)(&t,&rawPointer,&start,&end);
assert(!std::memcmp(rawLine+0x44,&line->mStart,12)&&!std::memcmp(&segment.End,&line->mSegs[0].End,12)&&segment.b==line->mSegs[0].b);
#endif
std::cout<<'['<<line->mStart.vx<<','<<line->mStart.vy<<','<<line->mStart.vz<<','<<line->mSegs[0].End.vx<<','<<line->mSegs[0].End.vy<<','<<line->mSegs[0].End.vz<<','<<((unsigned(line->mCode)<<24)|(unsigned(line->mStartB)<<16))<<','<<((unsigned(line->mSegs[0].code)<<24)|(unsigned(line->mSegs[0].b)<<16))<<"]\n";delete line;continue;}
randomState=seed;events.clear();CThugBulletTracer t(start,end,nullptr,nullptr,red,green,blue);std::cout<<'[';emit(t);
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x48]={},ribbon[0x48]={},white[0x48]={};SRibbonPoint points[5],whitePoints[5];std::memcpy(points,t.mpRibbon->mpPoints,sizeof(points));std::memcpy(whitePoints,t.mpRibbon2->mpPoints,sizeof(points));
*reinterpret_cast<void**>(raw+0x3C)=ribbon;*reinterpret_cast<void**>(raw+0x40)=white;*reinterpret_cast<unsigned*>(raw+0x44)=3;
*reinterpret_cast<void**>(ribbon+0x44)=points;*reinterpret_cast<void**>(white+0x44)=whitePoints;
unsigned afterConstructor=randomState;auto birthEvents=events;randomState=seed;events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4D2660)(raw);
assert(events==birthEvents&&randomState==afterConstructor);for(int i=0;i<5;i++){assert(points[i].Width==t.mpRibbon->mpPoints[i].Width&&whitePoints[i].Width==t.mpRibbon2->mpPoints[i].Width);}
#endif
for(int update=0;update<6;update++){events.clear();unsigned before=randomState;if(!t.dead)t.Move();auto expectedEvents=events;
#ifdef VALIDATE_ORIGINAL
if(!raw[0x37]){events.clear();randomState=before;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4286B0)(raw);assert(events==expectedEvents);}
assert(*reinterpret_cast<unsigned*>(raw+0x44)==t.mMaxWidth&&*reinterpret_cast<short*>(raw+0xC)==t.mAge&&bool(raw[0x37])==t.dead);
for(int i=0;i<5;i++){assert(!std::memcmp(points+i,t.mpRibbon->mpPoints+i,18));assert(!std::memcmp(whitePoints+i,t.mpRibbon2->mpPoints+i,18));}
#endif
 events=expectedEvents;std::cout<<',';emit(t);}std::cout<<"]\n";
}}
'''
    rng=random.Random(0x4D2710);cases=[]
    for i in range(256):
        start=[rng.randint(-100000000,100000000) for _ in range(3)]
        end=[x+rng.randint(-10000000,10000000) for x in start]
        cases.append([0,*start,*end,*[rng.randrange(256) for _ in range(3)],rng.randrange(2**32)])
    for distance in [0,1,999,1000,1001,32767]:cases.append([0,0,0,0,distance<<12,0,0,100,100,255,0])
    for distance in [0,1,998,999,1000,1001,10000]:cases.append([1,0,0,0,distance<<12,0,0,100,100,255,0])
    for _ in range(100):cases.append([1,*[rng.randint(-2147483648,2147483647) for _ in range(6)],0,0,0,0])
    for _ in range(256):cases.append([2,rng.randint(-2147483648,2147483647),rng.randint(-2147483648,2147483647),rng.randrange(2),rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-bullet-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        requests='\n'.join(' '.join(map(str,c)) for c in cases)+'\n';command=[str(binary)]
        if args.original_exe:command.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(command,input=requests.encode()).decode().splitlines()
    result=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-bullet.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(f'Generated {len(result)} source tracer/barrel/normal cases.');
    if args.original_exe:print('Original PC SetWidth/Move/SetUpLaser/CalcScreenNormal matches all sampled updates and normal cases; tracer constructor geometry remains source-backed.')

if __name__=='__main__':main()
