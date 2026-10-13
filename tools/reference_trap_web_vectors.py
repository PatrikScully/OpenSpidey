"""Verify trap-web projector, strands, Move and Burst against actual PC instructions with explicit geometry/effect leaves."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
import shutil

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/player/web.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#include <cassert>
#include <cstring>
#include <new>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
using VECTOR=CVector;
struct SHandle{void*pointer=nullptr;unsigned validation=0;};
struct SHook{CSVector Part;unsigned short Offset=0;};
struct CBit{unsigned char prefix[0x10]={};CVector mPos;unsigned char middle[0x1B]={};unsigned char mDead=0,mProtected=0,pad39=0,pad3A=0,mType=0;void Die(){mDead=1;}};
struct CNonRenderedBit:CBit{};
struct CSuper{unsigned vt=0;unsigned short mFlags=0,inquiry=0;CVector mPos;unsigned char pad14[0x10]={};unsigned mRGB=0;unsigned char pad28[0xDC]={};SHandle field_104,field_10C;unsigned char pad114[0x2A]={};unsigned char field_13E=100,field_13F=94;};
struct SLineSeg{CVector End;unsigned char r=255,g=255,b=255,code=0x50;};
struct CGPolyLine:CBit{int mNumSegs;SLineSeg*mSegs;CVector mStart;unsigned char mStartR=255,mStartG=255,mStartB=255,mCode=0x50;int pad54=0;
 CGPolyLine(int count):mNumSegs(count),mSegs(new SLineSeg[count]{}){}~CGPolyLine(){delete[]mSegs;}
 void SetSemiTransparent(){for(int i=0;i<mNumSegs;i++)mSegs[i].code|=2;mCode|=2;}};
struct CQuadBit:CBit{CVector mPosB,mPosC,mPosD;int pad60=0;unsigned mCodeBGR=0x1C0001,field_68=0x2030001,mTint=0x808080;int pad70[5]={};
 void SetTexture(unsigned){}void SetSemiTransparent(){mCodeBGR=(mCodeBGR&~0x100u)|0x2C0;}void SetTint(unsigned char r,unsigned char g,unsigned char b){mTint=r|(g<<8)|(b<<16);}};
struct SWebStrand{unsigned char mHookA=0,mHookB=0,mHookC=0,pad=0;CQuadBit*mpBit=nullptr;};
struct CTrapWebEffect:CNonRenderedBit{SHandle field_3C;CGPolyLine*field_44=nullptr;SHook field_48[81];short field_2D0[81]={};short pad372=0;int field_374=0;SWebStrand field_378[20];unsigned char field_418=0,pad419=0;unsigned short field_41A=0;short field_41C=0;unsigned char field_41E=0;signed char field_41F=0;int field_420=0,field_424=0,field_428=0,field_42C=0;
 CTrapWebEffect(CSuper*,int);void MoveProjector();int CalcHook(int);void AddAnotherStrand();void Move();void Burst();};
static_assert(sizeof(CBit)==0x3C);static_assert(sizeof(CGPolyLine)==0x58);static_assert(sizeof(CQuadBit)==0x84);static_assert(sizeof(CTrapWebEffect)==0x430);static_assert(offsetof(CSuper,field_13E)==0x13E);
struct Trig{short sin,cos;}G_RCOSSIN_TBL[4096];short*word_610C48=reinterpret_cast<short*>(G_RCOSSIN_TBL);
std::vector<int> events;unsigned randomIndex=0;int collisionIndex=0,missPeriod=0;bool expired=false;
int Rnd(int bound){int value=bound?int((randomIndex++*7+1)%unsigned(bound)):0;events.insert(events.end(),{1,bound,value});return value;}
void print_if_false(bool value,const char*){assert(value);}
SHandle Mem_MakeHandle(void*pointer){return {pointer,pointer?999u:0u};}
void*Mem_RecoverPointer(SHandle*handle){return expired?nullptr:handle->pointer;}
bool Web_CollideWithSuper(CSuper*target,CVector*start,CVector*end,SHook*hook,int scale){
 collisionIndex++;bool hit=missPeriod==0||collisionIndex%missPeriod!=0;
 hook->Part=CSVector(short((start->vx-target->mPos.vx)>>8),short((end->vy-target->mPos.vy)>>8),short((start->vz-target->mPos.vz)>>8));hook->Offset=hit?collisionIndex%8+1:65535;
 events.insert(events.end(),{2,start->vx,start->vy,start->vz,end->vx,end->vy,end->vz,scale,hit});return hit;}
void M3d_BuildTransform(CSuper*){events.push_back(3);}
void M3dUtils_GetDynamicHookPosition(CVector*out,CSuper*target,SHook*hook){
 events.insert(events.end(),{4,hook->Part.vx,hook->Part.vy,hook->Part.vz,hook->Offset});
 *out=target->mPos+CVector((int(hook->Part.vx)<<8)+123*hook->Offset,int(hook->Part.vy)<<8,int(hook->Part.vz)<<8);}
int Web_GetGroundY(const CVector*position){events.insert(events.end(),{5,position->vx,position->vy,position->vz});return position->vy+0x1388000;}
struct CWebFrag{CWebFrag(int ground,const CVector&previous,const CVector&a,const CVector&b,const CVector&owner,int speed,int jitter){
 events.insert(events.end(),{6,ground,previous.vx,previous.vy,previous.vz,a.vx,a.vy,a.vz,b.vx,b.vy,b.vz,owner.vx,owner.vy,owner.vz,speed,jitter});}};
void firePulse(CVector point,int scale){events.insert(events.end(),{7,point.vx,point.vy,point.vz,scale});}
'''
    for signature in ['CTrapWebEffect::CTrapWebEffect(', 'void CTrapWebEffect::MoveProjector(',
                      'i32 CTrapWebEffect::CalcHook(', 'void CTrapWebEffect::AddAnotherStrand(']:
        code += controller_method(source, signature) + '\n'
    code += r'''
// Direct translation of original 4F8860: source web.cpp has no Move implementation.
void CTrapWebEffect::Move(){if(field_44->mNumSegs==0)return;auto*target=static_cast<CSuper*>(Mem_RecoverPointer(&field_3C));assert(target);
 CVector points[81];for(int i=0;i<=field_44->mNumSegs;i++)M3dUtils_GetDynamicHookPosition(&points[i],target,&field_48[i]);field_44->mStart=points[0];
 if(field_418){int intensity=std::min(255,((100*G_RCOSSIN_TBL[field_41A&4095].sin)>>12)+206);field_41A+=80;target->mFlags|=0x400;
 target->mRGB=(((field_41C*(intensity-32))>>8)+32)|((((field_41C*(((123*intensity)>>8)-32))>>8)+32)<<8)|((32-(field_41C>>3))<<16);
 field_41C+=8;if(field_41C>256)field_41C=256;
 for(int i=0;i<field_44->mNumSegs;i++){field_44->mSegs[i].End=points[i+1];int value=Rnd(192);field_44->mSegs[i].r=((255*value)>>8)+63;field_44->mSegs[i].g=((123*value)>>8)+30;field_44->mSegs[i].b=0;}
 int value=Rnd(192);field_44->mStartR=((255*value)>>8)+63;field_44->mStartG=((123*value)>>8)+30;field_44->mStartB=0;
 firePulse(points[Rnd(field_44->mNumSegs)+1],256);
 for(int i=0;i<field_374;i++){auto&s=field_378[i];s.mpBit->mPos=points[s.mHookA];s.mpBit->mPosB=points[s.mHookB];s.mpBit->mPosC=s.mpBit->mPosD=points[s.mHookC];value=Rnd(192);s.mpBit->SetTint(((255*value)>>8)+63,((123*value)>>8)+30,0);}}
 else{int bound=2*(target->mRGB&255);if(!(target->mFlags&0x400)||(target->mFlags&0x80)||bound>256)bound=256;
 for(int i=0;i<field_44->mNumSegs;i++){field_44->mSegs[i].End=points[i+1];unsigned char value=Rnd(bound);field_44->mSegs[i].r=field_44->mSegs[i].g=field_44->mSegs[i].b=value;}
 unsigned char value=Rnd(bound);field_44->mStartR=field_44->mStartG=field_44->mStartB=value;unsigned char tint=bound?bound-1:0;
 for(int i=0;i<field_374;i++){auto&s=field_378[i];s.mpBit->mPos=points[s.mHookA];s.mpBit->mPosB=points[s.mHookB];s.mpBit->mPosC=s.mpBit->mPosD=points[s.mHookC];s.mpBit->SetTint(tint,tint,tint);}}
 field_42C=field_44->mNumSegs;}
// Actual 4F8600 stores hook0 in mStart, not mSegs[0] as the recovered-source comment claims.
void CTrapWebEffect::Burst(){auto*target=static_cast<CSuper*>(Mem_RecoverPointer(&field_3C));if(target){int count=field_44->mNumSegs;if(count){M3d_BuildTransform(target);
 M3dUtils_GetDynamicHookPosition(&field_44->mStart,target,&field_48[0]);for(int i=0;i<count;i++)M3dUtils_GetDynamicHookPosition(&field_44->mSegs[i].End,target,&field_48[i+1]);
 int ground=Web_GetGroundY(&target->mPos);CVector previous=field_44->mStart;for(int i=0;i<count-1;i+=2){new CWebFrag(ground,previous,field_44->mSegs[i].End,field_44->mSegs[i+1].End,target->mPos,25,0);previous=field_44->mSegs[i+1].End;}}
 if(field_418)target->mFlags&=~0x400u;if(mType==0)target->field_104=Mem_MakeHandle(nullptr);else target->field_10C=Mem_MakeHandle(nullptr);}Die();}
void vec(CVector value){std::cout<<value.vx<<','<<value.vy<<','<<value.vz;}
void output(CTrapWebEffect&s,CSuper&t){auto*l=s.field_44;std::cout<<'['<<int(s.mType)<<','<<int(s.field_418)<<','<<l->mNumSegs<<','<<s.field_42C<<','<<s.field_420<<','<<s.field_424<<','<<s.field_428<<','<<int(s.field_41E)<<','<<int(s.field_41F)<<','<<s.field_41A<<','<<s.field_41C<<','<<int(s.mDead)<<','<<t.mFlags<<','<<t.mRGB<<',';
 vec(l->mStart);std::cout<<','<<unsigned(l->mStartR|(l->mStartG<<8)|(l->mStartB<<16)|(unsigned(l->mCode)<<24))<<",[";
 for(int i=0;i<=l->mNumSegs;i++){if(i)std::cout<<',';auto&h=s.field_48[i];std::cout<<'['<<h.Part.vx<<','<<h.Part.vy<<','<<h.Part.vz<<','<<h.Offset<<','<<s.field_2D0[i]<<']';}std::cout<<"],[";
 for(int i=0;i<l->mNumSegs;i++){if(i)std::cout<<',';auto&p=l->mSegs[i];std::cout<<'[';vec(p.End);std::cout<<','<<unsigned(p.r|(p.g<<8)|(p.b<<16)|(unsigned(p.code)<<24))<<']';}std::cout<<"],[";
 for(int i=0;i<s.field_374;i++){if(i)std::cout<<',';auto&p=s.field_378[i];std::cout<<'['<<int(p.mHookA)<<','<<int(p.mHookB)<<','<<int(p.mHookC)<<',';vec(p.mpBit->mPos);std::cout<<',';vec(p.mpBit->mPosB);std::cout<<',';vec(p.mpBit->mPosC);std::cout<<',';vec(p.mpBit->mPosD);std::cout<<','<<p.mpBit->mTint<<']';}std::cout<<"],[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
'''
    code += r'''
#ifdef VALIDATE_ORIGINAL
std::vector<void*> allocations;
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void*allocate(unsigned size){void*p=calloc(1,size);allocations.push_back(p);return p;}
unsigned long long makeHandle(void*pointer){return static_cast<unsigned long long>(pointer?999:0)<<32|reinterpret_cast<unsigned>(pointer);}
void __attribute__((thiscall)) baseConstructor(void*){}
CGPolyLine*__attribute__((thiscall)) lineConstructor(void*raw,int count){return new(raw)CGPolyLine(count);}
CQuadBit*__attribute__((thiscall)) quadConstructor(void*raw){return new(raw)CQuadBit();}
void __attribute__((thiscall)) setTexture(void*,unsigned){}
void __attribute__((thiscall)) fragmentConstructor(void*,int ground,CVector*previous,CVector*a,CVector*b,CVector*owner,int speed,int jitter){CWebFrag(ground,*previous,*a,*b,*owner,speed,jitter);}
void relocate(const char*,unsigned which,const unsigned*arguments,unsigned*){assert(which==5);firePulse(*reinterpret_cast<CVector*>(arguments[0]),arguments[1]);}
void initLine(void*){}
void groundLine(unsigned char*line,int mode){assert(mode==1);int ground=Web_GetGroundY(reinterpret_cast<CVector*>(line));
 *reinterpret_cast<void**>(line+0x68)=nullptr;*reinterpret_cast<int*>(line+0x10)=ground;}
#endif
int main(int argc,char**argv){
 for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;G_RCOSSIN_TBL[i]={short(std::sin(angle)*4096),short(std::cos(angle)*4096)};}
#ifdef VALIDATE_ORIGINAL
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memcpy(reinterpret_cast<void*>(0x610C48),G_RCOSSIN_TBL,sizeof(G_RCOSSIN_TBL));
 patch(0x408B10,reinterpret_cast<void*>(&baseConstructor));patch(0x4088A0,reinterpret_cast<void*>(&allocate));
 patch(0x458360,reinterpret_cast<void*>(&makeHandle));patch(0x4583F0,reinterpret_cast<void*>(&Mem_RecoverPointer));
 patch(0x412350,reinterpret_cast<void*>(&lineConstructor));patch(0x408FA0,reinterpret_cast<void*>(&quadConstructor));
 patch(0x409240,reinterpret_cast<void*>(&setTexture));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
 patch(0x4F7AE0,reinterpret_cast<void*>(&Web_CollideWithSuper));patch(0x453EE0,reinterpret_cast<void*>(&M3dUtils_GetDynamicHookPosition));
 patch(0x475F50,reinterpret_cast<void*>(&M3d_BuildTransform));patch(0x4FA080,reinterpret_cast<void*>(&fragmentConstructor));
 patch(0x47D470,reinterpret_cast<void*>(&relocate));patch(0x4524C0,reinterpret_cast<void*>(&initLine));patch(0x4549A0,reinterpret_cast<void*>(&groundLine));
 // Omit Windows SEH registration/restoration only; all source control flow and numerical instructions remain original.
 for(auto entry:std::vector<std::pair<unsigned,int>>{{0x4F8600,6},{0x4F860E,7},{0x4F882F,7},
  {0x4F7F47,6},{0x4F7F4E,7},{0x4F80E0,7},{0x4F83C0,6},{0x4F83CE,7},{0x4F8568,7}})
  std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
#endif
 int type,fire,add,miss,seed,upper,lower,flags,frames,expire;unsigned tint;CVector position;
 while(std::cin>>type>>fire>>add>>miss>>seed>>upper>>lower>>flags>>tint>>frames>>expire>>position.vx>>position.vy>>position.vz){
 CSuper target{};target.mPos=position;target.field_13E=upper;target.field_13F=lower;target.mFlags=flags;target.mRGB=tint;
 randomIndex=seed;collisionIndex=0;missPeriod=miss;expired=false;events.clear();CTrapWebEffect source(&target,type);source.field_418=fire;
 std::vector<std::string> states;auto capture=[&](){std::ostringstream out;auto*old=std::cout.rdbuf(out.rdbuf());output(source,target);std::cout.rdbuf(old);states.push_back(out.str());events.clear();};capture();
 for(int i=0;i<add;i++)source.AddAnotherStrand();capture();for(int i=0;i<frames;i++){source.Move();capture();}expired=expire;source.Burst();capture();
#ifdef VALIDATE_ORIGINAL
 CSuper actualTarget{};actualTarget.mPos=position;actualTarget.field_13E=upper;actualTarget.field_13F=lower;actualTarget.mFlags=flags;actualTarget.mRGB=tint;
 alignas(4)unsigned char raw[0x430]={};auto&actual=*reinterpret_cast<CTrapWebEffect*>(raw);
 randomIndex=seed;collisionIndex=0;expired=false;events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*,int)>(0x4F7F40)(raw,&actualTarget,type);actual.field_418=fire;
 unsigned index=0;auto compare=[&](){std::ostringstream out;auto*old=std::cout.rdbuf(out.rdbuf());output(actual,actualTarget);std::cout.rdbuf(old);if(out.str()!=states[index]){std::cerr<<"Mismatch stage "<<index<<"\n";abort();}index++;events.clear();};compare();
 for(int i=0;i<add;i++)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F83C0)(raw);compare();for(int i=0;i<frames;i++){reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F8860)(raw);compare();}
 expired=expire;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F8600)(raw);compare();
 delete[]actual.field_44->mSegs;for(void*allocation:allocations)free(allocation);allocations.clear();
#endif
 std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";
 delete source.field_44;for(int i=0;i<source.field_374;i++)delete source.field_378[i].mpBit;
 }
}
'''
    code = code.replace('#include <new>', '#include <new>\n#include <sstream>')
    rng = random.Random(0x4F8860)
    cases = []
    for kind in [0, 1]:
        for fire in [0, 1]:
            for add in [0, 1, 8, 16, 80, 100]:
                for miss in [0, 3]:
                    cases.append([kind, fire, add, miss, 0, 100, 94, 2, 0x808080, 3, 0, 123456, -54321, 76543])
    for index in range(32):
        cases.append([index % 2, (index // 2) % 2, rng.choice([8, 16, 80, 100]), rng.choice([0, 2, 3]),
                      rng.randrange(20), rng.randint(2, 255), rng.randrange(256), rng.choice([2, 0x402, 0x482]),
                      rng.choice([0, 1, 127, 128, 255, 0x808080]), rng.choice([1, 3, 40]), index % 3 == 0,
                      *[rng.randint(-1000000, 1000000) for _ in range(3)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-trap-web-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        flags = ['-m32', '-fno-pie', '-no-pie', '-DVALIDATE_ORIGINAL'] if args.original_exe else ['-m32']
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', *flags, '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        inputs = ('\n'.join(' '.join(str(int(value)) for value in case) for case in cases) + '\n').encode()
        try:
            lines = subprocess.check_output(command, input=inputs).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copyfile(cpp, '/tmp/openspidey-trap-web-reference.cpp')
            shutil.copyfile(binary, '/tmp/openspidey-trap-web-reference')
            Path('/tmp/openspidey-trap-web-cases.txt').write_bytes(inputs)
            raise
    fixtures = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/trap-web.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Generated {len(fixtures)} complete trap-web sequences; ' +
          ('actual PC constructor/projector/strand/Move/Burst verified.' if args.original_exe else 'compiled source and explicit PC Move/Burst translation.'))


if __name__ == '__main__':
    main()
