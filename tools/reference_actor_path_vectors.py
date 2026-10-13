"""Compile original enemy capsule path tests, bounded point insertion and 64-phase offsets."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/actors/baddy.cpp').read_text()
    maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <cmath>
#include <iostream>
#include <vector>
using VECTOR=Vector;
int G_BADDY_COLLISION_CHECK=0,pattern=0,factor=0,groundFail=-1,losCalls=0,groundCalls=0;
std::vector<std::vector<long long>> losEvents,groundEvents;CVector waypointPosition;
struct CBaddy {
 CVector mPos,field_1A8[6];unsigned int field_2A8=0;u16 mRMinor=0;i16 field_21E=0;
 int field_29C=0,field_1F4=-1,field_1F0=0;
 int PathCheckGuts(CVector*,CVector*,CVector*,int);int PathCheck(CVector*,CVector*,CVector*,int);
 int AddPointToPath(CVector*,int);void GetWaypointNearTarget(CVector*,int,int,CVector*);
};
void print_if_false(bool,const char*){}
int Utils_LineOfSight(CVector*from,CVector*to,CVector*hit,int flags){
 bool clear=(pattern&(1<<(losCalls++&31)))==0;
 if(!clear)*hit=*from+((*to-*from)>>2)*factor;
 losEvents.push_back({from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,flags,G_BADDY_COLLISION_CHECK,clear?1:0,hit->vx,hit->vy,hit->vz});return clear;
}
int Utils_GetGroundHeight(CVector*at,int above,int below,void*){
 groundEvents.push_back({at->vx,at->vy,at->vz,above,below,G_BADDY_COLLISION_CHECK});
 return groundCalls++==groundFail?-1:1234;
}
void Trig_GetPosition(CVector*out,int){*out=waypointPosition;}
'''
    export=(root/'src/compatibility/export.h').read_text()
    code+=next(line for line in export.splitlines() if line.startswith('#define my_abs'))+'\n'
    code+=controller_method(maths,'i32 M3dMaths_SquareRoot0(')+'\n'
    code+=controller_method(maths,'void VectorNormal(')+'\n'
    code+='VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR;\n'
    code+='void gte_ldlvl(VECTOR*v){G_OP12_RESULT=*v;}void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}\n'
    code+=controller_method(maths,'void gte_sqr0(')+'\n'
    bridge=(root/'src/compatibility/fixed_vector_bridge.cpp').read_text()
    code+=controller_method(bridge,'int CVector::Length(')+'\n'
    for signature in ['i32 CBaddy::PathCheckGuts(', 'i32 CBaddy::PathCheck(', 'i32 CBaddy::AddPointToPath(', 'void CBaddy::GetWaypointNearTarget(']:
        code+=controller_method(source,signature)+'\n'
    code+=r'''
void emitVector(CVector v){std::cout<<'['<<v.vx<<','<<v.vy<<','<<v.vz<<']';}
void emitEvents(std::vector<std::vector<long long>>&events){std::cout<<'[';for(unsigned int i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned int j=0;j<events[i].size();j++){if(j)std::cout<<',';std::cout<<events[i][j];}std::cout<<']';}std::cout<<']';}
int main(){int mode,radius,clearance,limit,phase,distance,want;CBaddy b;CVector start,end,hit;
while(std::cin>>mode>>b.field_2A8>>radius>>clearance>>b.field_29C>>b.field_1F4>>b.field_1F0>>G_BADDY_COLLISION_CHECK>>pattern>>factor>>groundFail>>limit>>phase>>distance>>want){
 b.mRMinor=radius;b.field_21E=clearance;
 std::cin>>b.mPos.vx>>b.mPos.vy>>b.mPos.vz>>start.vx>>start.vy>>start.vz>>end.vx>>end.vy>>end.vz>>hit.vx>>hit.vy>>hit.vz>>waypointPosition.vx>>waypointPosition.vy>>waypointPosition.vz;
 for(CVector&point:b.field_1A8)std::cin>>point.vx>>point.vy>>point.vz;
 losCalls=0;groundCalls=0;losEvents.clear();groundEvents.clear();int result=0;
 if(mode==0){result=b.PathCheck(&start,&end,want?&hit:nullptr,limit);if(!want)hit=CVector();}
 else if(mode==1){result=b.AddPointToPath(&end,limit);hit=CVector();}
 else b.GetWaypointNearTarget(&end,distance,phase,&hit);
 std::cout<<'['<<result<<',';emitVector(hit);std::cout<<','<<G_BADDY_COLLISION_CHECK<<','<<b.field_2A8<<','<<b.field_1F0<<",[";
 for(int i=0;i<6;i++){if(i)std::cout<<',';emitVector(b.field_1A8[i]);}std::cout<<"],";emitEvents(losEvents);std::cout<<',';emitEvents(groundEvents);std::cout<<"]\n";
}}
'''
    rng=random.Random(0x403350);cases=[]
    def add(mode=0,behavior=0,radius=150,clearance=100,stored=0,waypoint=-1,count=0,collision=7,pattern=0,factor=2,gap=-1,limit=55,phase=0,distance=409600,want=1,start=None,end=None,position=None,hit=None,wp=None,points=None):
        cases.append([mode,behavior,radius,clearance,stored,waypoint,count,collision,pattern,factor,gap,limit,phase,distance,want,
                      *(position or [0,0,0]),*(start or [0,0,0]),*(end or [4096000,0,8192000]),*(hit or [0,0,0]),
                      *(wp or [0,0,0]),*(points or [0,0,0]*6)])
    for pattern in range(8):
        for want in [0,1]:
            for factor in [0,1,2,4,5,-1]:
                for start,end in [([0,0,0],[4096000,0,8192000]),([4096000,0,0],[0,0,8192000]),([0,0,0],[4096000,0,0])]:
                    add(pattern=pattern,want=want,factor=factor,start=start,end=end,gap=2)
    for behavior in [0,0x20000,0x2000,0x100000,0x2020000]:
        for end in [[0,0,0],[4095,100,4095],[0,409600,0],[1023999,0,0],[1024000,0,0],[1024001,0,0]]:
            for pattern in [0,1,2,4,7]:add(behavior=behavior,start=[0,0,0],end=end,pattern=pattern,gap=-1)
    for start,end in [([2147480000,0,2147480000],[-2147480000,0,-2147480000]),
                      ([-2147483648,0,0],[2147483647,0,0]),([0,2147483647,0],[4096000,-2147483648,0])]:
        for behavior in [0,0x20000]:
            for pattern in range(8):add(behavior=behavior,start=start,end=end,pattern=pattern,gap=0)
    for phase in range(128):add(mode=2,phase=phase,distance=rng.choice([-2147483648,-409600,0,409600,2147483647]),end=[rng.randint(-2147483648,2147483647) for _ in range(3)])
    for _ in range(350):
        mode=rng.choice([0,1]);position=[rng.randint(-100000000,100000000) for _ in range(3)]
        start=[rng.randint(-100000000,100000000) for _ in range(3)];end=[rng.randint(-100000000,100000000) for _ in range(3)]
        add(mode=mode,behavior=rng.choice([0,0x2000,0x100000,0x20000,0x2000000,0x2020000,0x102000]),
            radius=rng.choice([2,96,150,65535]),clearance=rng.choice([-32768,0,100,32767]),stored=rng.randint(-10000000,10000000),
            waypoint=rng.choice([-1,0,1,12]),count=rng.randrange(6),collision=rng.randint(-10,10),pattern=rng.getrandbits(20),
            factor=rng.choice([-1,0,1,2,4,5]),gap=rng.choice([-1,0,1,3]),limit=rng.choice([-1,0,55,10000]),want=rng.randrange(2),
            position=position,start=start,end=end,hit=[rng.randint(-100000000,100000000) for _ in range(3)],wp=start,
            points=[rng.randint(-100000000,100000000) for _ in range(18)])
    with tempfile.TemporaryDirectory(prefix='openspidey-path-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode())
    output=[dict(input=c,expected=json.loads(e)) for c,e in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-path.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source path checks/insertions/offsets with full capsule and ground callback traces.')


if __name__=='__main__':main()
