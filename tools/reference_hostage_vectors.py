"""Compile CHostage AI/constructor/animation leaves. Numerical death flash/fade also matches PC0x442650."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;source=(root/'src/game/actors/hostage.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct CHostage;using CBaddy=CHostage;using CBody=CHostage;
struct SHandle{int id=0;void*pWhatever=nullptr;};struct CMessage;
int G_DIFFICULTY_LEVEL=0,G_ATTACK_RELATED=0;unsigned G_VBLANKS=0;int currentBank=-1,currentSlot=-1,attachedBank=0,attachedSlot=0;unsigned attachedClock=0;
unsigned randomState;std::vector<int>events;int ground=0;bool nextWaypoint=false,dieResult=false,speechAccepted=false,shooterPresent=false,closestPresent=false;
int Rnd(int n){events.insert(events.end(),{13,n});randomState=randomState*1664525u+1013904223u;return n?randomState%unsigned(n):0;}
union AnimWord{unsigned Int=0;unsigned char Bytes[4];};
struct Proc{void Execute(){};};
struct CHostage{int id=0,mType=0;CVector mPos,mAcc,mVel,mAngVel,mAngAcc,field_27C,mShadowPos;CSVector mAngles;
 unsigned mCBodyFlags=0x216,field_2A8=0,field_288=0,field_2AC=0,field_218=0,field_2F0=0,mRGB=0;unsigned short mFlags=0,mInputFlags=0,mRMinor=128,mAnim=0;unsigned char mAnimFinished=0;
 short field_21E=0,field_216=32;int mTRN=0,mShadowScale=48,mPushVal=64,field_324=0,dumbAssPad=0,field_328=0,field_230=0,field_1F4=0,mNode=0,field_2A0=0,field_29C=0,field_208=0,field_204=0;
 AnimWord field_294,field_298;SHandle field_32C;CMessage*pMessage=nullptr;Proc*mAIProcList=nullptr;CHostage*mNextItem=nullptr;int procCount=0;
 CHostage(){}CHostage(short*,int);void AI();void DieHostage();void FollowWaypoints();void SetHostageType(int);void BegMotherfucker();void HostageXAPlay(int,int,int);void TellSomebodyToShootMe();void CheckIfFreed();void WaitForPlayer();void DisappearBitch();void GetUp();void RunAppropriateAnim();CBaddy*GetClosest(int,int);int DistanceToPlayer(int);void Neutralize();
 void DoPhysics(int mode){events.insert(events.end(),{1,mode});}void CycleAnim(int clip,int dir){events.insert(events.end(),{4,clip,dir});mAnim=clip;mAnimFinished=0;}void RunAnim(int clip,int from,int to){events.insert(events.end(),{5,clip,from,to});mAnim=clip;mAnimFinished=0;}
 int SetHeight(int initial,int above,int below){events.insert(events.end(),{6,initial,above,below});return 0;}bool GetNextWaypoint(){events.push_back(7);if(nextWaypoint){field_1F4++;field_2F0=0;}return nextWaypoint;}
 void Baddy_SendSignal(){events.push_back(9);}void AttachXA(int bank,int slot){attachedClock=G_VBLANKS;attachedBank=bank;attachedSlot=slot;}
 void StopMyXA(){if(unsigned(G_VBLANKS-attachedClock)<300&&attachedBank==currentBank&&attachedSlot==currentSlot)events.push_back(10);}
 void KillShadow(){mCBodyFlags&=~8u;events.push_back(11);}bool Die(int stage){events.insert(events.end(),{14,stage});return dieResult;}
 void MarkAIProcList(int,int,int){}void CleanUpAIProcList(int){}void CleanUpMessages(int,int);
 short*SquirtPos(short*);short*SquirtAngles(short*);void AttachTo(CBody**){events.push_back(15);}void ShadowOn(){mCBodyFlags|=8;events.push_back(16);}void ParseScript(unsigned short*){events.push_back(17);}
 void InitItem(const char*name){events.insert(events.end(),{18,!std::strcmp(name,"hostage")?305:315});}
};
CHostage player,shooter,candidates[2];CHostage*G_MECHLIST_PLAYER=&player,*G_BADDY_LIST=nullptr;
struct CMessage{int field_14=0,field_10=0,field_40=0;SHandle mHandle;CMessage*mNext=nullptr;CMessage(){}CMessage(CHostage*from,CHostage*target,int code,int){events.insert(events.end(),{12,target->id,code});field_14=code;mHandle.id=from->id;mNext=target->pMessage;target->pMessage=this;}};
void CHostage::CleanUpMessages(int,int){CMessage*prev=nullptr;for(CMessage*p=pMessage;p;){auto next=p->mNext;if((p->field_10&1)||p->field_14==0){if(prev)prev->mNext=next;else pMessage=next;}else prev=p;p=next;}}
void M3d_BuildTransform(CHostage*){events.push_back(2);}
int Utils_GetGroundHeight(CVector*p,int above,int below,void*){events.insert(events.end(),{3,p->vx,p->vy,p->vz,above,below});return ground;}
CVector waypoint(409600,0,409600);void Trig_GetPosition(CVector*p,int node){events.insert(events.end(),{19,node});*p=waypoint;}
int Redbook_XAPlayPos(int bank,int slot,CVector*p,int attenuation){events.insert(events.end(),{8,bank,slot,p->vx,p->vy,p->vz,attenuation});return speechAccepted;}
void*Mem_RecoverPointer(SHandle*h){events.insert(events.end(),{20,h->id});return shooterPresent?&shooter:nullptr;}
int Utils_LineOfSight(CVector*,CVector*,int,int){return 1;}
void print_if_false(int,const char*){events.push_back(21);}
struct SMoveToInfo{CVector field_0;int field_C=0,field_10=0,field_14=0;};
struct CAIProc_MoveTo{CAIProc_MoveTo(CHostage*h,SMoveToInfo*,int mask){h->field_288&=~unsigned(mask);h->field_288&=~0x10000u;h->field_2A8&=~0x40000000u;h->procCount+=2;}};
struct CAIProc_LookAt{CAIProc_LookAt(CHostage*h,CBody*,CVector*,int mask,int,int){h->field_288&=~unsigned(mask);h->field_2A8&=~0x40000000u;h->procCount++;}};
int gHostageXaSubIds[5]={0,1,2,4,12},gMaleHostageOne[2]={0x03030404,4},gFemaleHostageOne[2]={0x04040504,3};
'''
    code+='\n#define ASSERT(...) ((void)0)\n'
    for sig in ['i16* CBody::SquirtPos(', 'i16* CBody::SquirtAngles(']:code+=controller_method((root/'src/engine/world/ob.cpp').read_text(),sig)+'\n'
    for sig in ['i32 CBaddy::DistanceToPlayer(', 'CBaddy* CBaddy::GetClosest(', 'void CBaddy::RunAppropriateAnim(', 'void CBaddy::Neutralize(']:code+=controller_method(baddy,sig)+'\n'
    for sig in ['void CHostage::AI(', 'void CHostage::DieHostage(', 'void CHostage::FollowWaypoints(', 'void CHostage::SetHostageType(', 'void CHostage::BegMotherfucker(', 'void CHostage::HostageXAPlay(', 'void CHostage::TellSomebodyToShootMe(', 'void CHostage::CheckIfFreed(', 'void CHostage::WaitForPlayer(', 'void CHostage::DisappearBitch(', 'void CHostage::GetUp(', 'CHostage::CHostage(']:code+=controller_method(source,sig)+'\n'
    code+=r'''
#ifdef VALIDATE_ORIGINAL
void patchJump(unsigned address,void*function){auto p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(function)-address-5;}
void __attribute__((thiscall)) originalDie(void*,int stage){events.insert(events.end(),{14,stage});}
#endif
void vector(CVector p){std::cout<<p.vx<<','<<p.vy<<','<<p.vz;}
void emit(CHostage&h){std::cout<<'['<<h.field_324<<','<<h.dumbAssPad<<','<<h.field_230<<','<<h.field_328<<','<<h.field_218<<','<<h.field_2A8<<','<<h.field_288<<','<<h.mCBodyFlags<<','<<h.mFlags<<','<<h.mRGB<<','<<h.mTRN<<','<<h.mRMinor<<','<<h.field_1F4<<','<<h.field_2A0<<','<<h.field_29C<<','<<h.field_208<<','<<h.field_204<<','<<h.field_32C.id<<','<<h.procCount<<','<<h.mAnim<<','<<int(h.mAnimFinished)<<','<<attachedClock<<','<<attachedBank<<','<<attachedSlot<<',';vector(h.mPos);std::cout<<',';vector(h.mShadowPos);std::cout<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"],[";int i=0;for(auto p=h.pMessage;p;p=p->mNext){if(i++)std::cout<<',';std::cout<<'['<<p->field_14<<','<<p->field_40<<','<<p->field_10<<','<<p->mHandle.id<<']';}std::cout<<"]]";}
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patchJump(0x404320,reinterpret_cast<void*>(&originalDie));
#endif
int mode;while(std::cin>>mode){CHostage h;h.field_294.Int=gMaleHostageOne[0];h.field_298.Int=4;h.field_21E=100;int type,bodyFlags,itemFlags,inputFlags,anim,finished,groundValue,next,accepted,hasShooter,hasClosest;unsigned behavior,completion;int code,age,msgFlags,handle,die;
std::cin>>type>>h.field_324>>h.dumbAssPad>>h.field_230>>h.field_328>>h.field_218>>behavior>>completion>>bodyFlags>>itemFlags>>inputFlags>>groundValue>>anim>>finished>>G_DIFFICULTY_LEVEL
>>player.mPos.vx>>player.mPos.vy>>player.mPos.vz>>h.mPos.vx>>h.mPos.vy>>h.mPos.vz>>h.field_29C>>h.field_2A0>>h.field_208>>h.field_204>>G_ATTACK_RELATED
>>G_VBLANKS>>attachedClock>>attachedBank>>attachedSlot>>currentBank>>currentSlot>>h.field_2F0>>next>>accepted>>hasShooter>>hasClosest>>randomState>>h.field_1F4
>>code>>age>>msgFlags>>handle>>die;dieResult=die;
h.mType=type;if(type==315){h.field_294.Int=gFemaleHostageOne[0];h.field_298.Int=3;}h.field_2A8=behavior;h.field_288=completion;h.mCBodyFlags=bodyFlags;h.mFlags=itemFlags;h.mInputFlags=inputFlags;h.mAnim=anim;h.mAnimFinished=finished;ground=groundValue;nextWaypoint=next;speechAccepted=accepted;shooterPresent=hasShooter;closestPresent=hasClosest;
h.field_32C.id=handle;h.field_32C.pWhatever=&shooter;CMessage initial;initial.field_14=code;initial.field_40=age;initial.field_10=msgFlags;initial.mHandle.id=handle;initial.mHandle.pWhatever=&shooter;h.pMessage=code>=0?&initial:nullptr;
G_BADDY_LIST=&h;h.mNextItem=&candidates[0];candidates[0].id=1;candidates[0].mType=hasClosest?304:312;candidates[0].mPos=h.mPos+CVector(409600,0,0);candidates[0].mNextItem=&candidates[1];candidates[1].id=2;candidates[1].mType=hasClosest?304:312;candidates[1].mPos=h.mPos+CVector(204800,0,0);candidates[1].mNextItem=nullptr;
if(mode==2){alignas(4)short placement[10]={};std::memcpy(placement,&h.mPos,12);placement[6]=123;placement[7]=-456;placement[8]=789;events.clear();CHostage fresh(placement,h.field_1F4);fresh.SetHostageType(type);h=fresh;std::cout<<'[';emit(h);std::cout<<"]\n";continue;}
std::cout<<'[';int frames=mode==1?32:1;for(int frame=0;frame<frames;frame++){events.clear();
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x334]={};if(mode==1&&h.field_324==6&&h.dumbAssPad>=1){*reinterpret_cast<int*>(raw+0x320)=h.dumbAssPad;*reinterpret_cast<int*>(raw+0x328)=h.field_328;*reinterpret_cast<unsigned*>(raw+0x24)=h.mRGB;}
#endif
h.AI();if(frame)std::cout<<',';emit(h);
#ifdef VALIDATE_ORIGINAL
// The flash/fade numerical cases are compared directly; initialization/world/animation/speech are source-host boundaries.
if(mode==1&&*reinterpret_cast<int*>(raw+0x320)>=1){reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x442650)(raw);assert(*reinterpret_cast<int*>(raw+0x320)==h.dumbAssPad&&*reinterpret_cast<int*>(raw+0x328)==h.field_328&&*reinterpret_cast<unsigned*>(raw+0x24)==h.mRGB);}
#endif
G_ATTACK_RELATED++;G_VBLANKS+=2;}
std::cout<<"]\n";}}
'''
    rng=random.Random(0x442D10);cases=[]
    for state in range(7):
        for sub in range(4):
            for kind in [305,315]:
                for _ in range(4):
                    cases.append([0,kind,state,sub,rng.choice([-1,0,1,15,120]),rng.choice([0,7,8,19,20]),rng.randrange(4),rng.choice([1,0x1000,0x40000001]),rng.choice([0,1,0x10001]),rng.choice([0x216,0x21E,0xffff]),rng.randrange(4096),rng.randrange(2),rng.choice([-1,1000000]),rng.randrange(12),rng.randrange(2),rng.randrange(4),
                        *rng.choice([(0,0,0),(409600,0,0),(10000000,0,0)]),0,0,0,0,0,rng.choice([0,1,100]),rng.choice([0,1000]),100,100,0,7,10,rng.choice([-1,7]),rng.choice([-1,10]),rng.choice([0,8]),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(2**32),5,rng.choice([-1,0,1,2,3,11]),rng.choice([-60,0,14,15,16]),rng.randrange(2),17])
    for kind in [305,315]:cases.append([1,kind,6,0,0,0,0,1,0,0x216,0,0,-1,0,0,2,0,0,0,0,0,0,0,0,0,0,100,100,0,7,10,7,10,0,0,1,1,1,0,5,-1,0,0,17])
    for kind in [305,315]:
        for node in [0,5,65535]:
            cases.append([2,kind,0,0,0,0,0,0,0,0x216,0,0,-1,0,0,0,0,0,0,123456,-345678,567890,0,0,0,0,0,0,0,0,0,-1,-1,0,0,0,0,0,0,node,-1,0,0,0])
    for case in cases:case.append(rng.randrange(2))
    with tempfile.TemporaryDirectory(prefix='openspidey-hostage-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/hostage.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} hostage behavior cases; long death sequences use original PC flash/fade proof when requested.')

if __name__=='__main__':main()
