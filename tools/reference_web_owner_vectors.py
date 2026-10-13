"""Compile CWeb's ordinary no-target Fire/SetFirePos/Blob/AI path, composing source knotted endpoints."""
import argparse,json,subprocess,tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method
from reference_knotted_web_vectors import build_reference


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent;web=(root/'src/game/player/web.cpp').read_text()
    body=(root/'src/engine/world/ob.cpp').read_text()
    code=build_reference(root)+r'''
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
#undef mProtected
struct SHandle{void*pWhatever=nullptr;int validation=0;};
void DoAssert(bool value,const char*){if(!value)abort();}
struct CBody{CVector mPos;unsigned short mFlags=0,mType=0,mCBodyFlags=0x16;int mLastUpdateTick=0,mElapsedTicks=0,mAccumulatedTicks=0;void AttachTo(void*){events.insert(events.end(),{3});}void DeleteFrom(void*){events.insert(events.end(),{4});}void EveryFrame();};
using CItem=CBody;struct CManipOb:CBody{void GetAttachPoint(CVector*){abort();}};
struct CPlayer:CBody{int field_8F8=1;};CPlayer player;CBody*G_MECHLIST=&player;
struct CSuper:CBody{SHandle field_10C,field_104;unsigned short field_152=0,field_150=0,field_154=0,mFrame=0,mAnim=0;unsigned char field_143=0,mAnimDir=0;};
SHandle Mem_MakeHandle(CBody*p){events.insert(events.end(),{5,p?1:0});return {p,0};}
void*Mem_RecoverPointer(SHandle*h){events.insert(events.end(),{6,h->pWhatever?1:0});return nullptr;}
struct CTrapWebEffect{int field_42C=0;CGPolyLine*field_44=nullptr;CTrapWebEffect(CSuper*,int){}void Burst(){}void AddAnotherStrand(){}};
struct CKnottedWebSplat{CKnottedWebSplat(CVector*,CVector*){abort();}void SetTint(int,int,int){abort();}};
struct SChainData{CVector field_0;};struct CChain{SChainData*field_4=nullptr;void Move(CVector*){abort();}};
struct CWeb:CBody{int field_F8=0;unsigned short field_100=0,field_102=0;int field_104=0;CVector field_108,field_114;int field_120=0,field_124=0,field_128=0;CKnottedWeb*field_12C=nullptr;CChain*field_130=nullptr;int field_134=0,field_138=0;
CWeb();void Fire(CVector&,CVector&,CBody*,bool,CSVector&);void SetFirePos(CVector&);void SwitchToBlob();void AI();};void*G_WEB_LIST=nullptr;
'''
    code+=controller_method(body,'void CBody::EveryFrame(')+'\n'
    for sig in ['CWeb::CWeb(', 'void CWeb::Fire(', 'void CWeb::SetFirePos(', 'void CWeb::SwitchToBlob(', 'void CWeb::AI(']:code+=controller_method(web,sig)+'\n'
    code+=r'''
void output(CWeb&s){std::cout<<'['<<s.field_104<<','<<s.field_100<<','<<s.mCBodyFlags<<','<<(s.field_12C!=nullptr)<<','<<unsigned(s.mLastUpdateTick)<<','<<s.mElapsedTicks<<','<<s.mAccumulatedTicks;if(s.field_12C){auto&l=*s.field_12C;std::cout<<','<<l.mNumSegs<<','<<l.mStart.vx<<','<<l.mStart.vy<<','<<l.mStart.vz<<','<<l.field_58<<','<<l.field_5C<<','<<l.field_60<<','<<unsigned(l.field_6C)<<','<<unsigned(l.field_6E);for(int i=0;i<l.mNumSegs;i++)std::cout<<','<<l.mSegs[i].End.vx<<','<<l.mSegs[i].End.vy<<','<<l.mSegs[i].End.vz<<','<<l.mpExtraSegs[i].mPos.vx<<','<<l.mpExtraSegs[i].mPos.vy<<','<<l.mpExtraSegs[i].mPos.vz;}std::cout<<']';}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
#endif
for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.;}
CVector hook,target,secondHook;int shot,ticks;unsigned initialClock;
while(std::cin>>hook.vx>>hook.vy>>hook.vz>>target.vx>>target.vy>>target.vz>>secondHook.vx>>secondHook.vy>>secondHook.vz>>G_MIKE_CAMERA[0].Position.vx>>G_MIKE_CAMERA[0].Position.vy>>G_MIKE_CAMERA[0].Position.vz>>shot>>ticks>>initialClock){events.clear();randomIndex=0;player.field_8F8=shot;CWeb s;CSVector normal;std::cout<<'[';s.Fire(hook,target,nullptr,false,normal);output(s);s.SetFirePos(secondHook);std::cout<<',';output(s);s.SwitchToBlob();std::cout<<',';output(s);bool deleted=false;
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0xF8]={};*reinterpret_cast<unsigned short*>(raw+0x46)=0x16;
#endif
for(int i=0;i<12;i++){G_TIMER_RELATED=initialClock+unsigned((i+1)*ticks);if(!deleted){if(s.mCBodyFlags&0x40)deleted=true;else{
#ifdef VALIDATE_ORIGINAL
*reinterpret_cast<unsigned*>(0x6B4CA8)=G_TIMER_RELATED;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x460ED0)(raw);
#endif
s.AI();
#ifdef VALIDATE_ORIGINAL
assert(*reinterpret_cast<int*>(raw+0x7C)==s.mLastUpdateTick&&*reinterpret_cast<int*>(raw+0x80)==s.mElapsedTicks&&*reinterpret_cast<int*>(raw+0x84)==s.mAccumulatedTicks);
#endif
}}std::cout<<',';output(s);}std::cout<<"]\n";}}
'''
    cases=[]
    for delta in [0,79,80,160,640,3200,4000]:
        for shot in [0,1,2,4]:
            for ticks in [0,1,2,6,7,65535]:
                for initial_clock in [500, -6]:
                    cases.append([12345,-54321,67890,12345+(delta<<12),-54321,67890,23456,-65432,78901,500,300,-700,shot,ticks,initial_clock])
    with tempfile.TemporaryDirectory(prefix='openspidey-web-owner-') as d:
        cpp=Path(d)/'reference.cpp';binary=Path(d)/'reference';cpp.write_text(code)
        flags=['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-owner.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} original-source no-target owner Fire/SetFirePos/Blob/AI cases; actual PC EveryFrame verified={bool(args.original_exe)}; Snap/Chain/Trap scope excluded.')

if __name__=='__main__':main()
