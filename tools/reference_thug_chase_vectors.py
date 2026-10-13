"""Compile source ChasePlayer, GetAttackPosition and LookForPlayer with shared AI setup."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method
from reference_thug_attack_vectors import recovered_harness as attack_harness


def recovered_harness(root):
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text()
    ai=(root/'src/engine/world/ai.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    distance=(root/'src/core/distance.h').read_text()
    code=attack_harness(root)
    code=code.replace('#include <algorithm>','#include <algorithm>\n#include <cmath>\n#include "core/fixed_vector.h"\nusing VECTOR=Vector;\nvoid VectorNormal(VECTOR*,VECTOR*);')
    code=code.replace('struct CVector{int vx=0,vy=0,vz=0;};struct CSVector{i16 vx=11,vy=17,vz=13;};','')
    code=code.replace('int randomValue=0,pathResult=0,addResult=0,distanceIndex=0;int distances[4]={};',
                      'int randomValue=0,pathResult=0,addResult=0,distanceIndex=0,addIndex=0;int distances[6]={},addResults[3]={};\nint assignedFlags=0,assignedMask=0,assignedCursor=0;CVector pathHit,searchPoint;')
    code=code.replace('field_1A8[8]', 'field_1A8[64]')
    code=code.replace('CBaddy*G_MECHLIST_PLAYER=nullptr;','CBaddy*G_MECHLIST_PLAYER=nullptr,*G_THUG_LIST=nullptr;')
    code=code.replace('AI_TYPE_UNK_20000=0x20000','AI_TYPE_UNK_20000=0x20000,MOVE_TO=1794')
    code=code.replace('int RunTimer(int*);','int field_57C=0,field_364=0,field_3B4=0,field_29C=0,field_330=600;void*mHeldObject=nullptr;\nunsigned field_3AC=0,field_2AC=0;unsigned char field_3BC=0,field_3BD=0,field_3BE=0,field_21D=0;\nint RunTimer(int*);')
    code=code.replace('void AttackPlayer();int TooCloseToSpidey();','void AttackPlayer();int TooCloseToSpidey();void ChasePlayer(int);void GetAttackPosition(CVector*);void LookForPlayer();void RunAppropriateAnim();')
    code=code.replace('std::min(distanceIndex++,3)','std::min(distanceIndex++,5)')
    code=code.replace('CVector*to,CVector*,int clearance)', 'CVector*to,CVector*hit,int clearance)')
    code=code.replace('return pathResult;}','if(hit)*hit=pathHit;return pathResult;}')
    code=code.replace('if(addResult)field_1A8[++field_1F0]=*p;return addResult;',
                      'int result=addResults[std::min(addIndex++,2)];if(result)field_1A8[++field_1F0]=*p;return result;')
    code=code.replace('void CycleAnim(unsigned c,int dir)',
                      'int SetAttackFlags(){events.push_back(10);field_3BC=assignedFlags;field_3BD=assignedMask;field_3BE=assignedCursor;return 0;}\nvoid ClearAttackFlags(){events.push_back(12);field_3BC=0;field_3BD=0;}\nvoid GetWaypointNearTarget(CVector*p,int radius,int phase,CVector*out){events.insert(events.end(),{13,p->vx,p->vy,p->vz,radius,phase});*out=searchPoint;}\nvoid CycleAnim(unsigned c,int dir)')
    code+=r'''
struct SMoveToInfo{CVector field_0;int field_C=0,field_10=0,field_14=0;};
struct CAIProc_MoveTo:CAIProc{CVector field_20,field_30;int field_2C=0;CAIProc_MoveTo(CBaddy*,SMoveToInfo*,int);};
namespace spidey{namespace math{
'''
    code+='enum{PositionFractionBits=12};\n'+controller_method(distance,'inline u32 approximateDistance(')+'\n'
    code+=controller_method(distance,'inline u32 approximateHorizontalDistance(')+'\n}}\n'
    for source,signature in [(maths,'void VectorNormal('),(ai,'CAIProc_MoveTo::CAIProc_MoveTo('),
                             (baddy,'void CBaddy::RunAppropriateAnim('),(thug,'void CThug::GetAttackPosition('),
                             (thug,'void CThug::ChasePlayer('),(thug,'void CThug::LookForPlayer(')]:
        method=controller_method(source,signature).replace('CThug::','CBaddy::')
        if signature=='void CThug::ChasePlayer(':
            method=method.replace('print_if_false(depth < 5, "Matt needs to fix ChasePlayer.");','if(depth>=5)events.insert(events.end(),{11,depth});')
        code+=method+'\n'
    return code


def main():
    root=Path(__file__).resolve().parent.parent
    code=recovered_harness(root)
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();distanceIndex=addIndex=0;CBaddy t,p,owner;G_MECHLIST_PLAYER=&p;int mode,af,am,ac,phase,unavailable,held,fight,flags,depth;
 std::cin>>t.mType>>t.dumbAssPad>>t.field_31C.bothFlags>>t.field_1F8>>t.mElapsedTicks>>t.mCBodyFlags>>t.field_2A8>>t.field_288
 >>t.field_310>>t.field_1F0>>mode>>t.mAnim>>t.mFrame>>t.mAnimFinished>>t.runFinished>>t.field_364>>t.field_3B4>>t.field_29C
 >>t.field_3AC>>af>>am>>ac>>phase>>t.field_330>>unavailable>>held>>fight
 >>t.mPos.vx>>t.mPos.vy>>t.mPos.vz>>p.mPos.vx>>p.mPos.vy>>p.mPos.vz
 >>t.field_1A8[t.field_1F0].vx>>t.field_1A8[t.field_1F0].vy>>t.field_1A8[t.field_1F0].vz
 >>pathHit.vx>>pathHit.vy>>pathHit.vz>>searchPoint.vx>>searchPoint.vy>>searchPoint.vz
 >>distances[0]>>distances[1]>>distances[2]>>distances[3]>>distances[4]>>distances[5]
 >>randomValue>>pathResult>>addResults[0]>>addResults[1]>>addResults[2]>>assignedFlags>>assignedMask>>assignedCursor>>t.field_2AC>>flags>>depth;
 t.field_3BC=af;t.field_3BD=am;t.field_3BE=ac;t.field_21D=phase;p.field_57C=unavailable;p.mHeldObject=held?&p:nullptr;G_THUG_LIST=fight?&owner:nullptr;
 t.mAngVel={11,17,13};t.mAngAcc={11,-23,13};t.mVel={11,17,13};t.mAcc={-7,-23,5};t.field_27C={11,17,13};
 CAIProc look,move,monitor;look.mAIProcType=257;move.mAIProcType=1794;monitor.mAIProcType=0x20501;
 look.field_10=move.field_10=monitor.field_10=flags;monitor.mNext=&move;move.mNext=&look;t.mAIProcList=&monitor;
 t.field_294.Int=(t.mType==304?thugModes:henchModes)[mode][0];t.field_298.Int=(t.mType==304?thugModes:henchModes)[mode][1];
 CVector target;if(op==0)t.ChasePlayer(depth);else if(op==1)t.GetAttackPosition(&target);else t.LookForPlayer();
 std::cout<<'['<<t.dumbAssPad<<','<<t.field_31C.bothFlags<<','<<t.field_1F8<<','<<t.mCBodyFlags<<','<<t.field_2A8<<','<<t.field_288<<','<<t.field_310<<','<<t.field_1F0
 <<','<<t.field_294.Int<<','<<t.field_298.Int<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.field_364<<','<<t.field_3B4<<','<<t.field_29C<<','<<t.field_3AC
 <<','<<int(t.field_3BC)<<','<<int(t.field_3BD)<<','<<int(t.field_3BE)<<','<<int(t.field_21D)<<','<<t.field_330<<','<<(G_THUG_LIST!=nullptr)
 <<','<<t.mPos.vx<<','<<t.mPos.vy<<','<<t.mPos.vz<<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.mAcc.vx<<','<<t.mAcc.vy<<','<<t.mAcc.vz
 <<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz<<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.mAngAcc.vx<<','<<t.mAngAcc.vy<<','<<t.mAngAcc.vz
 <<','<<target.vx<<','<<target.vy<<','<<target.vz<<",[";bool first=true;
 for(CAIProc*q=t.mAIProcList;q;q=q->mNext){if(!first)std::cout<<',';first=false;std::cout<<'['<<q->mAIProcType<<','<<q->field_10<<','<<q->field_14<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    def case(**changes):
        base=dict(op=0,kind=304,sub=1,state=4,counter=7,elapsed=2,body=0x216,behavior=0xffffffff,complete=0,turn=0,index=1,mode=0,
                  anim=3,frame=4,finished=1,runfinished=0,work=0,grenade=0,ground=0,action=0,af=1,am=0,ac=0,phase=0,alert=600,
                  unavailable=0,held=0,fight=0,x=0,y=0,z=0,px=4096000,py=0,pz=8192000,tx=4096000,ty=0,tz=8192000,
                  hx=4096000,hy=0,hz=8192000,sx=409600,sy=0,sz=-819200,d0=1000,d1=1000,d2=1000,d3=1000,d4=1000,d5=1000,
                  random=0,path=0,add0=1,add1=1,add2=1,assignedflags=1,assignedmask=0,assignedcursor=0,movement=0,flags=0,depth=0)
        base.update(changes);return list(base.values())
    cases=[]
    for kind in [304,312]:
        for sub in [0,1,2,3,4,5,20,21,22]:
            for distanceValue in [0,100,139,140,189,190,209,210,284,285,650,651,799,800,1499,1500,1999,2000]:
                for flags in [0,1,2]:
                    cases.append(case(kind=kind,sub=sub,d0=distanceValue,d1=distanceValue,d2=distanceValue,d3=distanceValue,d4=distanceValue,d5=distanceValue,af=flags,assignedflags=flags))
    for kind in [304,312]:
        for flags in [0,1,2,3]:
            for mask in range(256):cases.append(case(op=1,kind=kind,af=flags,am=mask,d0=100))
    for counter in [-2147483648,-1,0,63,64,127,128,129,2147483647]:
        for phase in [0,63,64,254,255]:
            for path in [0,2]:
                for adds in [(1,1,1),(0,1,1),(0,0,1),(0,1,0)]:
                    cases.append(case(op=2,sub=1,counter=counter,phase=phase,path=path,add0=adds[0],add1=adds[1],add2=adds[2]))
    rng=random.Random(0x4D4680)
    for _ in range(1500):
        cases.append(case(op=rng.choice([0,0,0,1,2]),kind=rng.choice([304,312]),sub=rng.choice([0,1,2,3,4,5,20,21,22]),state=rng.choice([1,4]),
                          counter=rng.choice([-2147483648,-1,0,6,7,30,64,129,2147483647]),elapsed=rng.choice([-1,0,1,2,30,2147483647]),
                          body=rng.getrandbits(32),behavior=rng.getrandbits(32),complete=rng.choice([0,1,2,3]),index=rng.choice([0,1,5]),
                          mode=rng.randrange(4),finished=rng.choice([0,1,255]),work=rng.choice([-1,0,5,6,7,2147483647]),grenade=rng.choice([0,600,601]),
                          ground=rng.randint(-2000000,2000000),action=rng.getrandbits(32),af=rng.randrange(4),am=rng.randrange(256),ac=rng.randrange(256),phase=rng.randrange(256),
                          unavailable=rng.randrange(2),held=rng.randrange(2),fight=rng.randrange(2),x=rng.randint(-10000000,10000000),
                          y=rng.choice([-204800,-204799,0,204799,204800]),z=rng.randint(-10000000,10000000),px=rng.randint(-10000000,10000000),
                          py=rng.randint(-1000000,1000000),pz=rng.randint(-10000000,10000000),tx=rng.randint(-10000000,10000000),
                          tz=rng.randint(-10000000,10000000),d0=rng.choice([-1,0,100,139,189,285,650,799,1000,1500,2000]),
                          d1=rng.randrange(2200),d2=rng.randrange(2200),d3=rng.randrange(2200),d4=rng.randrange(2200),d5=rng.randrange(2200),
                          random=rng.randrange(64),path=rng.choice([-1,0,1,2,4]),add0=rng.randrange(2),add1=rng.randrange(2),add2=rng.randrange(2),
                          assignedflags=rng.randrange(4),assignedmask=rng.randrange(256),assignedcursor=rng.randrange(256),
                          movement=rng.choice([0,0x10000,0x20000,0x40000]),flags=rng.randrange(8),depth=rng.choice([0,4,5])))
    # Search only accepts substates zero and one; preserve all chase substates independently.
    for c in cases:
        if c[0]==2:c[2]=c[2]%2
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-chase-')as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c))for c in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=c,expected=json.loads(e))for c,e in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-chase.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(output)} thug chase/attack-position/search cases.')


if __name__=='__main__':main()
