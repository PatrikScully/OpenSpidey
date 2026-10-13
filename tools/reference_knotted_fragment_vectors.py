"""Verify broken knotted-web constructor/Move against actual PC with the independently proven base constructor leaf."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_knotted_web_vectors import build_reference


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent;source=(root/'src/game/player/web.cpp').read_text()
    code=build_reference(root)
    code=code.replace('unsigned char pad[0x3C]={};int mNumSegs;',
        'unsigned char pad[0xC]={};short mAge=0,mLifetime=0;CVector mPos,mVel;unsigned char remainder[0x14]={};int mNumSegs;')
    code+=r'''
#undef mProtected
#include <new>
#include <sstream>
#include <cassert>
int hitGround,groundY;
struct LineInfo{CVector StartCoords,EndCoords;void*pItem=nullptr;CVector Position;}G_LINE_INFO;
void M3dColij_InitLineInfo(LineInfo*){}
void M3dZone_LineToItem(LineInfo*line,int mode){events.insert(events.end(),{3,line->StartCoords.vx,line->StartCoords.vy,line->StartCoords.vz,line->EndCoords.vx,line->EndCoords.vy,line->EndCoords.vz,mode});line->pItem=hitGround?reinterpret_cast<void*>(1):nullptr;line->Position.vy=groundY;}
unsigned Utils_Dist(const CVector&from,const CVector&to){return (from-to).Length();}
struct CKnottedWebFrag:CKnottedWeb{CVector mEndVelocity,mEndPos;int mGroundY,mRestDistance;CKnottedWebFrag(const CVector&,const CVector&,const CVector&,const CVector&);void Move();void Die(){reinterpret_cast<unsigned char*>(this)[0x37]=1;}};
static_assert(sizeof(CKnottedWeb)==0x78);static_assert(sizeof(CKnottedWebFrag)==0x98);
'''
    code+=controller_method(source,'CKnottedWebFrag::CKnottedWebFrag(')+'\n'
    code+=controller_method(source,'void CKnottedWebFrag::Move(')+'\n'
    code+=r'''
void* __attribute__((thiscall)) baseCtor(void*self,const CVector*start,const CVector*end){return new(self)CKnottedWeb(*start,*end);}
void groundInit(void*){}
void groundTrace(unsigned char*raw,int mode){LineInfo line;line.StartCoords=*reinterpret_cast<CVector*>(raw);line.EndCoords=*reinterpret_cast<CVector*>(raw+12);M3dZone_LineToItem(&line,mode);*reinterpret_cast<void**>(raw+0x68)=line.pItem;*reinterpret_cast<CVector*>(raw+0x6C)=line.Position;}
void cameraSide(const CVector*start,const CVector*end,CVector*side){Utils_CalcUnitFacingCamera(start,end,side);}
void state(std::ostream&out,CKnottedWebFrag&s){out<<'['<<s.mPos.vx<<','<<s.mPos.vy<<','<<s.mPos.vz<<','<<s.mEndPos.vx<<','<<s.mEndPos.vy<<','<<s.mEndPos.vz<<','<<s.mVel.vx<<','<<s.mVel.vy<<','<<s.mVel.vz<<','<<s.mEndVelocity.vx<<','<<s.mEndVelocity.vy<<','<<s.mEndVelocity.vz<<','<<s.mGroundY<<','<<s.mRestDistance<<','<<s.mAge<<','<<int(reinterpret_cast<unsigned char*>(&s)[0x37])<<','<<s.mNumSegs<<','<<s.mStart.vx<<','<<s.mStart.vy<<','<<s.mStart.vz<<','<<s.field_58<<','<<s.field_5C<<','<<s.field_60<<','<<unsigned(s.field_6E)<<','<<unsigned(s.field_6F)<<','<<unsigned(s.mStartR|(s.mStartG<<8)|(s.mStartB<<16)|(unsigned(s.mCode)<<24))<<','<<s.mpInnerLine->mStart.vx<<','<<s.mpInnerLine->mStart.vy<<','<<s.mpInnerLine->mStart.vz<<','<<unsigned(s.mpInnerLine->mStartR|(s.mpInnerLine->mStartG<<8)|(s.mpInnerLine->mStartB<<16)|(unsigned(s.mpInnerLine->mCode)<<24));
for(int i=0;i<s.mNumSegs;i++){auto&e=s.mSegs[i];auto&k=s.mpExtraSegs[i];out<<','<<e.End.vx<<','<<e.End.vy<<','<<e.End.vz<<','<<unsigned(e.r|(e.g<<8)|(e.b<<16)|(unsigned(e.code)<<24))<<','<<k.mPos.vx<<','<<k.mPos.vy<<','<<k.mPos.vz<<','<<unsigned(k.field_C)<<','<<unsigned(k.field_D)<<','<<k.field_E<<','<<int((signed char)k.field_10)<<','<<unsigned(k.field_11)<<','<<k.mpBit->mPos.vx<<','<<k.mpBit->mPos.vy<<','<<k.mpBit->mPos.vz<<','<<k.mpBit->mCodeBGR;}
for(int i=0;i<2*s.mNumSegs;i++){auto&e=s.mpInnerLine->mSegs[i];out<<','<<e.End.vx<<','<<e.End.vy<<','<<e.End.vz<<','<<unsigned(e.r|(e.g<<8)|(e.b<<16)|(unsigned(e.code)<<24));}out<<']';}
std::string snapshot(CKnottedWebFrag&s){std::ostringstream out;state(out,s);return out.str();}
void freeBits(CKnottedWebFrag&s){for(int i=0;i<s.mNumSegs;i++)delete s.mpExtraSegs[i].mpBit;free(s.mpExtraSegs);free(s.mSegs);free(s.mpInnerLine->mSegs);delete s.mpInnerLine;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patchJump(0x4F8E20,reinterpret_cast<void*>(&baseCtor));patchJump(0x4524C0,reinterpret_cast<void*>(&groundInit));patchJump(0x4549A0,reinterpret_cast<void*>(&groundTrace));patchJump(0x4E7090,reinterpret_cast<void*>(&cameraSide));
for(auto entry:std::vector<std::pair<unsigned,int>>{{0x4F6297,6},{0x4F629E,7},{0x4F63F3,7}})std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.;}std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));
CVector start,end,firstVelocity,lastVelocity;unsigned clock,seed;while(std::cin>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>firstVelocity.vx>>firstVelocity.vy>>firstVelocity.vz>>lastVelocity.vx>>lastVelocity.vy>>lastVelocity.vz>>G_MIKE_CAMERA[0].Position.vx>>G_MIKE_CAMERA[0].Position.vy>>G_MIKE_CAMERA[0].Position.vz>>hitGround>>groundY>>clock>>seed){
events.clear();randomIndex=seed;CKnottedWebFrag source(start,end,firstVelocity,lastVelocity);auto ctorEvents=events;auto ctorRandom=randomIndex;alignas(4)unsigned char raw[0x98]={};events.clear();randomIndex=seed;
auto*original=reinterpret_cast<CKnottedWebFrag*>(raw);reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,const CVector*,const CVector*,const CVector*,const CVector*)>(0x4F6290)(raw,&start,&end,&firstVelocity,&lastVelocity);
assert(events==ctorEvents&&randomIndex==ctorRandom&&snapshot(source)==snapshot(*original));std::cout<<'[';state(std::cout,source);
for(int frame=0;frame<36;frame++){clock+=2;G_TIMER_RELATED=clock;*reinterpret_cast<unsigned*>(0x6B4CA8)=clock;unsigned saved=randomIndex;auto old=events;if(!reinterpret_cast<unsigned char*>(&source)[0x37])source.Move();auto sourceEvents=events;unsigned sourceRandom=randomIndex;events=old;randomIndex=saved;
if(!raw[0x37])reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F64B0)(raw);if(snapshot(source)!=snapshot(*original)){std::cerr<<"fragment frame "<<frame<<" source="<<snapshot(source)<<" actual="<<snapshot(*original)<<'\n';abort();}assert(events==sourceEvents&&randomIndex==sourceRandom);std::cout<<',';state(std::cout,source);}
std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";freeBits(source);freeBits(*original);}}
'''
    cases=[];rng=random.Random(0x4F64B0)
    for length in [80,160,600,3200]:
        for ground_hit,ground in [(1,100000),(1,1000000),(0,0)]:
            for vertical in [-100000,0,100000]:
                start=[123456,-54321,76543];end=[start[0]+length*4096,start[1]+vertical,start[2]-length*1024]
                cases.append([*start,*end,*[rng.randint(-60000,60000) for _ in range(3)],*[rng.randint(-60000,60000) for _ in range(3)],500,300,-700,ground_hit,ground,4294967290,rng.randrange(10)])
    with tempfile.TemporaryDirectory(prefix='openspidey-knotted-fragment-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/knotted-fragment.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} complete broken knotted-web sequences; fragment ctor/Move match actual PC at every state and RNG event.')


if __name__=='__main__':main()
