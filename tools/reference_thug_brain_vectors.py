"""Compile original CThug::AI orchestration with recovered physics, awareness and selected leaves.

External transform/sound/message/procedure boundaries deliberately mutate state. Covered dispatch:
10, 19, 20-release, Guard substate5, and state28 recovery into LookConfused substate0.
Other leaves abort if reached; this is not a fixture for the entire thug combat state machine.
"""
import ast
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def literal_header(path):
    tree = ast.parse(path.read_text())
    return next(node.value.value for node in ast.walk(tree)
                if isinstance(node, ast.Assign) and any(isinstance(t, ast.Name) and t.id == 'code' for t in node.targets)
                and isinstance(node.value, ast.Constant) and isinstance(node.value.value, str))


def main():
    root = Path(__file__).resolve().parent.parent
    thug = (root/'src/game/actors/thug.cpp').read_text()
    baddy = (root/'src/game/actors/baddy.cpp').read_text()
    maths = (root/'src/engine/rendering/ps2funcs.cpp').read_text()
    ai = (root/'src/engine/world/ai.cpp').read_text()
    utils = (root/'src/core/utils.cpp').read_text()
    distance = (root/'src/core/distance.h').read_text()
    code = literal_header(root/'tools/reference_actor_body_vectors.py')
    code = code.replace('struct CBaddy {', '''#include "engine/world/stateflags.h"
#include <vector>
#include <cstdlib>
struct CBaddy;using CThug=CBaddy;using CBody=CBaddy;struct CAIProc;
std::vector<long long> events;int G_ATTACK_RELATED=0;unsigned G_ATTACK_FLAG_RELATED=0;
CBaddy *G_MECHLIST_PLAYER=nullptr,*G_THUG_LIST=nullptr,*G_GLOBAL_THUG=nullptr;
SStateFlags stateFlags[17];SStateFlags*G_THUG_STATE_FLAGS=stateFlags;
int finalState=8,finalSub=0,messageState=8,messageSub=0,soundAction=-1,hasProcedure=0,retire=0,randomValue=0,los=0,path=0;
void stage(int,CBaddy*);void print_if_false(bool b,const char*){if(!b)std::abort();}
int Rnd(int range){events.insert(events.end(),{8,range});return randomValue%range;}
union Word{unsigned Int=0;unsigned char Bytes[4];};
struct SHitInfo{int field_0=0,field_8=0;};
struct CBaddy {''')
    code = code.replace('int mCollision=7;', 'int mCollision=7;')
    code = code.replace('void DoPhysics(int);', '''void DoPhysics(int);
 int mType=0,dumbAssPad=0,mHealth=90,mAnim=0,mFrame=0,mAnimFinished=0,field_3B8=0x1101,field_3B4=0,field_310=99;
 int field_32C=0,field_330=1000,field_334=0,field_338=0,field_33C=0,field_340=0;
 int field_314=0,field_348=0,field_344=0,field_1A4=0,field_36C=31,field_208=0,field_204=0,field_1F0=0,field_3B0=0,field_29C=0,field_2A0=0;
 int field_370=3500,field_374=400,field_378=2047,field_37C=100,field_384=1500,field_57C=1;
 unsigned field_218=0,field_3AC=0,mCBodyFlags=0x206,field_288=0xffffffff;
 u8 field_21D=0,field_3BC=0,field_3BD=0;u16 mFlags=0,mShadowScale=50;short field_21E=100;
 CVector mShadowPos,field_1A8[6];SStateFlags field_31C{};Word field_298;CAIProc*mAIProcList=nullptr;void*pMessage=nullptr,*mHeldObject=nullptr;
 void AI();void DoAISwitchLogic();int RunTimer(int*);u16 CheckStateFlags(SStateFlags*,int);
 int DistanceToPlayer(int);int CheckSightCone(int,int,int,int,CBody*);int PlayerIsVisible();int DetermineFightState();
 void Guard();void LookConfused();void Neutralize();void MarkAIProcList(int,int,int);void CleanUpAIProcList(int);
 bool IsDead(){return (mCBodyFlags&0x40)!=0;}
 int BumpedIntoSpidey(int){std::abort();}int Hit(SHitInfo*){std::abort();return 0;}void StrikeUpConversation(){std::abort();}
 void PlaySounds(){stage(2,this);if(soundAction>=0)field_3AC=soundAction;}
 void ProcessMessages(){stage(3,this);field_31C.bothFlags=messageState;dumbAssPad=messageSub;field_3AC=7;mPos.vx+=17;}
 void WarnOtherThugs(){stage(5,this);}void ShootHostage(){stage(6,this);}
 void RunAnim(unsigned clip,int from,int to){events.insert(events.end(),{9,clip,from,to});mAnim=clip;mFrame=from;mAnimFinished=0;}
 void CycleAnim(unsigned clip,int direction){events.insert(events.end(),{10,clip,direction});mAnim=clip;mAnimFinished=0;}
 void CycleOrContinueAnim(int,int,int,int){std::abort();}int MonitorSpitPlease(){std::abort();}
 void RunAppropriateAnim(){std::abort();}int GetNextWaypoint(){std::abort();}void SetHeight(int,int,int){std::abort();}
 int PathCheck(CVector*start,CVector*end,CVector*,int clearance){events.insert(events.end(),{12,start->vx,start->vy,start->vz,end->vx,end->vy,end->vz,clearance});return path;}
 int AddPointToPath(CVector*,int){std::abort();}void Baddy_SendSignal(){std::abort();}
 void FollowWaypoints(){std::abort();}void Caution(){std::abort();}void ChasePlayer(int){std::abort();}void AttackPlayer(){std::abort();}
 void LookForPlayer(){std::abort();}void TossGrenade(){std::abort();}void ShootPlayer(){std::abort();}void TakeHit(){std::abort();}
 void Fall(){std::abort();}void GetYankedBySpidey(){std::abort();}void GetWhippedLikeTheWhoreYouAre(){std::abort();}
 void FlyAcrossRoom(){std::abort();}void GetTrapped(){std::abort();}void BackpedalPlease(){std::abort();}
 void GettingGrabbed(){std::abort();}void Acknowledge(){std::abort();}void DieThug(int){std::abort();}
''')
    code += r'''
void stage(int id,CBaddy*t){events.insert(events.end(),{id,t->field_31C.bothFlags,t->dumbAssPad,t->field_3AC,t->field_2A8,
 t->mPos.vx,t->mPos.vy,t->mPos.vz,t->mVel.vx,t->mVel.vy,t->mVel.vz,t->mShadowScale,t->mShadowPos.vx,t->mShadowPos.vy,t->mShadowPos.vz});}
void M3d_BuildTransform(CBaddy*t){stage(1,t);}
void SFX_PlayPos(unsigned sound,CVector*,int){events.insert(events.end(),{11,sound});}
int Utils_GetGroundHeight(CVector*,int,int,int){std::abort();}
int Utils_LineOfSight(CVector*start,CVector*end,CVector*,int flags){events.insert(events.end(),{13,start->vx,start->vy,start->vz,end->vx,end->vy,end->vz,flags});return los;}
enum{AI_TYPE_UNK_20000=0x20000};
struct CAIProc{CBaddy*pBaddy=nullptr;CAIProc*mNext=nullptr,*field_18=nullptr;int mAIProcType=1794,field_C=0,field_10=0,field_14=0;
 virtual~CAIProc();virtual void Execute(){stage(4,pBaddy);pBaddy->field_31C.bothFlags=finalState;pBaddy->dumbAssPad=finalSub;
 pBaddy->field_3AC=4;pBaddy->mPos.vz-=23;if(retire)field_10|=1;if(mNext)mNext->Execute();}};
struct CAIProc_LookAt{CAIProc_LookAt(CBaddy*,int,int,int,int){std::abort();}};
namespace spidey{namespace math{enum{PositionFractionBits=12};
'''
    for signature in ['inline u32 approximateDistance(', 'inline u32 approximateHorizontalDistance(']:
        code += controller_method(distance, signature)+'\n'
    code += '}}\nint my_abs(int value){return value<0?-value:value;}\nint catan(int v){return int(std::atan(v/4096.0)*651.0006103515625);}\n'
    for signature in ['MATRIX* RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)', 'MATRIX* M3dMaths_RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)', 'EXPORT void  FixedXForm(', 'i32 M3dMaths_SquareRoot0(']:
        code += controller_method(maths, signature)+'\n'
    code += 'void gte_rtir(){FixedXForm(rotation.m,&input,&result);}\n'
    for source, signature in [(utils,'int Utils_CalcAim('), (baddy,'void CBaddy::DoPhysics(i32 a2)'),
                             (baddy,'int CBaddy::RunTimer('),(baddy,'u16 CBaddy::CheckStateFlags('),
                             (baddy,'void CBaddy::MarkAIProcList('),(baddy,'void CBaddy::Neutralize('),
                             (baddy,'void CBaddy::CleanUpAIProcList('),(ai,'CAIProc::~CAIProc('),
                             (baddy,'i32 CBaddy::DistanceToPlayer('),(baddy,'i32 CBaddy::CheckSightCone('),
                             (baddy,'i32 CBaddy::PlayerIsVisible('),(thug,'i32 CThug::DetermineFightState('),
                             (thug,'void CThug::DoAISwitchLogic('),(thug,'void CThug::Guard('),
                             (thug,'void CThug::LookConfused('),(thug,'void CThug::AI(')]:
        method = controller_method(source, signature)
        if signature == 'void CBaddy::Neutralize(': method = method.replace('{','{events.push_back(14);',1)
        code += method+'\n'
    code += r'''
int main(){int table[17][2]={{2,96},{23,64},{24,64},{1,96},{26,11},{27,11},{22,27},{18,1},{14,7},{15,7},{12,1},{17,7},{19,3},{20,3},{13,5},{25,96},{0,19}};
 for(int i=0;i<17;i++){stateFlags[i].flags[0]=table[i][0];stateFlags[i].flags[1]=table[i][1];}
 int initial;while(std::cin>>initial){CBaddy t{},player{};G_MECHLIST_PLAYER=&player;G_GLOBAL_THUG=nullptr;G_THUG_LIST=nullptr;events.clear();
 int hasMessages,phase,shadow,itemflags,finished;
 std::cin>>t.mType>>t.field_3B8>>t.mElapsedTicks>>t.field_3B4>>t.field_3AC>>shadow>>itemflags>>t.field_2A8>>t.field_2AC
 >>G_ATTACK_RELATED>>phase>>t.field_36C>>hasMessages>>messageState>>messageSub>>hasProcedure>>finalState>>finalSub>>retire>>soundAction
 >>finished>>los>>path>>randomValue>>player.mCBodyFlags;
 t.field_31C.bothFlags=initial;t.dumbAssPad=0;t.mShadowScale=shadow;t.mFlags=itemflags;t.field_21D=phase;t.mAnimFinished=finished;
 t.mPos=CVector(100001,-200002,300003);t.mShadowPos=CVector(7,11,13);t.mVel=CVector(9000,-11000,13000);t.mAcc=CVector(301,-503,701);
 t.field_27C=CVector(10000,-20000,30000);t.mAngles.vx=100;t.mAngles.vy=200;t.mAngles.vz=300;
 t.mAngVel.vx=3;t.mAngVel.vy=-5;t.mAngVel.vz=7;t.mAngAcc.vx=1;t.mAngAcc.vy=2;t.mAngAcc.vz=3;
 t.mFric.Set(4,4,4);t.mAngFric.Set(4,4,4);t.field_1F8=8;t.field_230=0;
 t.field_298.Int=t.mType==304?3:0;player.mHealth=0;player.field_57C=1;player.mPos=CVector(9000000,-17000,-7000000);
 t.pMessage=hasMessages?&t:nullptr;if(hasProcedure){t.mAIProcList=new CAIProc;t.mAIProcList->pBaddy=&t;}
 t.AI();std::cout<<'['<<t.field_31C.bothFlags<<','<<t.dumbAssPad<<','<<t.field_3B4<<','<<t.field_3AC<<','<<t.field_2A8<<','<<t.mShadowScale
 <<','<<t.mShadowPos.vx<<','<<t.mShadowPos.vy<<','<<t.mShadowPos.vz<<','<<t.field_330<<','<<t.field_310<<','<<t.mCBodyFlags<<','<<t.field_208<<','<<t.field_204
 <<','<<t.field_1F8<<','<<t.mAnim<<','<<t.mFrame<<','<<t.mAnimFinished<<','<<t.mPos.vx<<','<<t.mPos.vy<<','<<t.mPos.vz
 <<','<<t.mVel.vx<<','<<t.mVel.vy<<','<<t.mVel.vz<<','<<t.field_27C.vx<<','<<t.field_27C.vy<<','<<t.field_27C.vz
 <<','<<t.mAngles.vx<<','<<t.mAngles.vy<<','<<t.mAngles.vz<<','<<t.mAngVel.vx<<','<<t.mAngVel.vy<<','<<t.mAngVel.vz<<','<<t.field_2AC<<','<<t.mCollision
 <<','<<(t.mAIProcList?1:0)<<','<<(t.mAIProcList?t.mAIProcList->field_10:-1)<<','<<t.field_1A8[0].vx<<','<<t.field_1A8[0].vy<<','<<t.field_1A8[0].vz<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 t.CleanUpAIProcList(1);
 }}
'''
    rng = random.Random(0x4D6F10)
    cases = []
    for i in range(640):
        final,sub = rng.choice([(10,0),(10,1),(19,0),(19,1),(20,0),(1,5),(28,0)])
        initial = rng.choice([0,5,14,15,17,19])
        proc = rng.randrange(2)
        message = 1
        behavior = rng.choice([0,1,8,9,0x20,0x21,0x800])  # Never enters unselected Grab leaf.
        cases.append([initial,rng.choice([304,312]),rng.choice([0x1101,0x101]),rng.choice([1,2,5]),
                      rng.choice([0,10,2147483647]),rng.choice([0,2,4,6]),rng.choice([0,1,4,5,49,50,65526,65535]),
                      rng.choice([0,4]),behavior,rng.choice([0,1,4]),rng.choice([0,1,15,16,31,32,255,2147483647]),
                      rng.choice([0,1,15,31,255]),rng.choice([0,3,15,31]),message,
                      rng.choice([8,10,19,14]) if proc else final,0 if proc else sub,proc,final,sub,rng.randrange(2),
                      rng.choice([-1,0,2]),rng.randrange(2),rng.randrange(2),rng.choice([0,2]),rng.randrange(10),rng.choice([0,0x40])])
    with tempfile.TemporaryDirectory(prefix='openspidey-brain-reference-') as folder:
        cpp = Path(folder)/'reference.cpp'; binary = Path(folder)/'reference'; cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines = subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    results = [dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-brain.json').write_text(json.dumps(results,separators=(',',':'))+'\n')
    print(f'Generated {len(results)} original AI stage/selected-dispatch cases.')


if __name__ == '__main__': main()
