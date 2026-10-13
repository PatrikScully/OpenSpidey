"""Compile full original CManipOb::Chunk, six debris constructors and CChunkSmoke constructor/Move over explicit effects boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent;source=(root/'src/game/objects/manipob.cpp').read_text();maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text();utils=(root/'src/core/utils.cpp').read_text();effects=(root/'src/game/effects/effects.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <cmath>
#include <cstdlib>
#include <new>
#include <cstdint>
using VECTOR=Vector;std::vector<long long>events;unsigned seed;int allocation=1,ground=-1;
int Rnd(int range){events.insert(events.end(),{1,range});seed=seed*1664525u+1013904223u;return range>0?int((seed>>1)%unsigned(range)):0;}
VECTOR G_GENERAL_LONG_VECTOR,G_OP12_RESULT,G_WTF_OP12,G_FTW_OP12;void gte_stlvnl(VECTOR*p){*p=G_GENERAL_LONG_VECTOR;}
struct SSinCos{short sin,cos;};SSinCos G_RCOSSIN_TBL[4096];CVector ZeroVector;
struct SLineInfo{CSVector Normal;};struct SHitInfo{u8 field_0;int field_4;u16 field_8;CVector field_C;short field_18;u16 field_1A;};
struct CBaddy{int id,mType;CVector mPos;unsigned mCBodyFlags;CBaddy*mNextItem=nullptr;void Hit(SHitInfo*h){events.insert(events.end(),{5,id,h->field_0,h->field_8,h->field_C.vx,h->field_C.vy,h->field_C.vz});if(h->field_0&16)events.insert(events.end(),{h->field_18,h->field_1A});}};
using CBody=CBaddy;struct CSimby:CBaddy{void ExplosionReaction(){events.insert(events.end(),{6,id});}};CSimby bodies[4];CBaddy*BaddyList=nullptr,*G_MECHLIST=nullptr;
struct SMessageData{int mNumParams,mNumVects;int*pVects[1];};struct CMessage{CMessage(int,CBaddy*b,int kind,SMessageData*d){events.insert(events.end(),{10,b->id,kind,d->mNumParams,d->mNumVects,d->pVects[0][0],d->pVects[0][1],d->pVects[0][2]});}};
int Utils_GetGroundHeight(CVector*p,int above,int below,int flags){events.insert(events.end(),{11,p->vx,p->vy,p->vz,above,below,flags});return ground;}
void SFX_PlayPos(unsigned sound,CVector*p,int volume){events.insert(events.end(),{12,sound,p->vx,p->vy,p->vz,volume});}
void Exp_Big3DExplosion(CVector*p){events.insert(events.end(),{13,p->vx,p->vy,p->vz});}
struct Camera{CVector Position;};Camera G_MIKE_CAMERA[1];
std::vector<void*>allocated;void*alloc(size_t size,bool fail){if(fail)return nullptr;void*p=std::calloc(1,size);allocated.push_back(p);return p;}
struct CClass{static void*operator new(size_t size){return alloc(size,!allocation);}};
struct CBit{static void*operator new(size_t size){return alloc(size,false);}};
int G_OBJ_FILE=0,gObjFileRegion=7;unsigned Spool_GetModel(unsigned model,int){return model;}
struct CManipObChunk{CVector mPos,mVel,mAcc;CSVector mAngles,mAngVel;CFriction mFric;u16 mFlags=0,mRMinor=0,mType=0;int field_FC=0;unsigned mModel=0;CManipObChunk(u32,CVector*,CVector*);void InitItem(int){}void AttachTo(void*){events.push_back(15);}};
struct CChunkSmoke{CVector mPos,mVel,field_68;short mAngle=0,field_5A=0,mScale=0,mAge=0;int field_7C=0,field_78=0,field_74=0;u32 mCodeBGR=0;int mNumFrames=1,mFrame=0;int mpPSXAnim[1]={};int*mpPSXFrame=nullptr;bool dead=false;CChunkSmoke(CVector*,CVector*,int);void Move();void SetSemiTransparent(){}void SetAnim(unsigned){}void Die(){dead=true;}};
#define DoAssert(...) ((void)0)
u16 links[1]={9};u16*Trig_GetLinksPointer(u16 node){links[0]=node;return links;}void Trig_SendPulse(u16*p){events.insert(events.end(),{14,p[0]});}
struct CManipOb{CVector mPos,mVel;short field_104=0;int field_10C=0;uintptr_t field_120=0;u8 field_124=0;u16 mNode=9;void Chunk(SLineInfo*,CVector*);void SendPulse();};
'''
    for sig in ['i32 M3dMaths_SquareRoot0(','void VectorNormal(','void gte_ldopv1(','void gte_ldopv2(','void gte_op12(']:code+=controller_method(maths,sig)+'\n'
    for sig in ['u32 Utils_Dist(','i32 Utils_XZDist(','void Utils_CalcPerps(']:code+=controller_method(utils,sig)+'\n'
    code+=controller_method((root/'src/engine/world/bit.cpp').read_text(),'void Bit_ReduceRGB(')+'\n'
    code+=controller_method(source,'CManipObChunk::CManipObChunk(').replace('\n}',r'''events.insert(events.end(),{7,mModel,mPos.vx,mPos.vy,mPos.vz,mVel.vx,mVel.vy,mVel.vz,mAngles.vx,mAngles.vy,mAngVel.vx,mAngVel.vy,field_FC});
}''')+'\n'
    code+=controller_method(effects,'CChunkSmoke::CChunkSmoke(').replace('\n}',r'''events.insert(events.end(),{8,mPos.vx,mPos.vy,mPos.vz,field_68.vx,field_68.vy,field_68.vz,a4,field_7C,mAngle,field_5A,field_78,field_74,mVel.vy});
}''')+'\n'
    code+=controller_method(effects,'void CChunkSmoke::Move(')+'\n'
    for sig in ['static void SendExplosionMessageToNearby(','static void SpawnChunkSmokeBurst(','void CManipOb::Chunk(',' void CManipOb::SendPulse(']:code+=controller_method(source,sig)+'\n'
    code+=r'''
void jsonEvents(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(){for(int i=0;i<4096;i++){double a=i*0.001536096911877394;G_RCOSSIN_TBL[i].sin=std::sin(a)*4096.0;G_RCOSSIN_TBL[i].cos=std::cos(a)*4096.0;}
int op;while(std::cin>>op){if(op==0){CManipOb s;SLineInfo line;int pulse,count;std::cin>>s.mPos.vx>>s.mPos.vy>>s.mPos.vz>>s.mVel.vx>>s.mVel.vy>>s.mVel.vz>>s.field_10C>>s.field_104>>pulse>>ground>>allocation>>seed>>line.Normal.vx>>line.Normal.vy>>line.Normal.vz>>G_MIKE_CAMERA[0].Position.vx>>G_MIKE_CAMERA[0].Position.vy>>G_MIKE_CAMERA[0].Position.vz>>count;std::vector<int>models(count+1);for(int i=0;i<count;i++)std::cin>>models[i];s.field_120=reinterpret_cast<uintptr_t>(models.data());s.field_124=pulse;
for(int i=0;i<4;i++){bodies[i].id=i;std::cin>>bodies[i].mType>>bodies[i].mCBodyFlags>>bodies[i].mPos.vx>>bodies[i].mPos.vy>>bodies[i].mPos.vz;bodies[i].mNextItem=i<3?&bodies[i+1]:nullptr;}BaddyList=&bodies[1];G_MECHLIST=&bodies[0];events.clear();s.Chunk(&line,nullptr);std::cout<<'['<<int(s.field_124)<<',';jsonEvents();std::cout<<"]\n";for(void*p:allocated)std::free(p);allocated.clear();
}else{CVector p,o;int side;std::cin>>p.vx>>p.vy>>p.vz>>o.vx>>o.vy>>o.vz>>side>>seed;events.clear();CChunkSmoke s(&p,&o,side);std::cout<<'[';jsonEvents();std::cout<<",[";for(int i=0;i<48;i++){if(!s.dead)s.Move();if(i)std::cout<<',';std::cout<<'['<<s.mPos.vx<<','<<s.mPos.vy<<','<<s.mPos.vz<<','<<s.mAngle<<','<<s.field_5A<<','<<s.mScale<<','<<s.mAge<<','<<s.mCodeBGR<<','<<s.dead<<']';}std::cout<<"]]\n";}}
}
'''
    rng=random.Random(0x456e90);cases=[]
    for i in range(110):
        pos=[rng.randint(-20000,20000)*4096+rng.randrange(4096) for _ in range(3)]
        models=rng.choice([[],[0x7640e7bd],[0x7640e7bd,0xef49b607,0x984e8691,0x062a1332]])
        bodies=[]
        for j in range(4):bodies.append([rng.choice([304,324,401]),rng.choice([0,0x200,0x1000,0x1200]),*[p+rng.choice([-1024,-1023,-300,-299,-1,0,299,300,1023,1024,1999,2000])*4096 for p in pos]])
        cases.append([0,*pos,*[rng.randint(-300000,300000) for _ in range(3)],rng.choice([0,8,32,40]),rng.choice([0,1,32767,-32768]),rng.randrange(2),rng.choice([-1,pos[1],pos[1]+100000]),rng.randrange(2),rng.randrange(2**32),*rng.choice([[0,-4096,0],[0,-3000,0],[0,-2999,0],[4096,0,0],[1000,2000,3000],[0,0,0],[-2000,1000,3000]]),*[rng.randint(-20000,20000) for _ in range(3)],len(models),*[v if v<0x80000000 else v-0x100000000 for v in models],*[v for body in bodies for v in body]])
    for i in range(70):cases.append([1,*[rng.randint(-2147483648,2147483647) for _ in range(6)],rng.choice([-1,0,1]),rng.randrange(2**32)])
    with tempfile.TemporaryDirectory(prefix='openspidey-manipulation-chunk-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code);subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,row)) for row in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/manipulation-chunk.json').write_text(json.dumps([dict(input=row,expected=json.loads(line)) for row,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n');print(f'Generated{len(cases)} compiled fullChunk/smoke cases incl allocation failure, damage boundaries, all6debris and24smoke constructors.')

if __name__=='__main__':main()
