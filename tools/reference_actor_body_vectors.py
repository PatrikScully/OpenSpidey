"""Compile shared CBaddy motion with source vector damping and Euler rotation."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/actors/baddy.cpp').read_text();math=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <cmath>
struct VECTOR {i32 vx,vy,vz;long pad;};struct SVECTOR{i16 vx,vy,vz,pad;};struct MATRIX{i16 m[3][3];int t[3];};
CSVector G_TRAJECTORY_VECTOR;MATRIX rotation;VECTOR input,result;
void gte_SetRotMatrix(MATRIX*m){rotation=*m;}void gte_ldlvl(VECTOR*v){input.vx=v->vx;input.vy=v->vy;input.vz=v->vz;}
void gte_stlvnl(VECTOR*v){v->vx=result.vx;v->vy=result.vy;v->vz=result.vz;}
struct CBaddy {
 CVector mPos,mVel,mAcc,field_27C,field_2B8,field_2C4,field_2D0;
 CSVector mAngles,mAngVel,mAngAcc,field_A8,field_2DC,field_2E2,field_2E8;
 CFriction mFric,mAngFric;unsigned int field_2A8,field_2AC;int mCollision=7;
 int field_2B0=0,field_2B4=0,field_230=9,mElapsedTicks,field_1F8;void DoPhysics(int);
};
'''
    for signature in ['MATRIX* RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)','MATRIX* M3dMaths_RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)','EXPORT void  FixedXForm(']:code+=controller_method(math,signature)+'\n'
    code+='void gte_rtir(){FixedXForm(rotation.m,&input,&result);}\n'+controller_method(source,'void CBaddy::DoPhysics(i32 a2)')+'\n'
    code+=r'''
int main(){int local,flag,ticks,limit,remaining,recorded,wait;
while(std::cin>>local>>flag>>ticks>>limit>>remaining>>recorded>>wait){CBaddy body;body.field_2A8=local;body.field_2AC=flag;body.mElapsedTicks=ticks;body.field_1F8=remaining;body.field_230=wait;
 std::cin>>body.mPos.vx>>body.mPos.vy>>body.mPos.vz>>body.mVel.vx>>body.mVel.vy>>body.mVel.vz>>body.mAcc.vx>>body.mAcc.vy>>body.mAcc.vz>>body.field_27C.vx>>body.field_27C.vy>>body.field_27C.vz
 >>body.mAngles.vx>>body.mAngles.vy>>body.mAngles.vz>>body.mAngVel.vx>>body.mAngVel.vy>>body.mAngVel.vz>>body.mAngAcc.vx>>body.mAngAcc.vy>>body.mAngAcc.vz;
 int shifts[6];for(int&i:shifts)std::cin>>i;body.mFric.Set(shifts[0],shifts[1],shifts[2]);body.mAngFric.Set(shifts[3],shifts[4],shifts[5]);
 body.field_2B8=body.mPos;body.field_2C4=CVector(10000,-20000,30000);body.field_2D0=CVector(101,-202,303);
 body.field_2DC=body.mAngles;body.field_2E2.vx=100;body.field_2E2.vy=200;body.field_2E2.vz=300;body.field_2E8.vx=13;body.field_2E8.vy=-27;body.field_2E8.vz=41;
 if(recorded==1)body.field_2B4=9;else if(recorded==2)body.field_2B0=9;
 std::cout<<'[';for(int frame=0;frame<6;frame++){body.DoPhysics(limit);if(frame)std::cout<<',';
 std::cout<<'['<<body.mPos.vx<<','<<body.mPos.vy<<','<<body.mPos.vz<<','<<body.mVel.vx<<','<<body.mVel.vy<<','<<body.mVel.vz
 <<','<<body.field_27C.vx<<','<<body.field_27C.vy<<','<<body.field_27C.vz<<','<<body.mAngles.vx<<','<<body.mAngles.vy<<','<<body.mAngles.vz
 <<','<<body.mAngVel.vx<<','<<body.mAngVel.vy<<','<<body.mAngVel.vz<<','<<body.field_2AC<<','<<body.mCollision<<','<<body.field_1F8<<','<<body.field_2B0<<','<<body.field_2B4<<','<<body.field_230<<']';
 }std::cout<<"]\n";
}}
'''
    rng=random.Random(0x407520);cases=[]
    for local in [False,True]:
        for flag in [0,1,4]:
            for ticks in [1,2,5]:
                for limit,remaining in [(False,10),(True,0),(True,1),(True,10),(True,-3)]:
                    for recorded in [0,1,2]:
                        for wait in [0,9]:
                            initial=[rng.randint(-20000000,20000000) for _ in range(12)]+[rng.randint(-32768,32767) for _ in range(9)]
                            shifts=[rng.choice([0,1,4,31,32,255]) for _ in range(6)]
                            cases.append(dict(local=local,flag=flag,ticks=ticks,limit=limit,remaining=remaining,recorded=recorded,wait=wait,initial=initial,shifts=shifts))
    lines=[' '.join(map(str,[int(c['local']),c['flag'],c['ticks'],int(c['limit']),c['remaining'],c['recorded'],c['wait'],*c['initial'],*c['shifts']])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-body-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-body.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} shared-body sequences / {len(cases)*6} updates.')


if __name__=='__main__':main()
