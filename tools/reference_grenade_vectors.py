"""Compile recovered CGrenade initialization, AI and destructor against typed world/effect boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/"src/game/actors/thug.cpp").read_text();vectors=(root/"src/core/fixed_vector.cpp").read_text()
    bridge=(root/"src/compatibility/fixed_vector_bridge.cpp").read_text();gte=(root/"src/engine/rendering/ps2funcs.cpp").read_text();utils=(root/"src/core/utils.cpp").read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <algorithm>
#include <cmath>
#include <iostream>
#include <vector>
using namespace spidey::core;using VECTOR=Vector;using f64=double;
VECTOR G_OP12_RESULT{},G_GENERAL_LONG_VECTOR{};
std::vector<long long> events;int randomIndex=0,randomValues[8]={};
int Rnd(int bound){events.insert(events.end(),{2,bound});return randomValues[(randomIndex++)%8]%bound;}
int G_TIMER_RELATED=0,G_TTIME=0,levelId=0;u8 G_OBJ_FILE_REGION=16,defaultRegion=9;
int Trig_GetLevelId(){return levelId;}
struct CBody{};CBody *G_BULLET_LIST=nullptr,*G_MECHLIST_PLAYER=nullptr,*G_ENVIRONMENTAL_OBJECT_LIST=nullptr,*primary=nullptr;
void*Spool_GetModel(u32 checksum,u8 region){events.insert(events.end(),{1,checksum,region});return nullptr;}
struct CGlow{
 int mSkipTriangles=0,mAngle=0,mProtected=0;
 CGlow(int a,int b){events.insert(events.end(),{3,a,b});}
 ~CGlow(){events.push_back(22);}
 void SetSkip(int value){mSkipTriangles=value;events.insert(events.end(),{4,value});}
 void SetFringeWidth(int index,int width){events.insert(events.end(),{5,index,width});}
 void SetCentreRGB(int r,int g,int b){events.insert(events.end(),{6,r,g,b});}
 void SetRGB(int r,int g,int b){events.insert(events.end(),{7,r,g,b});}
 void SetFringeRGB(int index,int r,int g,int b){events.insert(events.end(),{8,index,r,g,b});}
 void SetAngle(int value){mAngle=value;events.insert(events.end(),{9,value});}
 void SetRadius(int value){events.insert(events.end(),{10,value});}
 void SetPos(CVector p){events.insert(events.end(),{11,p.vx,p.vy,p.vz});}
 void SetProtected(int value){mProtected=value;events.insert(events.end(),{12,value});}
};
struct CGrenadeExplosion{CGrenadeExplosion(CVector*p){events.insert(events.end(),{19,p->vx,p->vy,p->vz});}};
struct CGlowFlash{template<class...Args>CGlowFlash(CVector*p,Args...args){events.insert(events.end(),{20,p->vx,p->vy,p->vz,args...});}};
int SFX_PlayPos(unsigned sound,CVector*p,int pitch){events.insert(events.end(),{16,sound,p->vx,p->vy,p->vz,pitch});return 0;}
struct LineInfo{CVector StartCoords,EndCoords,Position,Normal;int Distance=0;CBody*pItem=nullptr;}G_LINE_INFO;
int G_LINE_OF_SIGHT_CHECK=0;bool collisionHasItem=false,shadowHasItem=false;int collisionDistance=0,groundHeight=-1;CVector collisionPosition,collisionNormal,shadowPosition;
void M3dColij_InitLineInfo(LineInfo*p){p->pItem=nullptr;}
void M3dZone_LineToItem(LineInfo*p,int){
 events.insert(events.end(),{13,p->StartCoords.vx,p->StartCoords.vy,p->StartCoords.vz,p->EndCoords.vx,p->EndCoords.vy,p->EndCoords.vz,G_LINE_OF_SIGHT_CHECK});
 if(G_LINE_OF_SIGHT_CHECK){p->pItem=collisionHasItem?reinterpret_cast<CBody*>(1):nullptr;p->Position=collisionPosition;p->Normal=collisionNormal;p->Distance=collisionDistance;}
 else {p->pItem=shadowHasItem?reinterpret_cast<CBody*>(1):nullptr;p->Position=shadowPosition;}
}
int Utils_GetGroundHeight(CVector*p,int above,int below,int){events.insert(events.end(),{14,p->vx,p->vy,p->vz,above,below});return groundHeight;}
enum HitId{ALWAYS_TWENTY_NINE=29};
struct CGrenade{
 CVector mPos,mVel,mShadowPos;CSVector mAngles;u16 mCBodyFlags=0x14,mFlags=0x200,field_104=0,field_106=0,mShadowScale=32;short mShadowDist=0,mShadowThreshold=200;
 void*field_100=nullptr,*mModel=nullptr;u8 mRegion=0;HitId field_108=ALWAYS_TWENTY_NINE;int mStartTime=0,mType=0;u8 mSettled=0,mRed=0,mGreen=0,mBlue=0,mExplodeOnContact=0,field_15C=0;CGlow*mpGlow=nullptr;
 CGrenade(const CVector&,const CVector&,i32,i32,i32,CBody**,u32,u8,u8);~CGrenade();void CommonInit(CBody**,i32,u32,u8,u8);void AI();
 void AttachTo(CBody**){events.push_back(0);}void DeleteFrom(CBody**){events.push_back(23);}
 void Shadow(bool enabled){if(enabled)mCBodyFlags|=8;else mCBodyFlags&=~8;events.insert(events.end(),{15,enabled,mShadowPos.vx,mShadowPos.vy,mShadowPos.vz,mShadowScale,mShadowDist});}
 void ShadowOn(){Shadow(true);}void KillShadow(){Shadow(false);}
 void GiveScaledDamageToObjects(CBody*list,int radius,int distance,int frames,HitId kind){int listId=list==G_ENVIRONMENTAL_OBJECT_LIST?1:list==G_MECHLIST_PLAYER?2:0;
  if(field_100==&G_MECHLIST_PLAYER&&list==G_MECHLIST_PLAYER)listId=0;
  events.insert(events.end(),{17,listId,mPos.vx,mPos.vy,mPos.vz,field_104,radius,distance,frames,kind});}
 void GiveScaledDamageToEnviro(int radius){events.insert(events.end(),{18,mPos.vx,mPos.vy,mPos.vz,field_104,radius});}
 void Die(){mCBodyFlags|=0x40;events.push_back(21);}
};
'''
    for signature in ["CVector* CVector::operator+=(", "CVector* CVector::operator>>=("]:code+=controller_method(vectors,signature)
    for signature in ["void gte_ldlvl(","void gte_sqr0(","void gte_stlvnl(","i32 M3dMaths_SquareRoot0("]:code+=controller_method(gte,signature)
    code+=controller_method(bridge,"int CVector::Length(void)")
    code+=controller_method(utils,"int catan(int a1)")
    common=controller_method(thug,"void CGrenade::CommonInit(")
    common=common.replace("*gGrenadeDefaultRegion","defaultRegion").replace("this->mpGlow->mSkipTriangles = 1;","this->mpGlow->SetSkip(1);").replace("this->mpGlow->mAngle = Rnd(1024);","this->mpGlow->SetAngle(Rnd(1024));").replace("this->mpGlow->mProtected = 1;","this->mpGlow->SetProtected(1);")
    code+=common
    code+=controller_method(thug,"CGrenade::CGrenade(")
    code+=controller_method(thug,"CGrenade::~CGrenade(void)")
    code+=controller_method(thug,"void CGrenade::AI(void)")
    code+=r'''
int main(){long long v[58];while(std::cin>>v[0]){
 for(int i=1;i<58;i++)std::cin>>v[i];G_TIMER_RELATED=v[1];G_TTIME=v[2];levelId=v[3];
 CBody player,env,target;G_MECHLIST_PLAYER=&player;G_ENVIRONMENTAL_OBJECT_LIST=&env;primary=&target;
 for(int i=0;i<8;i++)randomValues[i]=v[50+i];randomIndex=0;events.clear();
 CVector start{int(v[12]),int(v[13]),int(v[14])},end{int(v[15]),int(v[16]),int(v[17])};
 CGrenade*g=new CGrenade(start,end,v[4],v[5],v[6],v[7]?&G_MECHLIST_PLAYER:&primary,v[8],v[9],v[10]);
 if(v[0]!=0){
  events.clear();randomIndex=0;g->mPos={int(v[18]),int(v[19]),int(v[20])};g->mVel={int(v[21]),int(v[22]),int(v[23])};g->mAngles={i16(v[24]),i16(v[25]),i16(v[26])};g->mSettled=v[27];g->mStartTime=v[28];
  g->mRed=v[29];g->mGreen=v[30];g->mBlue=v[31];collisionHasItem=v[32];collisionDistance=v[33];collisionPosition={int(v[34]),int(v[35]),int(v[36])};collisionNormal={int(v[37]),int(v[38]),int(v[39])};
  shadowHasItem=v[40];shadowPosition={int(v[41]),int(v[42]),int(v[43])};groundHeight=v[44];g->mShadowPos={1,2,3};g->mShadowScale=v[45];g->mShadowDist=v[46];g->mCBodyFlags=v[47];
  for(int i=0;i<v[48];i++)g->AI();
 }
 std::cout<<"{\"state\":["<<g->mPos.vx<<","<<g->mPos.vy<<","<<g->mPos.vz<<","<<g->mVel.vx<<","<<g->mVel.vy<<","<<g->mVel.vz<<","<<g->mAngles.vx<<","<<g->mAngles.vy<<","<<g->mAngles.vz<<","<<int(g->mSettled)<<","<<g->mStartTime<<","<<g->mCBodyFlags<<","<<g->mShadowPos.vx<<","<<g->mShadowPos.vy<<","<<g->mShadowPos.vz<<","<<g->mShadowScale<<","<<g->mShadowDist<<","<<g->field_104<<","<<g->field_106<<","<<(v[7]?63:64)<<","<<int(g->mRegion)<<"],\"events\":[";
 if(v[49])delete g;
 for(size_t i=0;i<events.size();i++){if(i)std::cout<<",";std::cout<<events[i];}std::cout<<"]}\n";
 if(!v[49])delete g;
}}
'''
    def case(**changes):
        values=dict(op=1,timer=100,total_time=2,level=1,speed=35,damage=10,radius=512,player_targets=0,checksum=1989287857,contact=0,flag=0,reserved=0,
                    sx=10000,sy=-20000,sz=30000,tx=4096000,ty=-100000,tz=8192000,
                    x=10000,y=-20000,z=30000,vx=81920,vy=-81920,vz=40960,pitch=123,yaw=456,roll=789,settled=0,start_time=0,
                    red=100,green=50,blue=0,collision=0,collision_distance=10,cx=12345,cy=-23456,cz=34567,nx=0,ny=-4096,nz=0,
                    shadow=1,shx=10000,shy=200000,shz=30000,ground=-1,shadow_scale=32,shadow_distance=10,body=0x14,updates=1,dispose=0,
                    r0=2,r1=8,r2=1,r3=25,r4=49,r5=12,r6=38,r7=7)
        values.update(changes);return list(values.values())
    cases=[]
    for level in [1,0x1101]:
        for checksum in [0,1989287857,0xfffffffe]:
            for player_targets in [0,1]:
                for speed in [1,33,40,0x7fffffff]:
                    cases.append(case(op=0,level=level,checksum=checksum,player_targets=player_targets,speed=speed))
    for age in [-1,0,90,91,120,121]:
        for settled in [0,1]:
            for time in [0,2]:
                for player_targets in [0,1]:
                    for shadow in [0,1]:cases.append(case(timer=age,settled=settled,total_time=time,player_targets=player_targets,shadow=shadow))
    for level in [1,0x1101]:
        for contact in [0,1]:
            for normal_y in [-4096,-3501,-3500,0,4096]:
                for vy in [-245760,-114688,-32768,0,245760]:
                    for distance in [0,10,1000]:cases.append(case(level=level,contact=contact,collision=1,ny=normal_y,vy=vy,collision_distance=distance,ground=456789))
    for shadow_distance in [0,304,305,306,1000,32767,32768,-32768]:
        cases.append(case(settled=1,y=0,shy=81920+(shadow_distance<<12)))
    for red,green,blue in [(0,127,128),(127,128,255),(255,255,255)]:cases.append(case(red=red,green=green,blue=blue))
    rng=random.Random(0x4DBDB0)
    for _ in range(128):
        coordinate=lambda:rng.choice([-2147480000,-500000,0,500000,2147480000])
        cases.append(case(x=coordinate(),y=coordinate(),z=coordinate(),vx=coordinate(),vy=coordinate(),vz=coordinate(),collision=rng.randrange(2),
                          nx=rng.choice([-4096,0,4096]),ny=rng.choice([-4096,0,4096]),nz=rng.choice([-4096,0,4096]),
                          timer=rng.choice([-2147483648,0,120,121,2147483647]),start_time=rng.choice([-2147483648,0,2147483647]),updates=rng.choice([1,2]),dispose=rng.randrange(2)))
    cases.append(case(op=0,sx=0,sy=0,sz=0,tx=0,ty=999999,tz=0,speed=0,dispose=1))
    cases.append(case(op=0,damage=65537,radius=70000,contact=255,flag=255,dispose=1))
    with tempfile.TemporaryDirectory(prefix="grenade-reference-") as temporary:
        directory=Path(temporary);(directory/"reference.cpp").write_text(code)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv","-I",str(root/"src"),str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result=subprocess.run([str(directory/"reference")],input="\n".join(" ".join(map(str,row)) for row in cases),capture_output=True,text=True,check=True)
    rows=[{"input":inputs,**json.loads(output)} for inputs,output in zip(cases,result.stdout.splitlines(),strict=True)]
    destination=root/"modern/OpenSpidey.Tests/Fixtures/grenade.json"
    destination.write_text(json.dumps(rows,separators=(",",":"))+"\n")
    print(f"Compiled CGrenade lifecycle: {len(rows)} comparisons written to {destination}")


if __name__=="__main__":main()
