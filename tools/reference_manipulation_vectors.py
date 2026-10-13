"""Compile original CManipOb helper methods and CManipObChunk lifetime/physics over explicit world boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/objects/manipob.cpp').read_text()
    maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <cmath>
#include <cstdlib>
using VECTOR=Vector;
VECTOR G_GENERAL_LONG_VECTOR,G_OP12_RESULT;void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}
std::vector<int> events;int randomSeed=0;
int Rnd(int range){events.insert(events.end(),{1,range});unsigned value=unsigned(randomSeed)*1664525u+1013904223u;randomSeed=int(value);return range>0?int((value>>1)%unsigned(range)):0;}
short table[8192];short*word_610C48=table;
void*DCMem_New(unsigned size,int,int,void*,int){return std::calloc(1,size);}
struct CItem{};struct CManipOb{
 CVector mPos,mVel,mAcc,field_110;CSVector mAngles,mAngVel;CFriction mFric;u16 mFlags=0,mNode=9,mCBodyFlags=0x16;int field_10C=0,mNumVectors=0,field_100=0;CVector*pVectors=nullptr;CItem*field_11C=nullptr;u8 field_124=0;
 u8 TugImpulse(CVector*,CVector*);void GetAttachPoint(CVector*);void Drop(CVector*);void Throw(CVector*);void ThrowPos(CVector*,int);void CalculateThrowPositionArray(CVector*,int);void Pickup();void SendPulse();void TurnOffShadow(){if(field_11C)events.push_back(2);}
};
u16 pulse[1]={9};u16*Trig_GetLinksPointer(u16 node){pulse[0]=node;return pulse;}void Trig_SendPulse(u16*p){events.insert(events.end(),{3,p[0]});}
struct SLineInfo{CVector StartCoords,EndCoords,MinCoords,MaxCoords,Position;CSVector Normal;CItem*pItem=nullptr;};
CVector hitPosition;CSVector hitNormal;int hit=0;CItem world;
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*l,int mode){events.insert(events.end(),{4,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz,mode});l->pItem=hit?&world:nullptr;l->Position=hitPosition;l->Normal=hitNormal;}
struct CManipObChunk{
 CVector mPos,mVel,mAcc;CSVector mAngles,mAngVel;CFriction mFric;u16 mCBodyFlags=0x16;int field_F8=0,field_FC=0,mElapsedTicks=0;
 void AI();void DoPhysics();void Die(){mCBodyFlags|=0x40;}
};
'''
    for signature in ['i32 M3dMaths_SquareRoot0(','void VectorNormal(','void gte_ldlvl(','void gte_sqr0(']:code+=controller_method(maths,signature)+'\n'
    code+=controller_method((root/'src/compatibility/fixed_vector_bridge.cpp').read_text(),'int CVector::Length(')+'\n'
    for signature in ['u8 CManipOb::TugImpulse(','void CManipOb::GetAttachPoint(','void CManipOb::Drop(','void CManipOb::CalculateThrowPositionArray(','void CManipOb::ThrowPos(','void CManipOb::Throw(',' void CManipOb::SendPulse(','void CManipOb::Pickup(','void CManipObChunk::AI(','void CManipObChunk::DoPhysics(']:code+=controller_method(source,signature)+'\n'
    code+=r'''
void vector(CVector v){std::cout<<v.vx<<','<<v.vy<<','<<v.vz;}
void angles(CSVector v){std::cout<<v.vx<<','<<v.vy<<','<<v.vz;}
void eventJson(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(){for(int i=0;i<4096;i++){double a=i*0.001536096911877394;table[i*2]=std::sin(a)*4096.0;table[i*2+1]=std::cos(a)*4096.0;}
int op;while(std::cin>>op){CManipOb s;CVector point,velocity;int count,pulseSent,shadow;
std::cin>>s.mPos.vx>>s.mPos.vy>>s.mPos.vz>>s.mVel.vx>>s.mVel.vy>>s.mVel.vz>>s.mAcc.vx>>s.mAcc.vy>>s.mAcc.vz>>s.mAngles.vx>>s.mAngles.vy>>s.mAngles.vz>>s.mAngVel.vx>>s.mAngVel.vy>>s.mAngVel.vz>>s.mFlags>>s.field_10C>>pulseSent>>shadow>>randomSeed>>point.vx>>point.vy>>point.vz>>velocity.vx>>velocity.vy>>velocity.vz>>count;
s.field_124=pulseSent;s.field_11C=shadow?&world:nullptr;events.clear();
if(op<=5){if(op==0)s.TugImpulse(&point,&velocity);else if(op==1)s.Drop(&velocity);else if(op==2)s.Throw(&velocity);else if(op==3){s.Pickup();s.Pickup();}else if(op==4)s.ThrowPos(&point,count);else s.CalculateThrowPositionArray(&point,count);
CVector attach;s.GetAttachPoint(&attach);std::cout<<'[';vector(s.mPos);std::cout<<',';vector(s.mVel);std::cout<<',';vector(s.mAcc);std::cout<<',';angles(s.mAngles);std::cout<<',';angles(s.mAngVel);std::cout<<','<<s.mFlags<<','<<s.field_10C<<','<<int(s.field_124)<<',';vector(attach);std::cout<<','<<s.mNumVectors<<','<<s.field_100<<",[";for(int i=0;i<s.mNumVectors;i++){if(i)std::cout<<',';std::cout<<'[';vector(s.pVectors[i]);std::cout<<']';}std::cout<<"],";eventJson();std::cout<<"]\n";std::free(s.pVectors);
}else{
CManipObChunk d;d.mPos=s.mPos;d.mVel=s.mVel;d.mAcc=s.mAcc;d.mAngles=s.mAngles;d.mAngVel=s.mAngVel;d.mFric.vx=d.mFric.vy=d.mFric.vz=12;d.field_FC=count;d.field_F8=pulseSent;hitPosition=point;hitNormal=CSVector(velocity.vx,velocity.vy,velocity.vz);
std::cout<<'[';for(int i=0;i<6;i++){std::cin>>d.mElapsedTicks>>hit;events.clear();d.AI();if(i)std::cout<<',';std::cout<<'[';vector(d.mPos);std::cout<<',';vector(d.mVel);std::cout<<',';angles(d.mAngles);std::cout<<',';angles(d.mAngVel);std::cout<<','<<d.field_FC<<','<<d.field_F8<<','<<d.mCBodyFlags<<',';eventJson();std::cout<<']';}std::cout<<"]\n";
}}
}
'''
    rng=random.Random(0x456920);cases=[]
    for op in range(7):
        for index in range(70):
            pos=[rng.randint(-5000000,5000000) for _ in range(3)]
            vel=[rng.randint(-200000,200000) for _ in range(3)]
            acc=[rng.randint(-5000,5000) for _ in range(3)]
            angles=[rng.randint(-32768,32767) for _ in range(3)]
            angular=[rng.randint(-128,128) for _ in range(3)]
            pulse=rng.choice([0,1,255]) if op<6 else rng.choice([-2,-1,0,1,2147483647])
            point=[rng.randint(-8000000,8000000) for _ in range(3)]
            targetVelocity=[rng.randint(-100000,100000) for _ in range(3)] if op<6 else rng.choice([[0,-4096,0],[4096,0,0],[0,4096,0],[32768,65535,-65536]])
            count=rng.choice([2,3,4,7,16,63]) if op<6 else rng.choice([0,1,2,5,60,90])
            row=[op,*pos,*vel,*acc,*angles,*angular,rng.randrange(65536),rng.randrange(128),pulse,rng.randrange(2),rng.randint(-2147483648,2147483647),*point,*targetVelocity,count]
            if op==6:row.extend(v for _ in range(6) for v in [rng.choice([0,1,2,5,-1]),rng.randrange(2)])
            cases.append(row)
    with tempfile.TemporaryDirectory(prefix='openspidey-manipulation-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,row)) for row in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/manipulation.json').write_text(json.dumps([dict(input=row,expected=json.loads(line)) for row,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source helper/debris cases; world hits and allocation remain explicit boundaries; missing manip AI is not covered by extracted source.')

if __name__=='__main__':main()
