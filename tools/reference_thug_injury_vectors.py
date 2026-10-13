"""Compile recovered Fall/FlyAcrossRoom/DieThug plus original death/ground helpers."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_attack_vectors import recovered_harness as attack_harness


def recovered_harness(root):
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text();ai=(root/'src/engine/world/ai.cpp').read_text()
    code=attack_harness(root)
    code=code.replace('int Rnd(int n){events.insert(events.end(),{8,n});return randomValue%n;}',
                      'int randomIndex=0,randomValues[3]={};int Rnd(int n){events.insert(events.end(),{8,n});return randomValues[std::min(randomIndex++,2)]%n;}')
    code=code.replace('struct CBaddy{', 'int groundResult=0,heightResult=0,physicsCounter=0;CBaddy*collisionBody=nullptr;\nstruct CBaddy{')
    code=code.replace('AI_TYPE_UNK_20000=0x20000','AI_TYPE_UNK_20000=0x20000,PROC_FALL_TYPE=770')
    code=code.replace('int RunTimer(int*);',r'''
 int field_318=0,field_324=0,field_328=0,field_308=0,field_340=0,field_34C=0,mRMinor=150,field_21E=100;
 using u16=uint16_t;unsigned field_218=0,field_3AC=0,mRGB=0,mTRN=0;CSVector mAngles;unsigned short mFlags=0;i16*mpJoints=nullptr;CVector field_2FC;bool shadow=true,markedDead=false;
 bool IsDead(){return (mCBodyFlags&0x40)!=0;}void FlyAcrossRoom();void Fall();void DieThug(int);void DieAfterFlyingAcrossRoom();
 void RunAppropriateHitAnim();void PlayHitWallSound();int ShouldFall(int,int);int OriginalDie(int);
 void ClearAttackFlags(){events.push_back(16);}
 void DoPhysics(int mode){events.insert(events.end(),{4,mode});field_1F8=physicsCounter;mPos.vx+=11;mPos.vy-=23;mPos.vz+=17;mVel.vx+=1;mVel.vy-=2;mVel.vz+=3;}
 CBaddy*StruckGameObject(int a,int b){events.insert(events.end(),{5,a,b});return collisionBody;}
 void ElasticCollision(CBaddy*,CBaddy*body,CVector&velocity,int time){events.insert(events.end(),{6,body->mType,velocity.vx,velocity.vy,velocity.vz,time});}
 void CheckFallBack(){events.push_back(7);}
 void ApplyPose(i16*p){events.insert(events.end(),{17,p==reinterpret_cast<i16*>(0x557CE4)?304:312});}
 void KillShadow(){events.push_back(11);shadow=false;}
 int SetHeight(int initial,int clearance,int range){events.insert(events.end(),{12,initial,clearance,range});return heightResult;}
 void SendDeathPulse(){events.push_back(13);}
 void NotifyKill(int type){events.insert(events.end(),{18,type});}
 int Die(int stage){events.insert(events.end(),{14,stage});bool before=IsDead();int result=OriginalDie(stage);if(!before&&IsDead()){events.push_back(15);markedDead=true;}return result;}
 int RunTimer(int*);''')
    code=code.replace('void SFX_PlayPos(unsigned sound,CVector*,int){events.insert(events.end(),{9,int(sound)});}',
                      'void SFX_PlayPos(unsigned sound,CVector*p,int attenuation){events.insert(events.end(),{9,int(sound),p->vx,p->vy,p->vz,attenuation});}')
    code+=r'''
void SFX_Play(unsigned sound,int volume,int pan){events.insert(events.end(),{19,int(sound),volume,pan});}
int Utils_GetGroundHeight(CVector*p,int above,int below,int flags){events.insert(events.end(),{10,p->vx,p->vy,p->vz,above,below,flags});return groundResult;}
struct CAIProc_Fall:CAIProc{CAIProc_Fall(CBaddy*,int);};
'''
    for source,signature in [(ai,'CAIProc_Fall::CAIProc_Fall('),(baddy,'int CBaddy::Die('),(baddy,'int CBaddy::ShouldFall('),
                             (thug,'void CThug::RunAppropriateHitAnim('),(thug,'void CThug::PlayHitWallSound('),
                             (thug,'void CThug::DieAfterFlyingAcrossRoom('),(thug,'void CThug::FlyAcrossRoom('),
                             (thug,'void CThug::Fall('),(thug,'void CThug::DieThug(')]:
        method=controller_method(source,signature).replace('CThug::','CBaddy::')
        if signature=='int CBaddy::Die(':method=method.replace('CBaddy::Die(', 'CBaddy::OriginalDie(')
        code+=method+'\n'
    return code


def main():
    root=Path(__file__).resolve().parent.parent
    code=recovered_harness(root)
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();distanceIndex=randomIndex=0;CBaddy t,p,collision;G_MECHLIST_PLAYER=&p;int joints,instant,flags,type,mode;short jointValues[32]={};
 std::cin>>t.mType>>t.dumbAssPad>>t.field_31C.bothFlags>>t.mHealth>>t.field_1F8>>t.field_230>>t.mElapsedTicks>>t.mCBodyFlags>>t.field_2A8>>t.field_288
 >>t.field_310>>t.field_218>>t.field_3AC>>t.field_318>>t.field_324>>t.field_328>>t.field_308>>t.field_340>>t.field_34C>>t.mRMinor>>t.mFlags>>t.mRGB>>t.mTRN
 >>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished>>joints>>jointValues[6]
 >>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>t.mVel.vx>>t.mVel.vy>>t.mVel.vz>>t.mAcc.vx>>t.mAcc.vy>>t.mAcc.vz
 >>t.mAngles.vx>>t.mAngles.vy>>t.mAngles.vz>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz
 >>groundResult>>heightResult>>physicsCounter>>type>>instant>>randomValues[0]>>randomValues[1]>>randomValues[2]>>flags>>mode;
 collision.mType=type;collisionBody=type<0?nullptr:&collision;t.mpJoints=joints?jointValues:nullptr;
 t.mAngVel={11,17,13};t.mAngAcc={11,-23,13};t.field_27C={11,17,13};
 CAIProc look,move,monitor;look.mAIProcType=257;move.mAIProcType=1794;monitor.mAIProcType=0x20501;
 look.field_10=move.field_10=monitor.field_10=flags;monitor.mNext=&move;move.mNext=&look;t.mAIProcList=&monitor;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 int result=0;if(op==0)t.Fall();else if(op==1)t.FlyAcrossRoom();else if(op==2)t.DieThug(instant);else if(op==3)t.DieAfterFlyingAcrossRoom();else if(op==4)result=t.Die(instant);else result=t.ShouldFall(200,389120);
 std::cout<<'['<<result<<','<<t.dumbAssPad<<','<<t.field_31C.bothFlags<<','<<t.mHealth<<','<<t.field_1F8<<','<<t.field_230<<','<<t.mCBodyFlags<<','<<t.field_2A8<<','<<t.field_288
 <<','<<t.field_310<<','<<t.field_218<<','<<t.field_3AC<<','<<t.field_308<<','<<t.field_340<<','<<t.field_34C<<','<<t.mRMinor<<','<<t.mFlags<<','<<t.mRGB<<','<<t.mTRN<<','<<jointValues[6]
 <<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.shadow<<','<<t.markedDead
 <<','<<t.field_2FC.vx<<','<<t.field_2FC.vy<<','<<t.field_2FC.vz<<','<<t.mPos.vx<<','<<t.mPos.vy<<','<<t.mPos.vz
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz<<','<<t.mAngles.vx<<','<<t.mAngles.vy<<','<<t.mAngles.vz
 <<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz<<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz
 <<",[";bool first=true;for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(!first)std::cout<<',';first=false;std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**changes):
        base=dict(op=0,kind=304,sub=0,state=22,health=90,counter=2,pause=0,elapsed=2,body=0x216,behavior=0xffffffff,complete=0,turn=100,combat=0,action=0,
                  context=1,pitchrate=33,yawrate=-65,landing=1000000,collisiondelay=0,deathsound=3,radius=150,item=0,tint=0,trn=0,
                  anim=33,frame=21,finished=1,runfinished=0,joints=1,joint=0,x=0,y=0,z=0,vx=32768,vy=1310720,vz=-49152,ax=11,ay=17,az=13,
                  angx=10,angy=100,angz=20,px=4096000,py=0,pz=8192000,ground=409600,height=2,physicscounter=0,target=-1,instant=0,r0=1,r1=1,r2=1,flags=0,mode=0)
        base.update(changes);return list(base.values())
    cases=[]
    for kind in [304,312]:
        for sub in range(7):
            for counter in [-2147483648,-1,0,1,2,50,65,2147483647]:
                for complete in [0,4]:cases.append(case(kind=kind,sub=sub,counter=counter,complete=complete))
    for kind in [304,312]:
        for sub in [0,1,2,3,5,10]:
            for context in [0,1,2,6,23,25]:
                for target in [-1,50,304,312]:
                    for collisiondelay in [0,10]:cases.append(case(op=1,kind=kind,sub=sub,context=context,target=target,collisiondelay=collisiondelay,physicscounter=3))
    for kind in [304,312]:
        for sub in [0,1,2,3,5,6,7,8]:
            for counter in [-1,0,9,10,39,40,41,120]:
                for instant in [0,1]:cases.append(case(op=2,kind=kind,sub=sub,counter=counter,instant=instant))
    for stage in range(4):
        for counter in [-2147483648,-1,0,1,39,40,41,2147483647]:
            for zombie in [0,0x40]:cases.append(case(op=4,instant=stage,counter=counter,body=zombie))
    rng=random.Random(0x4D7C30)
    for _ in range(1500):
        operation=rng.randrange(6)
        sub=rng.choice([0,1,2,3,5,10])if operation==1 else rng.choice([0,1,2,3,5,6,7,8])if operation==2 else rng.randrange(7)
        cases.append(case(op=operation,kind=rng.choice([304,312]),sub=sub,state=rng.choice([17,22,26,27]),health=rng.choice([-1,0,50,51,90]),
                          counter=rng.choice([-2147483648,-1,0,1,2,3,39,40,50,65,120,2147483647]),elapsed=rng.choice([-1,0,1,2,50,65536,2147483647]),
                          body=rng.getrandbits(32),behavior=rng.getrandbits(32),complete=rng.choice([0,4]),combat=rng.getrandbits(32),context=rng.choice([0,1,2,6,23,25]),
                          pitchrate=rng.randint(-65536,65536),yawrate=rng.randint(-65536,65536),landing=rng.randint(-2000000,2000000),collisiondelay=rng.choice([0,10]),
                          deathsound=rng.choice([-1,0,1,2,300]),item=rng.randrange(65536),anim=rng.choice([0,14,15,19,25,26,32,33,37]),frame=rng.choice([-1,0,21,22,40]),
                          finished=rng.choice([0,1,255]),joints=rng.randrange(2),joint=rng.choice([-32768,-90,0,89,90,810,899,900,32767]),
                          x=rng.randint(-2147483648,2147483647),y=rng.randint(-2000000,2000000),z=rng.randint(-2147483648,2147483647),
                          vx=rng.randint(-2147483648,2147483647),vy=rng.randint(-2147483648,2147483647),vz=rng.randint(-2147483648,2147483647),
                          angx=rng.randint(-32768,32767),angy=rng.randint(-32768,32767),angz=rng.randint(-32768,32767),
                          ground=rng.choice([-1,409600,800000,2000000]),height=rng.choice([0,1,2]),physicscounter=rng.choice([0,1,2,3]),target=rng.choice([-1,0,50,304,312]),
                          instant=rng.randrange(4)if operation==4 else rng.randrange(2),r0=rng.randrange(300),r1=rng.randrange(300),flags=rng.randrange(8),mode=rng.randrange(4)))
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-injury-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-injury.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} thug fall/flight/death/lifecycle cases.')


if __name__=='__main__':main()
