"""Compile recovered thug LaserCollision, Utils_CheckObjectCollision and original LineToSphere/GTE leaves."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    maths=(root/'src/engine/rendering/ps2funcs.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <cmath>
#include <vector>
using VECTOR=Vector;using SVECTOR=CSVector;
VECTOR G_OP12_RESULT,G_GENERAL_LONG_VECTOR,G_WTF_OP12,G_FTW_OP12,G_VERTEX_REGISTER[3];int G_SCALAR=0;
SVECTOR G_LINE_TO_SPHERE_DIR_MATRIX[3];
#define gGeneralLongVector G_GENERAL_LONG_VECTOR
struct CBaddy;using CBody=CBaddy;using CItem=CBaddy;using CThug=CBaddy;
int G_LINE_TO_SPHERE_BEST_T=0,G_LINE_TO_SPHERE_IGNORE_RADIUS=0,G_LINE_OF_SIGHT_CHECK=0;
CBaddy*G_LINE_TO_SPHERE_BEST_BODY=nullptr,*G_MECHLIST_PLAYER=nullptr,*G_ENVIRONMENTAL_OBJECT_LIST=nullptr;
struct SLineInfo{CVector StartCoords,EndCoords,Position;CItem*pItem=nullptr;};SLineInfo G_LINE_INFO;SLineInfo*currentLine=nullptr;
struct CBaddy{int id=0,mType=304;CVector mPos;u16 mRMinor=150;unsigned mCBodyFlags=0;CBaddy*mNextItem=nullptr;
 int LaserCollision(SLineInfo*,CVector*,int);};
CBaddy bodies[4];int queryIndex=0,hitCodes[2]={},environmentHit=0,randomValue=0;
std::vector<int>events;CVector hitPosition(12345,-67890,24680);
int Rnd(int n){events.insert(events.end(),{4,n});return randomValue%n;}
void SFX_PlayPos(unsigned sound,CVector*p,int attenuation){events.insert(events.end(),{5,int(sound),p->vx,p->vy,p->vz,attenuation});}
void M3dColij_InitLineInfo(SLineInfo*line){line->Position=CVector();line->pItem=nullptr;}
CItem*pointer(int code){if(code==0)return nullptr;if(code<=3)return &bodies[code-1];if(code==4)return reinterpret_cast<CItem*>(currentLine);return reinterpret_cast<CItem*>(12);}
void M3dZone_LineToItem(SLineInfo*l,int include){events.insert(events.end(),{2,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz,include,G_LINE_OF_SIGHT_CHECK});l->pItem=pointer(hitCodes[queryIndex++]);l->Position=hitPosition;}
void M3dColij_LineToItem(CItem*,SLineInfo*l){events.insert(events.end(),{3,l->StartCoords.vx,l->StartCoords.vy,l->StartCoords.vz,l->EndCoords.vx,l->EndCoords.vy,l->EndCoords.vz,G_LINE_OF_SIGHT_CHECK});l->pItem=pointer(environmentHit);l->Position=hitPosition;}
void gte_stlvnl(VECTOR*v){*v=G_GENERAL_LONG_VECTOR;}
'''
    for signature in ['i32 M3dMaths_SquareRoot0(', 'void gte_ldlvl(', 'void gte_ldopv1(', 'void gte_ldopv2(',
                      'void gte_op12(', 'void gte_sqr0(', 'void gte_stlvnl0(', 'void gte_ldlv0(', 'void gte_lddp(',
                      'void gsub_46D930(', 'void gsub_46DEB0(', 'void gte_gpf(']:
        code+=controller_method(maths,signature).replace('vertexRegister','G_VERTEX_REGISTER')+'\n'
    code+=controller_method((root/'src/compatibility/fixed_vector_bridge.cpp').read_text(),'int CVector::Length(')+'\n'
    code+=controller_method((root/'src/engine/world/collision.cpp').read_text(),'CBody * M3dColij_LineToSphere(')+'\n'
    code+=controller_method((root/'src/core/utils.cpp').read_text(),'CBody* Utils_CheckObjectCollision(')+'\n'
    code+=controller_method((root/'src/game/actors/thug.cpp').read_text(),'i32 CThug::LaserCollision(')+'\n'
    code+=r'''
int main(){int op;while(std::cin>>op){events.clear();queryIndex=0;G_LINE_TO_SPHERE_BEST_T=2147483647;G_LINE_TO_SPHERE_BEST_BODY=nullptr;SLineInfo line;currentLine=&line;CVector direction,output(7,11,13);int check,excluded,scale;
 std::cin>>bodies[0].mType>>check>>G_LINE_TO_SPHERE_IGNORE_RADIUS>>G_LINE_OF_SIGHT_CHECK>>hitCodes[0]>>hitCodes[1]>>environmentHit>>randomValue>>excluded>>scale
 >>line.StartCoords.vx>>line.StartCoords.vy>>line.StartCoords.vz>>line.EndCoords.vx>>line.EndCoords.vy>>line.EndCoords.vz
 >>direction.vx>>direction.vy>>direction.vz;
 for(int i=0;i<4;i++){bodies[i].id=i;std::cin>>bodies[i].mPos.vx>>bodies[i].mPos.vy>>bodies[i].mPos.vz>>bodies[i].mRMinor>>bodies[i].mCBodyFlags;bodies[i].mNextItem=i<3?&bodies[i+1]:nullptr;}
 G_MECHLIST_PLAYER=&bodies[1];G_ENVIRONMENTAL_OBJECT_LIST=&bodies[3];int result=0;
 if(op==0)result=bodies[0].LaserCollision(&line,&direction,check);
 else if(op==1){auto p=M3dColij_LineToSphere(&line.StartCoords,&line.EndCoords,&output,&bodies[1],excluded>=0?&bodies[excluded]:nullptr,scale);result=p?p->id:-1;}
 else result=Utils_CheckObjectCollision(&line.StartCoords,&line.EndCoords,&bodies[1],nullptr)!=nullptr;
 std::cout<<'['<<result<<','<<line.StartCoords.vx<<','<<line.StartCoords.vy<<','<<line.StartCoords.vz<<','<<line.EndCoords.vx<<','<<line.EndCoords.vy<<','<<line.EndCoords.vz
 <<','<<direction.vx<<','<<direction.vy<<','<<direction.vz<<','<<G_LINE_OF_SIGHT_CHECK<<','<<output.vx<<','<<output.vy<<','<<output.vz
 <<','<<(G_LINE_TO_SPHERE_BEST_BODY?G_LINE_TO_SPHERE_BEST_BODY->id:-1)<<','<<G_LINE_TO_SPHERE_BEST_T<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    rng=random.Random(0x4D5FA0);cases=[]
    for i in range(1000):
        op=rng.choice([0,0,1,1,2]);start=[rng.randint(-20000,20000)*4096 for _ in range(3)]
        delta=[rng.randint(-30000,30000)*4096 for _ in range(3)]
        end=[a+b for a,b in zip(start,delta)];direction=delta.copy()
        case=[op,rng.choice([304,312]),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.choice([0,0,0,1,2,3,4,5]),
              rng.randrange(6),rng.randrange(6),rng.randrange(2),rng.choice([-1,1,2,3]),rng.choice([0,2048,4096,6144,8192,65536]),*start,*end,*direction]
        for j in range(4):
            if j==0:pos=[a-10000 for a in start]
            else:
                fraction=rng.choice([-1,0,1,2,3,4,5]);pos=[a+(b>>2)*fraction+rng.choice([0,0,4096,409600]) for a,b in zip(start,delta)]
            case.extend([*pos,rng.choice([0,1,96,150,65535]),rng.choice([0,0,0x40])])
        cases.append(case)
    # Exact radius equality is rejected, and endpoint/negative-projection candidates retain source strictness.
    for offset in [-151,-150,-149,0,149,150,151]:
        for along in [-151,-1,0,1000,2000,2001,2151]:
            cases.append([1,304,1,0,0,0,0,0,0,-1,4096,0,0,0,2000<<12,0,0,2000<<12,0,0,
                          0,0,0,150,0,along<<12,offset<<12,0,150,0,1000<<12,0,0,0,0x40,1000<<12,0,0,0,0x40])
    # Actual manipulation AI uses scale6144 (1.5 radius), including strict radius equality.
    for offset in [-226,-225,-224,0,224,225,226]:
        for along in [-226,-1,0,2000,2001,2226]:
            cases.append([1,304,1,0,0,0,0,0,0,-1,6144,0,0,0,2000<<12,0,0,2000<<12,0,0,
                          0,0,0,150,0,along<<12,offset<<12,0,150,0,1000<<12,0,0,0,0x40,1000<<12,0,0,0,0x40])
    with tempfile.TemporaryDirectory(prefix='openspidey-laser-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    result=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-laser.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(f'Generated {len(result)} source laser/sphere/object-collision cases.')


if __name__=='__main__':main()
