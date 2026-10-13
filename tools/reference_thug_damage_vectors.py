"""Compile recovered thug damage/recovery/slide methods and their shared aiming/procedure leaves."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text()
    ai=(root/'src/engine/world/ai.cpp').read_text();utils=(root/'src/core/utils.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "engine/world/stateflags.h"
#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <vector>
struct CThug;using CBaddy=CThug;using CBody=CThug;
struct CAIProc;
std::vector<int>events;int randomValue=0,pathResult=0,addPattern=0,addCalls=0,hitFactor=2,buddyType=0;
int Rnd(int n){events.insert(events.end(),{8,n});return randomValue%n;}
void print_if_false(bool,const char*){}
union Word{unsigned Int=0;unsigned char Bytes[4];};
int thugModes[4][2]={{0x0D0D0100,3},{0x14141C1B,0x114},{0x06061C1B,0x204},{0x14141C1B,0x314}};
int henchModes[4][2]={{0x06040504,0},{0x17171817,0x101},{0x17171817,0x201},{0x01010B0A,0x301}};
#define G_THUG_ANIM_MODES thugModes
#define G_HENCHMAN_ANIM_MODES henchModes
SStateFlags sourceStateFlags[17];SStateFlags*G_THUG_STATE_FLAGS=sourceStateFlags;
struct SHitInfo{u8 field_0=0,field_1=0;int field_4=0;u16 field_8=0;CVector field_C;i16 field_18=0;u16 field_1A=0;};
struct CThug{
 CVector mPos,mVel,mAcc,field_27C;CSVector mAngles,mAngVel,mAngAcc;
 int mType=0,dumbAssPad=0,mAnim=0,mFrame=0,mAnimFinished=0,runFinished=0;
 unsigned mCBodyFlags=0,field_2A8=0,field_218=0,field_3AC=0,field_288=0xffffffff,field_2AC=0;
 int field_318=0,field_330=0,field_310=0,field_1F8=0,field_324=0,field_328=0,field_314=0;
 i16 mHealth=0,field_21E=100;u16 mRMinor=150,mNode=0;u8 field_211=0;
 SStateFlags field_31C{};Word field_294,field_298;CAIProc*mAIProcList=nullptr;
 int Hit(SHitInfo*);void TakeHit();void SlideFromHit(int,int,CVector&);void SetHitDirectionFlag(CVector*);void RunAppropriateHitAnim();
 void Neutralize();void MarkAIProcList(int,int,int);u16 CheckStateFlags(SStateFlags*,int);int SetAnimMode(int,int);int YawTowards(int,int);void SendDeathPulse();
 void RunAnim(unsigned clip,int from,int to){events.insert(events.end(),{2,int(clip),from,to});mAnim=clip;mFrame=from;mAnimFinished=runFinished;}
 void CycleAnim(unsigned clip,int direction){events.insert(events.end(),{3,int(clip),direction});mAnim=clip;mAnimFinished=0;}
 void ClearAttackFlags(){events.push_back(10);}
 CBody*GetClosest(int type,int){events.insert(events.end(),{11,type});return closest(type);}
 static CBody*closest(int);
 int PathCheck(CVector*from,CVector*to,CVector*hit,int clearance){events.insert(events.end(),{4,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,clearance});*hit=*from+((*to-*from)>>2)*hitFactor;return pathResult;}
 int AddPointToPath(CVector*p,int limit){events.insert(events.end(),{5,p->vx,p->vy,p->vz,limit});return (addPattern&(1<<addCalls++))!=0;}
};
CThug player,buddy;CThug*G_MECHLIST_PLAYER=&player;CThug*self=nullptr;
CBody*CThug::closest(int type){return buddyType==type?&buddy:nullptr;}
struct CMessage{CMessage(CThug*,CThug*recipient,int code,void*){events.insert(events.end(),{12,recipient==self?0:1,code});}};
void SFX_PlayPos(unsigned sound,CVector*,int){events.insert(events.end(),{9,int(sound)});}
u16 links[1]={0};u16*Trig_GetLinksPointer(int){return links;}void Trig_SendPulse(u16*){events.push_back(13);}
enum AIProcType{UNK_257=257,UNK_258=258,LOOKAT_TWO=259,AI_TYPE_UNK_20000=0x20000};
struct CAIProc{
 CBaddy*pBaddy=nullptr;CAIProc*mNext=nullptr,*field_18=nullptr;int mAIProcType=0,field_C=0,field_10=0,field_14=0;
 virtual~CAIProc();void AttachProc(AIProcType,CBaddy*,int);int Wait();virtual void Execute(){if(mNext)mNext->Execute();}
};
struct CAIProc_LookAt:CAIProc{
 CVector field_2C;CBody*field_24=nullptr;int field_20=0,field_38=0,field_3C=0;
 CAIProc_LookAt(CBaddy*,CBody*,CVector*,int,int,int);void SetUpVariables(int,int);void Execute();
};
CBody*Mem_MakeHandle(CBody*p){return p;}CBody*Mem_RecoverPointer(CBody**p){return*p;}
int catan(int v){return int(std::atan(v/4096.0)*651.0006103515625);}
'''
    export=(root/'src/compatibility/export.h').read_text();code+=next(l for l in export.splitlines() if l.startswith('#define my_abs'))+'\n'
    for source,signature in [(maths,'i32 M3dMaths_SquareRoot0('),(utils,'int Utils_CalcAim('),
                             (baddy,'void CBaddy::MarkAIProcList('),(baddy,'void CBaddy::Neutralize('),
                             (baddy,'u16 CBaddy::CheckStateFlags('),(baddy,'int CBaddy::YawTowards('),
                             (baddy,'void CBaddy::SendDeathPulse('),(ai,'CAIProc::~CAIProc('),(ai,'void CAIProc::AttachProc('),
                             (ai,'i32 CAIProc::Wait('),(ai,'void CAIProc_LookAt::SetUpVariables('),
                             (ai,'CAIProc_LookAt::CAIProc_LookAt(CBaddy* a2'),(ai,'void CAIProc_LookAt::Execute('),
                             (thug,'i32 CThug::SetAnimMode('),(thug,'void CThug::RunAppropriateHitAnim('),
                             (thug,'void CThug::SetHitDirectionFlag('),(thug,'void CThug::SlideFromHit('),
                             (thug,'void CThug::TakeHit('),(thug,'i32 CThug::Hit(')]:
        method=controller_method(source,signature)
        if signature=='void CBaddy::Neutralize(':method=method.replace('{','{events.push_back(1);',1)
        code+=method+'\n'
    code+=r'''
void emit(CThug&t,int result){
 std::cout<<'['<<result<<','<<t.field_31C.bothFlags<<','<<t.dumbAssPad<<','<<t.mHealth<<','<<t.field_218<<','<<t.field_2A8<<','<<t.mCBodyFlags
 <<','<<t.field_3AC<<','<<t.field_318<<','<<t.field_310<<','<<t.field_1F8<<','<<t.mRMinor<<','<<t.field_324<<','<<t.field_328
 <<','<<t.field_294.Int<<','<<t.field_298.Int<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<int(t.field_211)<<','<<t.field_314
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz
 <<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz<<','<<t.mAngles.vx<<','<<t.mAngles.vy<<','<<t.mAngles.vz
 <<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz<<','<<t.field_330<<','<<t.field_2AC<<','<<t.field_288<<",[";
 int i=0;for(CAIProc*p=t.mAIProcList;p;p=p->mNext){if(i++)std::cout<<',';std::cout<<'['<<p->mAIProcType<<','<<p->field_10<<','<<p->field_14<<']';}std::cout<<"],[";
 for(unsigned j=0;j<events.size();j++){if(j)std::cout<<',';std::cout<<events[j];}std::cout<<"]]";
}
int main(){int states[17][2]={{2,96},{23,64},{24,64},{1,96},{26,11},{27,11},{22,27},{18,1},{14,7},{15,7},{12,1},{17,7},{19,3},{20,3},{13,5},{25,96},{0,19}};
 for(int i=0;i<17;i++){sourceStateFlags[i].flags[0]=states[i][0];sourceStateFlags[i].flags[1]=states[i][1];}
 int mode,health,radius,oldMode,pulse,node;unsigned bodyFlags;SHitInfo hit;
 while(std::cin>>mode){CThug t;self=&t;int lastAttack,playerAnim;
 std::cin>>t.mType>>t.field_31C.bothFlags>>t.dumbAssPad>>health>>t.field_218>>t.field_2A8>>t.mCBodyFlags>>t.field_3AC
 >>t.field_318>>t.field_310>>t.field_1F8>>radius>>t.field_324>>t.field_328>>oldMode>>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished
 >>buddyType>>buddy.field_330>>player.mAnim>>pathResult>>addPattern>>randomValue>>pulse>>node;
 t.mHealth=health;t.mRMinor=radius;t.field_211=pulse;t.mNode=node;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[oldMode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[oldMode][1];
 int hitFlags,damage,slideDistance,slideFrames;std::cin>>hitFlags>>hit.field_4>>damage>>slideDistance>>slideFrames>>hitFactor;
 hit.field_0=hitFlags;hit.field_8=damage;hit.field_18=slideDistance;hit.field_1A=slideFrames;
 std::cin>>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>hit.field_C.vx>>hit.field_C.vy>>hit.field_C.vz
 >>t.mVel.vx>>t.mVel.vy>>t.mVel.vz>>t.mAcc.vx>>t.mAcc.vy>>t.mAcc.vz>>t.field_27C.vx>>t.field_27C.vy>>t.field_27C.vz
 >>t.mAngles.vx>>t.mAngles.vy>>t.mAngles.vz>>t.mAngVel.vx>>t.mAngVel.vy>>t.mAngVel.vz>>t.mAngAcc.vx>>t.mAngAcc.vy>>t.mAngAcc.vz;
 player.mPos=CVector(500000,0,-700000);events.clear();addCalls=0;
 CAIProc*initial[3];int types[3]={257,1794,0x20000};for(int i=0;i<3;i++){initial[i]=new CAIProc();initial[i]->AttachProc(AIProcType(types[i]),&t,0);initial[i]->field_10=i*4;}
 int result=0;
 if(mode==0)result=t.Hit(&hit);else if(mode==1)t.TakeHit();else if(mode==2)t.SlideFromHit(slideDistance,slideFrames,hit.field_C);
 else if(mode==3)t.RunAppropriateHitAnim();else t.SetHitDirectionFlag(&hit.field_C);
 std::cout<<'[';emit(t,result);if(t.mAIProcList)t.mAIProcList->Execute();
 for(CAIProc*p=t.mAIProcList;p;){CAIProc*next=p->mNext;if(p->field_10&1)delete p;p=next;}
 std::cout<<',';emit(t,result);std::cout<<"]\n";while(t.mAIProcList)delete t.mAIProcList;
 }
}
'''
    rng=random.Random(0x4D3F50);cases=[]
    for _ in range(850):
        mode=rng.randrange(5);frames=rng.choice([1,2,4,8,16,31]);dist=rng.choice([16,40,128,-32])
        direction=[rng.choice([-4096,-2048,1024,2048,4096]),rng.choice([-4096,0,4096]),rng.choice([-4096,-2048,1024,2048,4096])]
        cases.append([mode,rng.choice([304,312]),rng.choice([0,1,2,12,17,18,20,23,26,28]),rng.randrange(4) if mode==1 else rng.choice([0,1,2,8]),
                      rng.choice([-32768,-1,0,1,50,32767]),rng.getrandbits(20),rng.getrandbits(32),rng.getrandbits(16),rng.choice([0,1,2]),
                      rng.choice([0,1,3,29]),rng.randint(-400,400),rng.choice([0,1,4,8,16]),rng.choice([0,96,150]),rng.randint(-400,400),rng.randint(-400,400),
                      rng.randrange(4),rng.randrange(44),rng.randrange(50),rng.randrange(2),rng.randrange(2),rng.choice([0,304,312]),rng.choice([0,100,600]),
                      rng.choice([0,106,113,284]),rng.choice([0,1,2,4]),rng.randrange(4),rng.randrange(6),rng.randrange(2),rng.choice([0,16,65535]),
                      rng.randrange(32),rng.choice([0,1,3,15,29]),rng.choice([0,1,50,65535]),dist,frames,rng.choice([1,2,3]),
                      *[rng.randint(-10000000,10000000) for _ in range(3)],*direction,
                      *[rng.randint(-100000,100000) for _ in range(9)],*[rng.randint(-32768,32767) for _ in range(9)]])
    # Repeat direct healthy slides to ensure every source death spin and player-animation branch appears.
    for kind in [0,0x10000,0x20000,0x30000]:
        for playerAnim in [0,106,113,284]:
            for randomValue in [0,1]:
                c=cases[0].copy();c[:34]=[2,312,12,0,0,kind,0x10000010,0x216,0,0,0,0,150,0,0,0,0,0,0,0,0,0,playerAnim,0,3,randomValue,0,16,0,0,0,40,16,2]
                c[34:40]=[0,0,0,4096,0,2048];cases.append(c)
    for actorType in [304,312]:
        for distance in [-32768,32767]:
            for frames in [1,65535]:
                for hitFlags in [4,8,16,30]:
                    c=cases[0].copy();c[:34]=[0,actorType,12,0,50,0,0x10000010,0x216,0,0,0,0,150,0,0,0,0,0,0,0,0,0,106,2,3,1,0,16,hitFlags,1,65535,distance,frames,2]
                    c[34:40]=[0,0,0,4096,0,2048];cases.append(c)
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-damage-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-fpermissive','-w','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode())
    output=[dict(input=c,expected=json.loads(e)) for c,e in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-damage.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} recovered damage cases with before/after shared procedure execution snapshots.')


if __name__=='__main__':main()
