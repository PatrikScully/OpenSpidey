"""Compile source ranged/grenade states, shot schedules, auto aim and state-exit monitors."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_chase_vectors import recovered_harness


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text();ai=(root/'src/engine/world/ai.cpp').read_text()
    code=recovered_harness(root)
    code=code.replace('int Rnd(int n){events.insert(events.end(),{8,n});return randomValue%n;}',
                      'int randomIndex=0,randomValues[4]={};int Rnd(int n){events.insert(events.end(),{8,n});return randomValues[std::min(randomIndex++,3)]%n;}')
    code=code.replace('void AttachProc(AIProcType,CBaddy*,int);', 'virtual ~CAIProc();virtual void Execute(){if(mNext)mNext->Execute();}void AttachProc(AIProcType,CBaddy*,int);')
    code=code.replace('struct CBaddy{',r'''
struct FakeLaser{};struct SLineInfo{CVector StartCoords,EndCoords;};struct SHitInfo{unsigned field_0=0;int field_8=0;};
struct SStateFlags{int bothFlags=0;};struct SHook{CSVector Part;int Offset=0;};
int visible=0,laserResult=0,hitMutation=0,G_ATTACK_RELATED=0;CVector collisionAdjustment;
int Utils_LineOfSight(CVector*,CVector*,CVector*,int){events.push_back(27);return visible;}
struct CBaddy{''')
    code=code.replace('struct{int bothFlags=0;}field_31C;', 'SStateFlags field_31C;')
    code=code.replace('MOVE_TO=1794','MOVE_TO=1794,STATE_SWITCH_SEND_MESSAGE=0x60601')
    code=code.replace('void ChasePlayer(int);',r'''
 unsigned field_218=0;int field_32C=0;ushort mFlags=0;int field_3A8=0,field_388=10,field_38C=10,field_380=400,field_338=0,field_344=0;FakeLaser*field_3A0=nullptr;
 bool IsDead(){return (mCBodyFlags&0x40)!=0;}int PlayerIsVisible();void ShootPlayer();void TossGrenade();
 int DrawLaserSiteThingie(int,CBaddy*);int DrawLaserSiteThingieForAlternateModel(int);void AutoAimPlease(CVector*,CVector*,CVector*,int);
 void ClearLaser(){events.push_back(15);delete field_3A0;field_3A0=nullptr;}
 void RotateTorsoToAimAtPlayer(CVector&p){events.insert(events.end(),{16,p.vx,p.vy,p.vz});}
 int LaserCollision(SLineInfo*l,CVector*d,int check){events.insert(events.end(),{17,check,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz,d->vx,d->vy,d->vz});l->EndCoords+=collisionAdjustment;return laserResult;}
 void DrawBarrelFlash(CVector*s,CVector*e,SLineInfo*,int r,int g,int b){events.insert(events.end(),{19,s->vx,s->vy,s->vz,e->vx,e->vy,e->vz,r,g,b});}
 void SetUpLaser(FakeLaser**p,CVector*s,CVector*e){events.insert(events.end(),{21,s->vx,s->vy,s->vz,e->vx,e->vy,e->vz});*p=new FakeLaser;}
 int Hit(SHitInfo*p){events.insert(events.end(),{24,int(p->field_0),p->field_8});if(hitMutation)mHealth-=p->field_8;return 0;}
 void Victorious(){events.push_back(25);}
 void CleanUpAIProcList(int);void ChasePlayer(int);''')
    code=code.replace('unsigned mCBodyFlags=0,','unsigned mCBodyFlags=0,')
    code=code.replace('void SFX_PlayPos(unsigned sound,CVector*,int){events.insert(events.end(),{9,int(sound)});}',
                      'void SFX_PlayPos(unsigned sound,CVector*,int attenuation){events.insert(events.end(),{9,int(sound),attenuation});}')
    code+=r'''
CBody*G_BADDY_LIST=nullptr;
CVector hookPositions[4]={{81920,16384,40960},{40960,8192,20480},{-81920,16384,-40960},{-40960,8192,-20480}};
void M3dUtils_GetHookPosition(VECTOR*out,CBaddy*,int index){events.insert(events.end(),{14,index});auto p=hookPositions[index];out->vx=p.vx;out->vy=p.vy;out->vz=p.vz;}
void M3dUtils_GetDynamicHookPosition(VECTOR*out,CBaddy*,SHook*h){events.insert(events.end(),{26,h->Part.vx,h->Part.vy,h->Part.vz,h->Offset});out->vx=1234;out->vy=4567;out->vz=8901;}
struct CThugBulletTracer{CThugBulletTracer(CVector&s,CVector&e,CBaddy*body,SLineInfo*,int r,int g,int b){events.insert(events.end(),{18,s.vx,s.vy,s.vz,e.vx,e.vy,e.vz,body!=nullptr,r,g,b});}};
struct CGlowFlash{CGlowFlash(CVector*p,int a,int b,int c,int d,int e,int f,int g,int h,int i,int j,int k,int l,int m,int n,int o,int q,int r,int s){events.insert(events.end(),{20,p->vx,p->vy,p->vz,a,b,c,d,e,f,g,h,i,j,k,l,m,n,o,q,r,s});}};
struct CGrenade{CGrenade(CVector&s,CVector&t,int speed,int damage,int radius,CBody**targets,unsigned checksum,int contact,int flag){events.insert(events.end(),{22,s.vx,s.vy,s.vz,t.vx,t.vy,t.vz,speed,damage,radius,int(checksum),contact,flag,targets==&G_BADDY_LIST?0:1});}};
struct CMessage{CMessage(CBaddy*,CBaddy*,int code,void*){events.insert(events.end(),{23,code});}};
struct CAIProc_StateSwitchSendMessage:CAIProc{SStateFlags mFlags;int field_24=0;CAIProc_StateSwitchSendMessage(CBaddy*,int);void Execute()override;};
'''
    for source,signature in [(ai,'CAIProc::~CAIProc('),(baddy,'void CBaddy::CleanUpAIProcList('),(ai,'void CAIProc_StateSwitchSendMessage::Execute('),(ai,'CAIProc_StateSwitchSendMessage::CAIProc_StateSwitchSendMessage('),
                             (baddy,'i32 CBaddy::PlayerIsVisible('),(thug,'void CThug::AutoAimPlease('),
                             (thug,'i32 CThug::DrawLaserSiteThingieForAlternateModel('),(thug,'i32 CThug::DrawLaserSiteThingie('),
                             (thug,'void CThug::ShootPlayer('),(thug,'void CThug::TossGrenade(')]:
        method=controller_method(source,signature).replace('CThug::','CBaddy::').replace('delete this->field_3A0;', 'this->ClearLaser();')
        code+=method+'\n'
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();distanceIndex=addIndex=randomIndex=0;CBaddy t,p;G_MECHLIST_PLAYER=&p;int itemFlags,held,mode,initialLaser,check,procFlags;
 std::cin>>t.mType>>t.dumbAssPad>>t.field_31C.bothFlags>>t.field_1F8>>t.field_230>>t.mElapsedTicks>>t.field_330>>t.field_218>>itemFlags
 >>t.field_338>>t.field_344>>t.field_3A8>>t.field_3B4>>t.field_32C>>t.field_288>>t.field_2A8>>t.mCBodyFlags>>t.field_29C
 >>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished>>p.mHealth>>held>>p.mCBodyFlags>>G_ATTACK_RELATED>>visible>>pathResult>>laserResult>>hitMutation
 >>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz
 >>distances[0]>>distances[1]>>distances[2]>>distances[3]>>distances[4]>>distances[5]
 >>randomValues[0]>>randomValues[1]>>randomValues[2]>>randomValues[3]>>mode>>initialLaser>>check>>t.field_380>>t.field_388>>t.field_38C>>procFlags;
 t.mFlags=itemFlags;p.mHeldObject=held?&p:nullptr;t.field_3A0=initialLaser?new FakeLaser:nullptr;collisionAdjustment={11,-23,17};
 t.mVel={11,17,13};t.mAcc={-7,-23,5};t.field_27C={11,17,13};t.mAngVel={11,17,13};t.mAngAcc={11,-23,13};
 CAIProc look,move,monitor;look.mAIProcType=257;move.mAIProcType=1794;monitor.mAIProcType=0x20501;look.field_10=move.field_10=monitor.field_10=procFlags;
 monitor.mNext=&move;move.mNext=&look;look.pBaddy=move.pBaddy=monitor.pBaddy=&t;look.field_18=&move;move.field_18=&monitor;t.mAIProcList=&monitor;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 int result=0;CVector aimTarget,aimDirection;CVector origin=t.mPos;
 if(op==0)t.ShootPlayer();else if(op==1)t.TossGrenade();else if(op==2)result=t.DrawLaserSiteThingie(check,nullptr);else if(op==3)t.AutoAimPlease(&origin,&aimTarget,&aimDirection,t.field_380);else{new CAIProc_StateSwitchSendMessage(&t,12);new CAIProc_StateSwitchSendMessage(&t,17);t.mAIProcList->Execute();t.field_31C.bothFlags++;t.mAIProcList->Execute();t.CleanUpAIProcList(0);t.mAIProcList->Execute();}
 std::cout<<'['<<result<<','<<t.dumbAssPad<<','<<t.field_31C.bothFlags<<','<<t.field_1F8<<','<<t.field_230<<','<<t.field_330<<','<<t.field_218
 <<','<<t.field_338<<','<<t.field_344<<','<<t.field_3A8<<','<<t.field_3B4<<','<<t.field_32C<<','<<t.field_288<<','<<t.field_2A8<<','<<t.mCBodyFlags
 <<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.field_310<<','<<p.mHealth<<','<<(t.field_3A0!=nullptr)<<','<<t.field_294.Int<<','<<t.field_298.Int
 <<','<<t.field_1A8[0].vx<<','<<t.field_1A8[0].vy<<','<<t.field_1A8[0].vz
 <<','<<aimTarget.vx<<','<<aimTarget.vy<<','<<aimTarget.vz<<','<<aimDirection.vx<<','<<aimDirection.vy<<','<<aimDirection.vz
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz<<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz
 <<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz
 <<",[";bool first=true;for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(!first)std::cout<<',';first=false;std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**changes):
        base=dict(op=0,kind=304,sub=4,state=9,counter=2,pause=15,elapsed=2,alert=600,combat=0,item=0,cooldown=0,delay=0,aim=0,grenade=0,ranged=0,
                  complete=2,behavior=0xffffffff,body=0x216,ground=0,anim=7,frame=2,finished=0,runfinished=0,health=100,held=0,pbody=0,
                  clock=16,visible=1,path=0,laser=1,hitmutation=1,x=0,y=0,z=0,px=4096000,py=16384,pz=8192000,
                  d0=1000,d1=1000,d2=1000,d3=1000,d4=1000,d5=1000,r0=1,r1=17,r2=31,r3=2,mode=0,initiallaser=1,check=1,spread=400,gdamage=10,damage=10,flags=0)
        base.update(changes);return list(base.values())
    cases=[]
    for kind in [304,312]:
        for sub in range(7):
            for done in [0,1]:
                for counter in [-2147483648,-1,0,1,2,3,120]:
                    for held in [0,1]:cases.append(case(kind=kind,sub=sub,finished=done,counter=counter,held=held,anim=7 if kind==304 else 8))
    for kind in [304,312]:
        for shot in range(8):
            for frame in [-1,0,1,2,5,6,9,10,11,13,14,15,37]:
                for laser in [-1,0,1]:
                    for check in [0,1]:cases.append(case(op=2,kind=kind,combat=shot<<12,frame=frame,laser=laser,check=check,anim=7 if kind==304 else 8))
    for sub in range(4):
        for frame in [0,36,37,38]:
            for complete in [0,2]:
                for done in [0,1]:cases.append(case(op=1,kind=312,sub=sub,frame=frame,complete=complete,finished=done))
    for kind in [304,312]:
        for py in [-8192000,-1,0,1,8192000]:
            for aim in [-8192000,0,8192000]:
                for px,pz in [(1,0),(0,1),(-1,-1),(4096,8192),(8192000,-4096000),(16384000,16384000)]:
                    cases.append(case(op=3,kind=kind,py=py,aim=aim,px=px,pz=pz))
    for state in [0,9,10,2147483647]:cases.append(case(op=4,state=state,flags=0))
    rng=random.Random(0x4D6C90)
    for _ in range(900):
        operation=rng.randrange(4)
        cases.append(case(op=operation,kind=rng.choice([304,312]),sub=rng.randrange(4)if operation==1 else rng.randrange(7),counter=rng.choice([-1,0,1,2,3,120]),
                          pause=rng.choice([-2147483648,-1,0,1,7,8,15,2147483647]),elapsed=rng.choice([-1,0,1,2,120,2147483647]),combat=rng.getrandbits(32),
                          item=rng.choice([0,0x8000]),cooldown=rng.choice([0,1]),aim=rng.randint(-2000000,2000000),complete=rng.choice([0,2]),
                          behavior=rng.getrandbits(32),body=rng.getrandbits(32),anim=rng.choice([0,1,4,7,8,31]),frame=rng.choice([-1,0,2,6,10,11,14,36,37,38]),
                          finished=rng.choice([0,1,255]),runfinished=rng.randrange(2),health=rng.choice([0,5,100]),held=rng.randrange(2),pbody=rng.choice([0,0x40]),
                          clock=rng.randrange(32),visible=rng.randrange(2),path=rng.choice([0,1,2,4]),laser=rng.choice([-1,0,1]),hitmutation=rng.randrange(2),
                          x=rng.randint(-1000000,1000000),y=rng.randint(-1000000,1000000),z=rng.randint(-1000000,1000000),
                          px=rng.randint(-10000000,10000000),py=rng.randint(-1000000,1000000),pz=rng.randint(-10000000,10000000),
                          d0=rng.choice([0,649,650,651,1000]),d1=rng.randrange(1500),d2=rng.randrange(1500),r0=rng.randrange(1000),r1=rng.randrange(1000),
                          r2=rng.randrange(1000),r3=rng.randrange(1000),mode=rng.randrange(4),initiallaser=rng.randrange(2),spread=rng.choice([1,300,400]),
                          gdamage=rng.choice([0,10,50]),damage=rng.choice([0,10,50]),flags=rng.randrange(8)))
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-ranged-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-ranged.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} ranged states/shot schedule/auto aim/grenade cases.')


if __name__=='__main__':main()
