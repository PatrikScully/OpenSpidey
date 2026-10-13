"""Compile original backpedal/grab/yank/web-throw/trap/acknowledgement state machines."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_injury_vectors import recovered_harness


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text();ai=(root/'src/engine/world/ai.cpp').read_text()
    code=recovered_harness(root)
    code=code.replace('#include <algorithm>','#include <algorithm>\n#include "core/fixed_vector.h"')
    code=code.replace('struct CVector{int vx=0,vy=0,vz=0;};struct CSVector{i16 vx=11,vy=17,vz=13;};','')
    code=code.replace('mHealth=0,','').replace('field_1A8[8]', 'field_1A8[64]')
    code=code.replace('struct CBaddy{',r'''
struct SHitInfo{unsigned field_0=0;int field_4=0;};struct SHandle{void*pWhatever=nullptr;};struct SStateFlags{int bothFlags=0;};
CBaddy*buddy304=nullptr,*buddy312=nullptr,*trackedSelf=nullptr;int G_ATTACK_RELATED=0,grabResult=0,webBurst=0;
struct WebInfo{int mNumSegs=0;};struct CTrapWebEffect{WebInfo*field_44=nullptr;void Burst(){events.push_back(24);webBurst=1;}};
struct CBaddy{''')
    code=code.replace('struct{int bothFlags=0;}field_31C;', 'SStateFlags field_31C;')
    code=code.replace('PROC_FALL_TYPE=770','PROC_FALL_TYPE=770,STATE_SWITCH_SEND_MESSAGE=0x60601')
    code=code.replace('bool IsDead(){',r'''
 short mHealth=0;int mActionState=0,field_350=0,field_368=0,field_364=0,field_398=0,field_39C=0,field_33C=0,field_330=0;
 int field_528=0,field_534=0,field_52C=0;CVector*field_3A4=nullptr;
 CTrapWebEffect*field_104=nullptr;SHandle mHandleTwo;
 void BackpedalPlease();void GettingGrabbed();void GetYankedBySpidey();void GetWhippedLikeTheWhoreYouAre();void GetTrapped();void Acknowledge();int YawTowards(int,int);
 void SetAttacker(){events.push_back(21);}
 CBaddy*GetClosest(int type,int flag){events.insert(events.end(),{20,type,flag});return type==304?buddy304:buddy312;}
 int GrabUpdate(CVector*p,i16*yaw){events.push_back(28);p->vx+=11;p->vy-=23;p->vz+=17;*yaw+=7;return grabResult;}
 int Hit(SHitInfo*h){events.insert(events.end(),{25,mType,int(h->field_0),h->field_4,trackedSelf->mPos.vx,trackedSelf->mPos.vy,trackedSelf->mPos.vz});return 0;}
 int PathCheck(CVector*from,CVector*to,CVector*,int clearance){events.insert(events.end(),{29,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,clearance});return pathResult;}
 int AddPointToPath(CVector*p,int a){events.insert(events.end(),{30,p->vx,p->vy,p->vz,a});int result=addResults[std::min(addIndex++,2)];if(result)field_1A8[++field_1F0]=*p;return result;}
 bool IsDead(){''')
    # Remove the original attack boundary overloads now replaced by reaction path instrumentation.
    begin=code.rindex(' int PathCheck(CVector*from,CVector*to,CVector*,int clearance)')
    end=code.index('\n};',begin)
    old=code[begin:end]
    # This segment ends with the original AddPointToPath stub; only remove those two methods.
    code=code[:begin]+code[end:]
    code=code.replace('int randomValue=0,pathResult=0,addResult=0,distanceIndex=0;int distances[4]={};',
                      'int randomValue=0,pathResult=0,addResult=0,distanceIndex=0,addIndex=0;int distances[4]={},addResults[3]={};')
    code+=r'''
using CThug=CBaddy;using CPlayer=CBaddy;
CTrapWebEffect*Mem_RecoverPointer(CTrapWebEffect**p){events.push_back(22);return *p;}
CBaddy*Mem_RecoverPointer(SHandle*p){events.push_back(23);return static_cast<CBaddy*>(p->pWhatever);}
void Redbook_XAPlayPos(int bank,int clip,CVector*p,int attenuation){events.insert(events.end(),{27,bank,clip,p->vx,p->vy,p->vz,attenuation});}
struct CMessage{CMessage(CBaddy*,CBaddy*target,int code,void*){events.insert(events.end(),{26,target->mType,code});}};
struct CAIProc_StateSwitchSendMessage:CAIProc{SStateFlags mFlags;int field_24=0;CAIProc_StateSwitchSendMessage(CBaddy*,int);};
'''
    for source,signature in [(ai,'CAIProc_StateSwitchSendMessage::CAIProc_StateSwitchSendMessage('),(baddy,'int CBaddy::YawTowards('),
                             (thug,'void CThug::BackpedalPlease('),(thug,'void CThug::GettingGrabbed('),(thug,'void CThug::GetYankedBySpidey('),
                             (thug,'void CThug::GetWhippedLikeTheWhoreYouAre('),(thug,'void CThug::GetTrapped('),(thug,'void CThug::Acknowledge(')]:
        code+=controller_method(source,signature).replace('CThug::','CBaddy::')+'\n'
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();randomIndex=distanceIndex=addIndex=webBurst=0;CBaddy t,p,collision,b1,b2,partner;trackedSelf=&t;G_MECHLIST_PLAYER=&p;int mode,flags,has1,has2,hasWeb,strands,hasPartner,type;
 std::cin>>t.mType>>t.field_31C.bothFlags>>t.dumbAssPad>>t.mHealth>>t.field_1F8>>t.field_368>>t.field_364>>t.field_350>>t.field_33C>>t.field_398>>t.field_39C
 >>t.mElapsedTicks>>t.mCBodyFlags>>t.field_2A8>>t.field_288>>t.field_218>>t.field_3AC>>t.field_310>>t.field_308>>t.field_1F0
 >>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished>>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>t.mVel.vx>>t.mVel.vy>>t.mVel.vz
 >>t.mAngles.vx>>t.mAngles.vy>>t.mAngles.vz>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>p.mAngles.vy>>p.mAnim>>p.mActionState
 >>t.field_1A8[t.field_1F0].vx>>t.field_1A8[t.field_1F0].vy>>t.field_1A8[t.field_1F0].vz
 >>G_ATTACK_RELATED>>groundResult>>physicsCounter>>type>>pathResult>>addResults[0]>>addResults[1]>>addResults[2]>>grabResult
 >>has1>>has2>>b1.field_330>>b2.field_330>>hasWeb>>strands>>p.field_528>>hasPartner>>randomValues[0]>>randomValues[1]>>randomValues[2]>>flags>>mode;
 t.mAcc={11,17,13};t.field_27C={11,17,13};t.mAngVel={11,17,13};t.mAngAcc={11,-23,13};b1.mType=304;b2.mType=312;
 buddy304=has1?&b1:nullptr;buddy312=has2?&b2:nullptr;t.mHandleTwo.pWhatever=hasPartner?&partner:nullptr;
 collision.mType=type;collisionBody=type<0?nullptr:&collision;WebInfo info;info.mNumSegs=strands;CTrapWebEffect web;web.field_44=&info;t.field_104=hasWeb?&web:nullptr;
 CVector throwPoints[8];for(int i=0;i<8;i++)throwPoints[i]={409600*i,100000*i,-819200*i};t.field_3A4=throwPoints;
 CAIProc look,move,monitor;look.mAIProcType=257;move.mAIProcType=1794;monitor.mAIProcType=0x20501;look.field_10=move.field_10=monitor.field_10=flags;
 monitor.mNext=&move;move.mNext=&look;t.mAIProcList=&monitor;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 if(op==0)t.BackpedalPlease();else if(op==1)t.GettingGrabbed();else if(op==2)t.GetYankedBySpidey();else if(op==3)t.GetWhippedLikeTheWhoreYouAre();else if(op==4)t.GetTrapped();else t.Acknowledge();
 std::cout<<'['<<t.field_31C.bothFlags<<','<<t.dumbAssPad<<','<<t.mHealth<<','<<t.field_1F8<<','<<t.field_368<<','<<t.field_364<<','<<t.field_350<<','<<t.field_33C
 <<','<<t.mCBodyFlags<<','<<t.field_2A8<<','<<t.field_288<<','<<t.field_218<<','<<t.field_3AC<<','<<t.field_310<<','<<t.field_308<<','<<t.field_1F0
 <<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.shadow<<','<<webBurst<<','<<p.field_534<<','<<p.field_52C
 <<','<<t.field_2FC.vx<<','<<t.field_2FC.vy<<','<<t.field_2FC.vz<<','<<t.mPos.vx<<','<<t.mPos.vy<<','<<t.mPos.vz
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz<<','<<t.mAngles.vx<<','<<t.mAngles.vy<<','<<t.mAngles.vz
 <<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz<<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz
 <<",[";bool first=true;for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(!first)std::cout<<',';first=false;std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**changes):
        base=dict(op=0,kind=304,state=13,sub=0,health=90,counter=1,delay=1,work=0,osc=0,conversation=0,clip=-3,bank=4,elapsed=2,
                  body=0x216,behavior=0xffffffff,complete=0,combat=0,action=0,turn=100,landing=1000000,index=1,anim=0,frame=0,finished=1,runfinished=0,
                  x=0,y=0,z=0,vx=32768,vy=49152,vz=-32768,angx=10,angy=100,angz=20,px=4096000,py=0,pz=8192000,pyaw=2048,panim=0,paction=0,
                  tx=409600,ty=0,tz=-819200,clock=16,ground=409600,physics=0,target=-1,path=0,add0=1,add1=1,add2=1,grab=0,
                  b1=1,b2=1,alert1=0,alert2=0,web=1,strands=79,combo=1,partner=1,r0=1,r1=1,r2=1,flags=0,mode=0)
        base.update(changes);return list(base.values())
    cases=[]
    for operation,states,substates in [(0,[13],[0,1]),(1,[16],[0,1,2]),(2,[14,15],[0,1,2,3,9,10,11]),(3,[18],[0,1,2,3,4]),(4,[19],[0,1,2,3]),(5,[21],[0,1,2])]:
        for kind in [304,312]:
            for state in states:
                for sub in substates:
                    for counter in [0,1,2,7]:
                        for done in [0,1]:cases.append(case(op=operation,kind=kind,state=state,sub=sub,counter=counter,finished=done))
    for strands in [0,1,79,80,81,200]:
        for work in [0,79,80,81]:
            for delay in [-2147483648,-1,0,1,2,2147483647]:cases.append(case(op=4,state=19,sub=2,strands=strands,work=work,delay=delay,counter=1))
    for osc in [-2147483648,-1,0,1,39,40,41,2147483647]:
        for elapsed in [-1,0,1,2,40,2147483647]:
            for flag in [0,0x20]:cases.append(case(op=2,state=14,sub=1,osc=osc,elapsed=elapsed,combat=flag,physics=3))
    rng=random.Random(0x4D96D0)
    for _ in range(1500):
        operation=rng.randrange(6);sub=rng.choice([[0,1],[0,1,2],[0,1,2,3,9,10,11],[0,1,2,3,4],[0,1,2,3]][operation]if operation<5 else [0,1,2])
        cases.append(case(op=operation,kind=rng.choice([304,312]),state=rng.choice([14,15])if operation==2 else [13,16,14,18,19,21][operation],sub=sub,
                          health=rng.choice([-32768,-1,0,50,51,90,32767]),counter=rng.randrange(8)if operation==3 and sub==1 else rng.choice([-2147483648,-1,0,1,2,7,2147483647]),
                          delay=rng.choice([-2147483648,-1,0,1,2,20,2147483647]),work=rng.choice([0,79,80,200]),osc=rng.choice([-1,0,1,39,40,41]),
                          conversation=rng.choice([0,1,2000]),clip=rng.choice([-2147483648,-3,0,4]),elapsed=rng.choice([-1,0,1,2,40,2147483647]),
                          body=rng.getrandbits(32),behavior=rng.getrandbits(32),complete=rng.choice([0,2]),combat=rng.getrandbits(32),
                          index=rng.choice([0,1,5]),anim=rng.choice([0,27,28,30,31,33,38]),finished=rng.choice([0,1,255]),runfinished=rng.randrange(2),
                          x=rng.randint(-1000000,1000000),y=rng.randint(-1000000,1000000),z=rng.randint(-1000000,1000000),
                          angx=rng.randint(-32768,32767),angy=rng.randint(-32768,32767),pyaw=rng.randint(-32768,32767),panim=rng.choice([0,124]),paction=rng.choice([0,0x8000000]),
                          tx=rng.randint(-1000000,1000000),tz=rng.randint(-1000000,1000000),clock=rng.randrange(16),ground=rng.choice([-1,409600,1000000,2000000]),
                          physics=rng.choice([-1,0,1,2,3]),target=rng.choice([-1,0,50,304,312]),path=rng.choice([-1,0,1,2,4]),
                          add0=rng.randrange(2),add1=rng.randrange(2),add2=rng.randrange(2),grab=rng.randrange(2),b1=rng.randrange(2),b2=rng.randrange(2),
                          alert1=rng.randrange(2),alert2=rng.randrange(2),web=rng.randrange(2),strands=rng.choice([0,79,80,81]),
                          combo=rng.choice([-2147483648,-1,0,1,2147483647]),partner=rng.randrange(2),r0=rng.randrange(3),flags=rng.randrange(8),mode=rng.randrange(4)))
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-reaction-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-reaction.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} thug grab/yank/web/trap/ack cases.')


if __name__=='__main__':main()
