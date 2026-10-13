"""Compile original hostage readiness and action path branches against explicit world boundaries."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/"src/game/actors/thug.cpp").read_text()
    vectors=(root/"src/core/fixed_vector.cpp").read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include <iostream>
#include <vector>
#include <algorithm>
using namespace spidey::core;
struct CItem{CVector mPos;};struct CThug;using CBaddy=CThug;
struct SHandle{void*pWhatever=nullptr;};struct CMessage{SHandle mHandle;int field_10=0;CMessage(CThug*,CBaddy*,int,void*);};
std::vector<int> events;bool los=false;int pathCodes[2],addResults[3],pathIndex=0,addIndex=0,hitMode=0,procedureCount=0,procedureCode=0;CVector hit;
void*Mem_RecoverPointer(SHandle*p){return p->pWhatever;}
bool Utils_LineOfSight(CVector*from,CVector*to,int flags,void*){events.insert(events.end(),{2,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,flags});return los;}
struct Player:CItem{int field_57C=0;};Player*G_MECHLIST_PLAYER=nullptr;
struct CThug:CItem{
 SHandle mHandle;int field_330=0,dumbAssPad=0;unsigned field_2F0=0,field_2A8=0;struct{int bothFlags=0;}field_31C;
 void GetReadyToShootHostage(CMessage*);void RunToWhereTheActionIs(CVector*);int TryAddingCollidePointToPath(CVector*);
 void Neutralize(){events.push_back(5);}
 int AddPointToPath(CVector*p,int limit){events.insert(events.end(),{6,p->vx,p->vy,p->vz,limit});return addResults[std::min(addIndex++,2)];}
 int PathCheck(CVector*from,CVector*to,CVector*out,int clearance){
  CVector initial;if(out)initial=*out;
  events.insert(events.end(),{7,from->vx,from->vy,from->vz,to->vx,to->vy,to->vz,out!=nullptr,clearance,initial.vx,initial.vy,initial.vz});
  int result=pathCodes[std::min(pathIndex++,1)];if(out&&hitMode)*out=hit;return result;
 }
};
CMessage::CMessage(CThug*,CBaddy*,int code,void*){events.insert(events.end(),{4,code});}
struct CAIProc_StateSwitchSendMessage{CAIProc_StateSwitchSendMessage(CThug*,int code){procedureCount++;procedureCode=code;}};
'''
    for signature in ["CVector operator-(const CVector& lhs", "CVector* CVector::operator+=(", "CVector* CVector::operator*=(", "CVector* CVector::operator>>=("]:
        code+=controller_method(vectors,signature)
    for signature in ["void CThug::GetReadyToShootHostage(","void CThug::RunToWhereTheActionIs(","i32 CThug::TryAddingCollidePointToPath("]:
        code+=controller_method(source,signature)
    code+=r'''
int main(){long long v[28];while(std::cin>>v[0]){
 for(int i=1;i<28;i++)std::cin>>v[i];CThug actor;CItem sender;CMessage message(nullptr,nullptr,0,nullptr);Player player;G_MECHLIST_PLAYER=&player;
 actor.field_31C.bothFlags=v[1];actor.dumbAssPad=v[2];actor.field_330=v[3];actor.field_2A8=v[4];actor.field_2F0=v[5];message.field_10=v[6];
 sender.mPos={int(v[13]),int(v[14]),int(v[15])};message.mHandle={v[7]?&sender:nullptr};los=v[8];player.field_57C=v[9];
 actor.mPos={int(v[10]),int(v[11]),int(v[12])};player.mPos={int(v[16]),int(v[17]),int(v[18])};hit={int(v[19]),int(v[20]),int(v[21])};
 pathCodes[0]=v[22];pathCodes[1]=v[23];hitMode=v[24];addResults[0]=v[25];addResults[1]=v[26];addResults[2]=v[27];pathIndex=addIndex=procedureCount=procedureCode=0;events.clear();
 int result=-1;if(v[0]==0)actor.GetReadyToShootHostage(&message);else if(v[0]==1)actor.RunToWhereTheActionIs(&sender.mPos);else result=actor.TryAddingCollidePointToPath(&sender.mPos);
 std::cout<<"{\"state\":["<<actor.field_31C.bothFlags<<","<<actor.dumbAssPad<<","<<actor.field_330<<","<<actor.field_2A8<<","<<actor.field_2F0<<","<<message.field_10<<","<<(actor.mHandle.pWhatever!=nullptr)<<","<<procedureCount<<","<<procedureCode<<","<<result<<"],\"events\":[";
 for(size_t i=0;i<events.size();i++){if(i)std::cout<<",";std::cout<<events[i];}std::cout<<"]}\n";
}}
'''
    def case(**changes):
        values=dict(op=1,state=1,substate=3,alert=120,behavior=0xffffffff,waypoint=0x10,message_flags=2,sender=1,los=1,unavailable=0,
                    x=123456,y=-654321,z=987654,tx=4096000,ty=-100000,tz=-8192000,px=3000000,py=-200000,pz=1000000,
                    hx=2000000,hy=-300000,hz=-4000000,code0=0,code1=0,hit_mode=0,add0=1,add1=1,add2=1)
        values.update(changes);return list(values.values())
    cases=[]
    for sender in [0,1]:
        for los_value in [0,1]:
            for flags in [0,1,2,0x7fffffff]:cases.append(case(op=0,sender=sender,los=los_value,message_flags=flags))
    for unavailable in [0,1]:
        for code0 in [0,1,2,4]:
            for code1 in [0,1,2,4]:
                for hit_mode in [0,1]:
                    for add0 in [0,1]:
                        for add1 in [0,1]:
                            for add2 in [0,1]:cases.append(case(unavailable=unavailable,code0=code0,code1=code1,hit_mode=hit_mode,add0=add0,add1=add1,add2=add2))
    # Strict metric boundaries, negative truncation, non-axis vectors and int32 wrap.
    for distance in [99,100,101,3499,3500,3501]:
        for sign in [-1,1]:
            for fraction in [-1,0,1,4095]:
                for op in [1,2]:cases.append(case(op=op,x=0,y=0,z=0,tx=sign*distance*4096+fraction,ty=0,tz=0,unavailable=1,code0=2,hit_mode=0))
    rng=random.Random(0x4DA560)
    for _ in range(160):
        def coordinate():return rng.choice([-2147483648,-2147483000,-409601,-409600,-1,0,1,409599,409600,2147483000,2147483647])
        cases.append(case(op=rng.choice([1,2]),x=coordinate(),y=coordinate(),z=coordinate(),tx=coordinate(),ty=coordinate(),tz=coordinate(),
                          unavailable=rng.choice([0,1]),code0=rng.choice([0,2,4]),code1=rng.choice([0,2,4]),hit_mode=rng.choice([0,1])))
    with tempfile.TemporaryDirectory(prefix="thug-message-reactions-") as temporary:
        directory=Path(temporary);(directory/"reference.cpp").write_text(code)
        subprocess.run(["g++","-std=c++17","-O0","-fwrapv","-I",str(root/"src"),str(directory/"reference.cpp"),"-o",str(directory/"reference")],check=True)
        result=subprocess.run([str(directory/"reference")],input="\n".join(" ".join(map(str,row)) for row in cases),capture_output=True,text=True,check=True)
    rows=[{"input":inputs,**json.loads(output)} for inputs,output in zip(cases,result.stdout.splitlines(),strict=True)]
    destination=root/"modern/OpenSpidey.Tests/Fixtures/thug-message-reactions.json"
    destination.write_text(json.dumps(rows,separators=(",",":"))+"\n")
    print(f"Compiled original hostage/path reactions: {len(rows)} comparisons written to {destination}")


if __name__=="__main__":main()
