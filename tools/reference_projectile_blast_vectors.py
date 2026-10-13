"""Compile original CBullet blast helpers; exclude original undefined zero-distance divisions."""
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    bullet=(root/"src/game/objects/bullet.cpp").read_text();vectors=(root/"src/core/fixed_vector.cpp").read_text()
    bridge=(root/"src/compatibility/fixed_vector_bridge.cpp").read_text();gte=(root/"src/engine/rendering/ps2funcs.cpp").read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <cmath>
using namespace spidey::core;using VECTOR=Vector;
VECTOR G_OP12_RESULT{},G_GENERAL_LONG_VECTOR{};
struct SHitInfo{u8 field_0=0,field_1=0;i32 field_4=0;u16 field_8=0;CVector field_C;i16 field_18=0;u16 field_1A=0;};
struct CItem{int id=0;CVector mPos;CItem*mNextItem=nullptr;};
struct CBody:CItem{void Hit(SHitInfo*);};
enum HitId{ALWAYS_TWENTY_NINE=29};
struct CBullet{CVector mPos;u16 field_104=0;void GiveScaledDamageToObjects(CBody*,i32,i32,i32,HitId);void GiveScaledDamageToEnviro(i32);};
std::vector<long long> events;int mutation=0;CBody bodies[4];CBullet*currentBullet=nullptr;CItem*EnviroList=nullptr;
void mutate(CItem*item){
 if(item->id!=0)return;
 if(mutation==1&&item->mNextItem)item->mNextItem=item->mNextItem->mNextItem;
 else if(mutation==2){bodies[3].mNextItem=item->mNextItem;item->mNextItem=&bodies[3];}
 else if(mutation==3&&item->mNextItem)item->mNextItem->mPos={currentBullet->mPos.vx+8192,currentBullet->mPos.vy,currentBullet->mPos.vz};
}
void CBody::Hit(SHitInfo*hit){events.insert(events.end(),{1,id,hit->field_0,hit->field_4,hit->field_8,hit->field_C.vx,hit->field_C.vy,hit->field_C.vz,hit->field_18,hit->field_1A});mutate(this);}
void Exp_HitEnvItem(CItem*item,u32*face,i32 damage){events.insert(events.end(),{2,item->id,damage,face!=nullptr});mutate(item);}
'''
    for signature in ["CVector operator-(const CVector& lhs", "CVector operator/(const CVector& lhs"]:code+=controller_method(vectors,signature)
    for signature in ["void gte_ldlvl(","void gte_sqr0(","void gte_stlvnl(","i32 M3dMaths_SquareRoot0("]:code+=controller_method(gte,signature)
    code+=controller_method(bridge,"int CVector::Length(void)")
    code+=controller_method(bullet,"void CBullet::GiveScaledDamageToObjects(")
    code+=controller_method(bullet,"void CBullet::GiveScaledDamageToEnviro(")
    code+=r'''
int main(){long long v[22];while(std::cin>>v[0]){
 for(int i=1;i<22;i++)std::cin>>v[i];CBullet bullet;bullet.mPos={int(v[1]),int(v[2]),int(v[3])};bullet.field_104=v[4];currentBullet=&bullet;mutation=v[9];
 for(int i=0;i<4;i++){bodies[i].id=i;bodies[i].mPos={int(v[10+3*i]),int(v[11+3*i]),int(v[12+3*i])};bodies[i].mNextItem=i<2?&bodies[i+1]:nullptr;}
 EnviroList=bodies;events.clear();if(v[0]==0)bullet.GiveScaledDamageToObjects(bodies,v[5],v[6],v[7],HitId(v[8]));else bullet.GiveScaledDamageToEnviro(v[5]);
 std::cout<<"[";for(size_t i=0;i<events.size();i++){if(i)std::cout<<",";std::cout<<events[i];}std::cout<<"]\n";
}}
'''
    def signed(v):return (v+(1<<31))%(1<<32)-(1<<31)
    def length(position,target):
        delta=[signed(a-b)>>12 for a,b in zip(target,position)]
        square=signed(sum(a*a for a in delta))
        return 32768 if square<=-32768 else 0 if square<0 else math.isqrt(square)
    cases=[]
    for op in [0,1]:
        for radius in [1,2,100,512,32768,65535,-1]:
            for distance in [max(1,radius-1),max(1,radius),max(1,radius+1)]:
                for fraction in [-1,0,1,4095]:
                    for mutation in range(4):
                        targets=[[distance*4096+fraction,0,0],[8192,4096,0],[-4096,0,4096],[4096,0,0]]
                        if op==0 and radius>=0 and any(length([0,0,0],target)==0 for target in targets):continue
                        cases.append([op,0,0,0,65535,radius,100,10,29,mutation,*[v for t in targets for v in t]])
    rng=random.Random(0x43B1)
    for _ in range(512):
        op=rng.randrange(2)
        position=[rng.choice([-2147483648,-1000000,0,1000000,2147483647]) for _ in range(3)]
        targets=[[rng.choice([-2147483648,-2000000,-4096,0,4096,2000000,2147483647]) for _ in range(3)] for _ in range(4)]
        radius=rng.choice([1,100,512,32768,65535,2147483647,-1,-2147483648])
        if op==0 and radius>=0 and any(length(position,target)==0 for target in targets):continue
        cases.append([op,*position,rng.choice([0,1,10,32768,65535]),radius,rng.choice([0,100,65535,-100,2147483647]),
                      rng.choice([0,1,10,65536,-1,2147483647]),rng.choice([0,29,0x8000000]),rng.randrange(4),*[v for t in targets for v in t]])
    # Environment helper has no division: preserve zero/negative radii and signed
    # approximate-distance overflow cases rather than applying object safeguards.
    for radius in [0,-1,-2147483648]:cases.append([1,0,0,0,123,radius,100,10,29,0,-2147483648,0,-2147483648,0,0,0,4096,0,0,8192,0,0])
    with tempfile.TemporaryDirectory(prefix="projectile-blast-reference-") as temporary:
        directory=Path(temporary);(directory/"reference.cpp").write_text(code)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv","-I",str(root/"src"),str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result=subprocess.run([str(directory/"reference")],input="\n".join(" ".join(map(str,row)) for row in cases),capture_output=True,text=True,check=True)
    rows=[{"input":inputs,"events":json.loads(output)} for inputs,output in zip(cases,result.stdout.splitlines(),strict=True)]
    destination=root/"modern/OpenSpidey.Tests/Fixtures/projectile-blast.json"
    destination.write_text(json.dumps(rows,separators=(",",":"))+"\n")
    print(f"Compiled original valid blast traversal: {len(rows)} comparisons written to {destination}")


if __name__=="__main__":main()
