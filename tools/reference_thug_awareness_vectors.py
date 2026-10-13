"""Compile recovered caution/confusion, AI switch timers, contact and shooting decisions."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_fight_vectors import recovered_harness


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text();ai=(root/'src/engine/world/ai.cpp').read_text()
    code=recovered_harness(root)
    code=code.replace('struct CBaddy{',r'''
using u16=uint16_t;using u8=uint8_t;
enum AIProcType{LOOKAT_TWO=259,AI_TYPE_UNK_20000=0x20000};
struct CAIProc{CBaddy*pBaddy=nullptr;CAIProc*mNext=nullptr,*field_18=nullptr;int mAIProcType=0,field_10=0,field_14=0;virtual ~CAIProc()=default;void AttachProc(AIProcType,CBaddy*,int);};
struct SStateFlags{short flags[2];};
SStateFlags thugFlags[17]={{{2,96}},{{23,64}},{{24,64}},{{1,96}},{{26,11}},{{27,11}},{{22,27}},{{18,1}},{{14,7}},{{15,7}},{{12,1}},{{17,7}},{{19,3}},{{20,3}},{{13,5}},{{25,96}},{{0,19}}};
#define G_THUG_STATE_FLAGS thugFlags
struct SHitInfo{unsigned field_0=0;int field_8=0;};
int hitMutation=0,conversationMutation=0,randomIndex=0,randomValues[3]={};
int Rnd(int n){events.insert(events.end(),{8,n});return randomValues[std::min(randomIndex++,2)]%n;}
void print_if_false(bool,const char*){}
struct CBaddy{''')
    code=code.replace('int field_330=0,field_36C=0,field_348=0,field_1A4=0,field_32C=0,field_208=0,field_204=0,field_1F0=0;',
                      'int field_32C=0,field_330=0,field_334=0,field_338=0,field_33C=0,field_340=0;\nint field_36C=0,field_348=0,field_1A4=0,field_208=0,field_204=0,field_1F0=0;')
    code=code.replace('struct{int bothFlags=0;}field_31C;', 'union{int bothFlags=0;short flags[2];}field_31C;')
    code=code.replace('void Neutralize(){events.push_back(4);field_2A8&=0xB7FFFFFB;}',r'''
 int mElapsedTicks=0,field_310=0,field_29C=0,field_1F8=0,field_344=0,field_314=0,mAnim=0,mFrame=0,mAnimFinished=0,runFinished=0;
 short field_21E=100,mSurfaceClearance=0;unsigned field_218=0,field_3AC=0,field_2AC=0,field_288=0;
 struct{unsigned Int=0;unsigned char byteAt(int i){return (Int>>(i*8))&255;}}unused;
 union Word{unsigned Int=0;unsigned char Bytes[4];}field_294,field_298;
 CVector mVel,mAcc,field_27C;CSVector mAngVel,mAngAcc;
 CAIProc*mAIProcList=nullptr;
 void Neutralize();void MarkAIProcList(int,int,int);int RunTimer(int*);u16 CheckStateFlags(SStateFlags*,int);
 int BumpedIntoSpidey(int);void Caution();int ShouldIShootPlayer();void LookConfused();void DoAISwitchLogic();void RunAppropriateAnim();
 int Hit(SHitInfo*p){events.insert(events.end(),{6,int(p->field_0),p->field_8});if(hitMutation){mHealth-=p->field_8;field_31C.bothFlags=12;dumbAssPad=0;field_330=600;}return 0;}
 void StrikeUpConversation(){events.push_back(7);if(conversationMutation){field_31C.bothFlags=20;dumbAssPad=0;field_33C=160;}}
 void RunAnim(unsigned c,int from,int to){events.insert(events.end(),{10,int(c),from,to});mAnim=c;mFrame=from;mAnimFinished=runFinished;}
 void CycleAnim(unsigned c,int dir){events.insert(events.end(),{11,int(c),dir});mAnim=c;mAnimFinished=0;}
''')
    code+=r'''
void SFX_PlayPos(unsigned sound,CVector*,int){events.insert(events.end(),{9,int(sound)});}
struct CAIProc_LookAt:CAIProc{int field_20=0,field_38=0,field_3C=0;CVector field_2C;void SetUpVariables(int,int);CAIProc_LookAt(CBaddy*,int,int,int,int);};
'''
    for source,signature in [(ai,'void CAIProc::AttachProc('),(baddy,'void CBaddy::MarkAIProcList('),(baddy,'void CBaddy::Neutralize('),
                             (baddy,'int CBaddy::RunTimer('),(baddy,'u16 CBaddy::CheckStateFlags('),(baddy,'i32 CBaddy::BumpedIntoSpidey('),
                             (baddy,'void CBaddy::RunAppropriateAnim('),(ai,'void CAIProc_LookAt::SetUpVariables('),
                             (ai,'CAIProc_LookAt::CAIProc_LookAt(CBaddy* pBaddy'),(thug,'i32 CThug::ShouldIShootPlayer('),
                             (thug,'void CThug::Caution('),(thug,'void CThug::LookConfused('),(thug,'void CThug::DoAISwitchLogic(')]:
        method=controller_method(source,signature).replace('CThug::','CBaddy::')
        if signature=='void CBaddy::Neutralize(':method=method.replace('{','{events.push_back(4);',1)
        code+=method+'\n'
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();adds=randomIndex=0;CBaddy t,p,owner;G_MECHLIST_PLAYER=&p;int af,am,held,fight,yaw,mode;
 std::cin>>t.mType>>t.field_31C.bothFlags>>t.dumbAssPad>>t.mHealth>>p.mHealth>>p.mCBodyFlags>>p.field_57C>>held>>fight>>G_ATTACK_RELATED
 >>t.field_32C>>t.field_330>>t.field_334>>t.field_338>>t.field_33C>>t.field_340>>t.field_348>>t.field_344>>t.field_1A4>>t.mElapsedTicks
 >>t.field_208>>t.field_204>>t.field_36C>>t.field_1F8>>t.field_310>>t.field_29C>>t.field_2A8>>t.mCBodyFlags>>t.field_218>>t.field_3AC>>af>>am
 >>t.field_288>>t.field_2AC>>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>yaw>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz>>p.mSurfaceClearance
 >>los>>pathTarget>>pathLast>>addFirst>>addSecond>>hitMutation>>conversationMutation>>randomValues[0]>>randomValues[1]>>randomValues[2]>>mode;
 t.field_3BC=af;t.field_3BD=am;t.mAngles.vy=yaw;p.mHeldObject=held?&p:nullptr;G_THUG_LIST=fight?&owner:nullptr;G_GLOBAL_THUG=nullptr;
 t.field_1A8[0]={40960,0,-81920};t.mVel={11,17,13};t.mAcc={-7,-23,5};t.field_27C={11,17,13};t.mAngVel={11,17,13};t.mAngAcc={11,-23,13};
 CAIProc look,move,monitor;look.mAIProcType=257;move.mAIProcType=1794;monitor.mAIProcType=0x20501;monitor.mNext=&move;move.mNext=&look;t.mAIProcList=&monitor;
 unsigned thugModes[4][2]={{0x0D0D0100,3},{0x14141C1B,0x114},{0x06061C1B,0x204},{0x14141C1B,0x314}};
 unsigned henchModes[4][2]={{0x06040504,0},{0x17171817,0x101},{0x17171817,0x201},{0x01010B0A,0x301}};
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 int result=0;if(op==0)t.DoAISwitchLogic();else if(op==1)t.Caution();else if(op==2)t.LookConfused();else if(op==3)result=t.ShouldIShootPlayer();else result=t.BumpedIntoSpidey(100);
 std::cout<<'['<<result<<','<<t.field_31C.bothFlags<<','<<t.dumbAssPad<<','<<t.mHealth<<','<<t.field_32C<<','<<t.field_330<<','<<t.field_334<<','<<t.field_338
 <<','<<t.field_33C<<','<<t.field_340<<','<<t.field_348<<','<<t.field_344<<','<<t.field_1A4<<','<<t.field_208<<','<<t.field_204<<','<<t.field_36C
 <<','<<t.field_1F8<<','<<t.field_310<<','<<t.field_2A8<<','<<t.mCBodyFlags<<','<<t.field_218<<','<<t.field_3AC<<','<<t.field_288<<','<<t.field_1F0
 <<','<<t.field_1A8[0].vx<<','<<t.field_1A8[0].vy<<','<<t.field_1A8[0].vz<<','<<(G_THUG_LIST!=nullptr)<<','<<t.field_314
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz<<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz
 <<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz
 <<",[";bool first=true;for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(!first)std::cout<<',';first=false;std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14;
 if(q->mAIProcType==259){auto*l=static_cast<CAIProc_LookAt*>(q);std::cout<<','<<l->field_20<<','<<l->field_38<<','<<l->field_3C;}std::cout<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**changes):
        base=dict(op=0,kind=304,state=1,sub=0,health=90,ph=100,pbody=0,unavailable=0,held=0,fight=0,clock=16,
                  ranged=3,alert=0,t334=3,cooldown=3,conversation=0,collision=3,close=2,delay=2,suppress=3,elapsed=2,
                  cacheclock=0,cached=1000,poll=3,counter=2,turn=100,ground=0,behavior=0xffffffff,body=0x216,combat=0,action=0,af=0,am=3,
                  complete=0,movement=0,x=0,y=0,z=0,yaw=2048,px=409600,py=0,pz=0,clearance=100,
                  los=1,path=0,lastpath=0,add1=1,add2=1,hitmutation=0,conmutation=0,r0=1,r1=1,r2=1,mode=0)
        base.update(changes);return list(base.values())
    cases=[]
    for state in [0,1,2,3,4,5,9,12,14,23,24,25,26,28]:
        for elapsed in [0,1,2,3,600]:
            for distance in [0,99,100,299,300,650,651,1999,2000]:
                cases.append(case(state=state,elapsed=elapsed,px=distance<<12,combat=0x88000,action=4,af=1))
    for operation in [1,3,4]:
        for distance in [0,99,100,650,651,1999,2000]:
            for height in [-425984,-425983,-393216,-393215,0,409599,409600]:
                for los in [0,1]:cases.append(case(op=operation,px=distance<<12,py=height,los=los))
    for sub in [0,1,5,6]:
        for counter in [-2147483648,-1,0,1,2,3,2147483647]:
            for yaw in [-32768,-1,0,2048,4095,32767]:cases.append(case(op=2,sub=sub,counter=counter,yaw=yaw,r0=999,r1=1))
    rng=random.Random(0x4DB090)
    for _ in range(1200):
        operation=rng.randrange(5)
        cases.append(case(op=operation,kind=rng.choice([304,312]),state=rng.choice([0,1,2,3,4,5,9,12,14,23,24,25,26,28]),
                          sub=rng.choice([0,1,5,6])if operation==2 else 0,health=rng.choice([0,90]),ph=rng.choice([0,100]),pbody=rng.choice([0,0x40]),
                          unavailable=rng.randrange(2),held=rng.randrange(2),fight=rng.randrange(2),clock=rng.choice([-2147483648,-1,0,15,16,17,2147483647]),
                          ranged=rng.choice([-2147483648,-1,0,1,2,600,2147483647]),alert=rng.choice([0,1,2,600]),t334=rng.randint(-1,5),cooldown=rng.randint(-1,5),
                          conversation=rng.randint(-1,5),collision=rng.randint(-1,5),close=rng.randint(-1,5),delay=rng.randint(-1,5),suppress=rng.randint(-1,5),
                          elapsed=rng.choice([-2147483648,-1,0,1,2,600,2147483647]),cacheclock=rng.choice([0,16,17]),cached=rng.choice([0,99,299,300,650,651,1999,2000]),
                          counter=rng.choice([-1,0,1,2]),behavior=rng.getrandbits(32),body=rng.getrandbits(32),combat=rng.choice([0,0x80000,0x8000,0x88000]),
                          action=rng.choice([0,4]),af=rng.randrange(4),complete=rng.choice([0,2]),movement=rng.choice([0,0x10000,0x20000,0x40000]),
                          px=rng.randint(-10000000,10000000),py=rng.randint(-1000000,1000000),pz=rng.randint(-10000000,10000000),yaw=rng.randint(-32768,32767),
                          clearance=rng.choice([-32768,0,100,32767]),los=rng.randrange(2),path=rng.choice([-1,0,1,2,4]),lastpath=rng.choice([0,2]),
                          add1=rng.randrange(2),add2=rng.randrange(2),hitmutation=rng.randrange(2),conmutation=rng.randrange(2),r0=rng.randrange(1000),r1=rng.randrange(1000),mode=rng.randrange(4)))
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-awareness-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-awareness.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} thug AI timers/contact/caution/confusion cases.')


if __name__=='__main__':main()
