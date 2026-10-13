"""Compile extracted AttackPlayer/TooCloseToSpidey and shared timer/procedure methods."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def recovered_harness(root):
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text();ai=(root/'src/engine/world/ai.cpp').read_text()
    code=r'''
#include <algorithm>
#include <cstdint>
#include <cstdlib>
#include <iostream>
#include <vector>
using i32=int32_t;using i16=int16_t;using u8=uint8_t;
struct CVector{int vx=0,vy=0,vz=0;};struct CSVector{i16 vx=11,vy=17,vz=13;};
union Word{unsigned Int=0;unsigned char Bytes[4];};
std::vector<int>events;int randomValue=0,pathResult=0,addResult=0,distanceIndex=0;int distances[4]={};
int Rnd(int n){events.insert(events.end(),{8,n});return randomValue%n;}
int my_abs(int a){return a<0?-a:a;}
void print_if_false(bool,const char*){}
int thugModes[4][2]={{0x0D0D0100,3},{0x14141C1B,0x114},{0x06061C1B,0x204},{0x14141C1B,0x314}};
int henchModes[4][2]={{0x06040504,0},{0x17171817,0x101},{0x17171817,0x201},{0x01010B0A,0x301}};
#define G_THUG_ANIM_MODES thugModes
#define G_HENCHMAN_ANIM_MODES henchModes
struct CBaddy;using CBody=CBaddy;
CBaddy*G_MECHLIST_PLAYER=nullptr;
enum AIProcType{UNK_257=257,UNK_258=258,MONITOR_ATTACK=1281,AI_TYPE_UNK_20000=0x20000};
struct CAIProc{
 CBaddy*pBaddy=nullptr;CAIProc*mNext=nullptr,*field_18=nullptr;int mAIProcType=0,field_10=0,field_14=0;
 void AttachProc(AIProcType,CBaddy*,int);
};
struct CBaddy{
 int mType=0,dumbAssPad=0,mHealth=0,mAnim=0,mFrame=0,mAnimFinished=0,mElapsedTicks=0,field_1F8=0,field_230=0,field_310=0,field_1F0=0;
 unsigned mCBodyFlags=0,field_2A8=0,field_288=0;struct{int bothFlags=0;}field_31C;Word field_294,field_298;
 CVector mPos,field_1A8[8],mVel,mAcc,field_27C;CSVector mAngVel,mAngAcc;
 CAIProc*mAIProcList=nullptr;int runFinished=0;
 int RunTimer(int*);void MarkAIProcList(int,int,int);void Neutralize();int SetAnimMode(int,int);void AttackPlayer();int TooCloseToSpidey();
 void RunAnim(unsigned c,int from,int to){events.insert(events.end(),{2,int(c),from,to});mAnim=c;mFrame=from;mAnimFinished=runFinished;}
 void CycleAnim(unsigned c,int dir){events.insert(events.end(),{3,int(c),dir});mAnim=c;mAnimFinished=0;}
 int DistanceToPlayer(int tolerance){int value=distances[std::min(distanceIndex++,3)];events.insert(events.end(),{7,tolerance,value});return value;}
 int PathCheck(CVector*from,CVector*to,CVector*,int clearance){events.insert(events.end(),{4,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,clearance});return pathResult;}
 int AddPointToPath(CVector*p,int arg){events.insert(events.end(),{5,p->vx,p->vy,p->vz,arg});if(addResult)field_1A8[++field_1F0]=*p;return addResult;}
};
void SFX_PlayPos(unsigned sound,CVector*,int){events.insert(events.end(),{9,int(sound)});}
CBody*Mem_MakeHandle(CBody*p){return p;}
void*DCMem_New(int n,int,int,int,int){return calloc(n?n:1,1);}
struct CAIProc_LookAt:CAIProc{
 CVector field_2C;CBody*field_24=nullptr;int field_38=0,field_3C=0;
 CAIProc_LookAt(CBaddy*,CBody*,CVector*,int,int,int);void SetUpVariables(int,int);
};
struct CAIProc_MonitorAttack:CAIProc{
 int field_20=0,field_24=0,field_28=0,field_2C=0;CVector*field_30=nullptr;
 CAIProc_MonitorAttack(CBaddy*,int,int,int,int);
};
'''
    for source,signature in [(ai,'void CAIProc::AttachProc('),(baddy,'void CBaddy::MarkAIProcList('),
                            (ai,'void CAIProc_LookAt::SetUpVariables('),(ai,'CAIProc_LookAt::CAIProc_LookAt(CBaddy* a2'),
                            (ai,'CAIProc_MonitorAttack::CAIProc_MonitorAttack('),(baddy,'void CBaddy::Neutralize('),
                            (baddy,'int CBaddy::RunTimer('),(thug,'i32 CThug::SetAnimMode('),
                            (thug,'i32 CThug::TooCloseToSpidey('),(thug,'void CThug::AttackPlayer(')]:
        method=controller_method(source,signature).replace('CThug::','CBaddy::')
        if signature=='void CBaddy::Neutralize(':method=method.replace('{','{events.push_back(1);',1)
        if signature=='CAIProc_MonitorAttack::CAIProc_MonitorAttack(':method=method.replace('{','{events.insert(events.end(),{6,a3,a4,a5,a6});',1)
        code+=method+'\n'
    return code


def main():
    root=Path(__file__).resolve().parent.parent
    code=recovered_harness(root)
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();distanceIndex=0;CBaddy t,p;G_MECHLIST_PLAYER=&p;int mode,yawVel,yawAcc,oldFlags;
 std::cin>>t.mType>>t.dumbAssPad>>t.field_31C.bothFlags>>t.field_1F8>>t.field_230>>t.mElapsedTicks
 >>t.mCBodyFlags>>t.field_2A8>>t.field_288>>t.field_310>>t.field_1F0>>mode>>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished>>p.mHealth
 >>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz
 >>t.field_1A8[t.field_1F0].vx>>t.field_1A8[t.field_1F0].vy>>t.field_1A8[t.field_1F0].vz
 >>yawVel>>yawAcc>>distances[0]>>distances[1]>>distances[2]>>distances[3]>>randomValue>>pathResult>>addResult>>oldFlags;
 t.mAngVel.vy=yawVel;t.mAngAcc.vy=yawAcc;t.mVel={11,17,13};t.mAcc={-7,-23,5};t.field_27C={11,17,13};
 CAIProc look,move,monitor;look.mAIProcType=257;move.mAIProcType=1794;monitor.mAIProcType=0x20501;
 look.field_10=move.field_10=monitor.field_10=oldFlags;monitor.mNext=&move;move.mNext=&look;t.mAIProcList=&monitor;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 int result=0;if(op)result=t.TooCloseToSpidey();else t.AttackPlayer();
 std::cout<<'['<<result<<','<<t.dumbAssPad<<','<<t.field_31C.bothFlags<<','<<t.field_1F8<<','<<t.field_230<<','<<t.mCBodyFlags<<','<<t.field_2A8<<','<<t.field_288<<','<<t.field_310<<','<<t.field_1F0
 <<','<<t.field_294.Int<<','<<t.field_298.Int<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.mPos.vx<<','<<t.mPos.vy<<','<<t.mPos.vz
 <<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz<<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz
 <<",[";bool first=true;for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(!first)std::cout<<',';first=false;std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**overrides):
        base=dict(op=0,kind=304,sub=2,state=5,counter=120,pause=0,elapsed=2,body=0x216,behavior=0xffffffff,complete=2,turn=0,index=0,mode=0,
                  anim=5,frame=4,finished=1,runfinished=0,health=100,x=409600,y=0,z=-819200,px=0,py=0,pz=0,
                  tx=-409600,ty=123,tz=819200,angvel=17,angacc=-23,d0=100,d1=100,d2=100,d3=100,random=1,path=0,add=1,flags=0)
        base.update(overrides);return list(base.values())
    cases=[]
    for kind in [304,312]:
        for sub in range(7):
            for dist in [0,104,105,119,120,139,140,161,162,189,190]:
                for done in [0,1]:
                    for randomValue in [0,1]:cases.append(case(kind=kind,sub=sub,d0=dist,d1=dist,d2=dist,d3=dist,finished=done,random=randomValue))
    for counter in [-2147483648,-1,0,1,2,120,2147483647]:
        for elapsed in [-2147483648,-1,0,1,2,120,2147483647]:cases.append(case(counter=counter,elapsed=elapsed))
    for kind in [304,312]:
        for path in [-1,0,1,2,4]:
            for add in [0,1]:
                for d0 in [-1,0,104,105,161,162]:
                    for op in [0,1]:cases.append(case(op=op,kind=kind,sub=3,path=path,add=add,d0=d0,d1=125,d2=95,d3=135))
    rng=random.Random(0x4D5540)
    for _ in range(1500):
        cases.append(case(op=rng.randrange(2),kind=rng.choice([304,312]),sub=rng.randrange(7),state=rng.choice([4,5,28]),
                          counter=rng.choice([-2147483648,-1,0,1,2,3,120,2147483647]),pause=rng.choice([-2147483648,-1,0,1,2,2147483647]),
                          elapsed=rng.choice([-1,0,1,2,120,2147483647]),body=rng.getrandbits(32),behavior=rng.getrandbits(32),complete=rng.getrandbits(32),
                          turn=rng.randint(-32768,32767),index=rng.choice([0,1,5]),mode=rng.randrange(4),anim=rng.randrange(30),frame=rng.randrange(40),
                          finished=rng.choice([0,1,255]),runfinished=rng.randrange(2),health=rng.choice([0,100]),
                          x=rng.randint(-2147483648,2147483647),y=rng.choice([-204800,-204799,0,204799,204800]),z=rng.randint(-2147483648,2147483647),
                          px=rng.randint(-2147483648,2147483647),py=0,pz=rng.randint(-2147483648,2147483647),tx=rng.randint(-2147483648,2147483647),
                          tz=rng.randint(-2147483648,2147483647),angvel=rng.randint(-32768,32767),angacc=rng.randint(-32768,32767),
                          d0=rng.choice([-1,0,100,120,140,162,190]),d1=rng.randrange(190),d2=rng.randrange(190),d3=rng.randrange(190),
                          random=rng.randrange(2),path=rng.choice([-1,0,1,2,4]),add=rng.randrange(2),flags=rng.randrange(8)))
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-attack-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-attack.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} thug attack/retreat cases.')


if __name__=='__main__':main()
