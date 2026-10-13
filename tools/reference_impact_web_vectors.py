"""Compile actual CImpactWeb constructor/Move; effects/world are explicit observed host boundaries."""
import json, math, random, subprocess, tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/player/web.cpp').read_text();utils=(root/'src/core/utils.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <cmath>
#include <iostream>
#include <vector>
using VECTOR=CVector;using SVECTOR=CSVector;
struct SinCos{short sin,cos;};SinCos G_RCOSSIN_TBL[4096];
std::vector<long long>events;void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
int G_TIMER_RELATED,G_LINE_OF_SIGHT_CHECK=0,G_LOW_MEMORY=0,randomIndex=0,bodyHitFrame,updateIndex=0,initialHit,groundHit,lowAfter,lowReads=0;
int Rnd(int n){int v=(randomIndex++*7+1)%n;emit({1,n,v});return v;}
void print_if_false(bool,const char*){}
struct SHitInfo{int field_0=0,field_4=0;unsigned short field_8=0;CVector field_C;short field_18=0;unsigned short field_1A=0;};
struct CItem{unsigned short mFlags=0;CItem*mNextItem=nullptr;};
struct CBody:CItem{void Hit(SHitInfo*h){emit({7,h->field_0,h->field_4,h->field_8,h->field_C.vx,h->field_C.vy,h->field_C.vz,h->field_18,h->field_1A});}};
struct CPlayer{int field_534=0,field_528=5,field_52C=0;int GetDamageInflictedFromDifficulty(int n){emit({2,n});return n+77;}void SetFirstContactDetails(){emit({6,field_534,field_52C});}}player;
CPlayer*G_MECHLIST=&player,*G_MECHLIST_PLAYER=&player;CBody environment,body;CItem*EnviroList=&environment;
struct SLineInfo{CVector StartCoords,EndCoords,Position;CSVector Normal;CItem*pItem=nullptr;unsigned*pFace=nullptr;int Distance=0;}G_LINE_INFO;
unsigned face[4]={};CVector initialPosition;CSVector initialNormal;int initialDistance;
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*l,int mode){emit({3,mode,G_LINE_OF_SIGHT_CHECK,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz});
if(mode==0){l->pItem=initialHit?&environment:nullptr;l->pFace=face;l->Position=initialPosition;l->Normal=initialNormal;l->Distance=initialDistance;}
else{l->pItem=groundHit?&environment:nullptr;l->Position.vy=-123456;}}
CBody*Utils_CheckObjectCollision(CVector*a,CVector*b,void*,int){emit({4,a->vx,a->vy,a->vz,b->vx,b->vy,b->vz});return updateIndex==bodyHitFrame?&body:nullptr;}void*G_BADDY_LIST=nullptr;
void SFX_PlayPos(int id,CVector*p,int pitch){emit({5,id,p->vx,p->vy,p->vz,pitch});}
struct CSwitch{bool field_100=true;void Flick(){emit({9});}}switchActor;CSwitch*Switch_GetCSwitchObjectFromItem(CItem*){emit({8});return &switchActor;}
struct CSplat{CSplat(CVector&p,SVECTOR&n){emit({10,p.vx,p.vy,p.vz,n.vx,n.vy,n.vz});}};
struct CImpactRing{CImpactRing(CVector&p,int r,int g,int b,int s,int rate){emit({11,p.vx,p.vy,p.vz,r,g,b,s,rate});}};
struct CWebFrag{CWebFrag(int ground,CVector&p,CVector&a,CVector&b,CVector&owner,int speed,int jitter){emit({12,ground,p.vx,p.vy,p.vz,a.vx,a.vy,a.vz,b.vx,b.vy,b.vz,owner.vx,owner.vy,owner.vz,speed,jitter});}};
struct CImpactWeb{CVector mPos,mVel,mHitPos;CSVector mHitNormal;int mDamage=0,mStartTime=0;CItem*mpHitItem=nullptr;unsigned*mpHitFace=nullptr;unsigned short mLifetime=0;short mAge=0,mScale=0,mAngle=0,field_5A=0;unsigned mPostScale=0;bool dead=false;
CImpactWeb(const CVector&,const CSVector&,int,int,int);void Move();void SetAnim(int){}void SetFrame(int){}void SetScale(int v){mScale=v;}void Die(){dead=true;}
};
'''
    code+=controller_method(utils,'void Utils_GetVecFromMagDir(')+'\n'
    code+=controller_method((root/'src/engine/rendering/ps2funcs.cpp').read_text(),'void VectorNormal(').replace('VECTOR','CVector')+'\n'
    code+=controller_method(source,'CImpactWeb::CImpactWeb(')+'\n'
    move=controller_method(source,'void CImpactWeb::Move(').replace('if (G_LOW_MEMORY)','if (lowReads++ >= lowAfter)')
    code+=move+r'''
void output(CImpactWeb&s){std::cout<<'['<<s.mPos.vx<<','<<s.mPos.vy<<','<<s.mPos.vz<<','<<s.mVel.vx<<','<<s.mVel.vy<<','<<s.mVel.vz<<','<<s.mDamage<<','<<unsigned(s.mStartTime)<<','<<s.mLifetime<<','<<s.mAge<<','<<s.mScale<<','<<s.mAngle<<','<<s.field_5A<<','<<s.dead<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}
int main(){for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.;}
CVector pos;CSVector dir;int speed,damage,lifetime,flags,switchable,seed;unsigned clock,delta;
while(std::cin>>pos.vx>>pos.vy>>pos.vz>>dir.vx>>dir.vy>>dir.vz>>speed>>damage>>lifetime>>clock>>initialHit>>initialDistance>>flags>>face[3]>>initialPosition.vx>>initialPosition.vy>>initialPosition.vz>>initialNormal.vx>>initialNormal.vy>>initialNormal.vz>>bodyHitFrame>>groundHit>>lowAfter>>switchable>>delta>>seed){
G_TIMER_RELATED=(int)clock;environment.mFlags=flags;switchActor.field_100=switchable;events.clear();randomIndex=seed;CImpactWeb s(pos,dir,speed,damage,lifetime);std::cout<<'[';output(s);
for(updateIndex=0;updateIndex<8;updateIndex++){clock+=delta;G_TIMER_RELATED=(int)clock;events.clear();lowReads=0;if(!s.dead)s.Move();std::cout<<',';output(s);}std::cout<<"]\n";}}
'''
    rng=random.Random(0x4F9940);cases=[]
    for i in range(128):
        cases.append([*[rng.randint(-50000000,50000000) for _ in range(3)],rng.choice([0,1024,2048,3072,4095]),rng.randrange(4096),0,rng.choice([1,32,200]),rng.choice([50,65530]),rng.choice([0,1,3,30,120,65535]),rng.choice([0,4294967290]),rng.randrange(2),rng.choice([-1,0,32,96,10000]),rng.choice([0,1]),rng.choice([0,0x2000000]),12345,-54321,67890,0,-4096,0,rng.choice([-1,0,1,3]),rng.randrange(2),rng.choice([0,1,2,30]),rng.randrange(2),rng.choice([0,1,2,40000]),rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-impact-web-') as d:
        cpp=Path(d)/'reference.cpp';binary=Path(d)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/impact-web.json').write_text(json.dumps([dict(input=c,expected=json.loads(l)) for c,l in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} original constructor and 1024 Move samples, typed world/effects callbacks observed.')

if __name__=='__main__':main()
