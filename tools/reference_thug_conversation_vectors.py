"""Compile original conversation selection, CanAck, GetClosest and cached player distance."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    thug=(root/'src/game/actors/thug.cpp').read_text();baddy=(root/'src/game/actors/baddy.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include "core/distance.h"
#include "engine/world/stateflags.h"
#include <iostream>
#include <vector>
#include <cstdlib>
struct CBaddy;using CThug=CBaddy;
CBaddy*G_BADDY_LIST=nullptr,*G_MECHLIST_PLAYER=nullptr;int G_ATTACK_RELATED=0,randomValue=0,visibleMask=0;
SStateFlags flags[17];SStateFlags*G_THUG_STATE_FLAGS=flags;std::vector<int>events;
void print_if_false(bool b,const char*){if(!b)std::abort();}
int Rnd(int n){events.insert(events.end(),{2,n});return randomValue%n;}
struct CBaddy{
 int id=0,mType=304,dumbAssPad=7,field_330=0,field_33C=0,field_398=123,field_39C=456,field_314=0,field_208=0,field_204=0;
 CVector mPos;SStateFlags field_31C{};CBaddy*mNextItem=nullptr,*mHandleTwo=nullptr;
 int DistanceToPlayer(int);u16 CheckStateFlags(SStateFlags*,int);CBaddy*GetClosest(int,int);int CanAck(int,int,int,CThug*);void StrikeUpConversation();
};
CBaddy* actors[4];
CBaddy*Mem_MakeHandle(CBaddy*p){events.insert(events.end(),{3,p->id});return p;}
int Utils_LineOfSight(CVector*start,CVector*end,CVector*,int mask){int id=-1;for(int i=0;i<4;i++)if(actors[i]->mPos.vx==start->vx && actors[i]->mPos.vy==start->vy && actors[i]->mPos.vz==start->vz){id=i;break;}
 events.insert(events.end(),{1,id,start->vx,start->vy,start->vz,end->vx,end->vy,end->vz,mask});return (visibleMask&(1<<id))!=0;}
'''
    for source,signature in [(baddy,'i32 CBaddy::DistanceToPlayer('),(baddy,'u16 CBaddy::CheckStateFlags('),
                             (baddy,'CBaddy* CBaddy::GetClosest('),(thug,'i32 CThug::CanAck('),(thug,'void CThug::StrikeUpConversation(')]:
        code+=controller_method(source,signature)+'\n'
    code+=r'''
int main(){int table[17][2]={{2,96},{23,64},{24,64},{1,96},{26,11},{27,11},{22,27},{18,1},{14,7},{15,7},{12,1},{17,7},{19,3},{20,3},{13,5},{25,96},{0,19}};
 for(int i=0;i<17;i++){flags[i].flags[0]=table[i][0];flags[i].flags[1]=table[i][1];}
 int op;while(std::cin>>op){CBaddy t[4]{},player;for(int i=0;i<4;i++){actors[i]=&t[i];t[i].id=i;t[i].mNextItem=i<3?&t[i+1]:nullptr;}
 G_BADDY_LIST=&t[0];G_MECHLIST_PLAYER=&player;events.clear();int queryType,delay,response,bank;
 std::cin>>G_ATTACK_RELATED>>t[0].field_208>>t[0].field_204>>randomValue>>visibleMask>>queryType>>delay>>response>>bank
 >>player.mPos.vx>>player.mPos.vy>>player.mPos.vz;
 for(int i=0;i<4;i++)std::cin>>t[i].mType>>t[i].field_31C.bothFlags>>t[i].field_330>>t[i].field_33C>>t[i].mPos.vx>>t[i].mPos.vy>>t[i].mPos.vz;
 int result=0;if(op==0)t[0].StrikeUpConversation();else if(op==1)result=t[1].CanAck(delay,response,bank,&t[0]);else{auto p=t[0].GetClosest(queryType,1);result=p?p->id:-1;}
 std::cout<<'['<<result<<','<<t[0].field_208<<','<<t[0].field_204<<",[";
 for(int i=0;i<4;i++){if(i)std::cout<<',';std::cout<<'['<<t[i].field_31C.bothFlags<<','<<t[i].dumbAssPad<<','<<t[i].field_330<<','<<t[i].field_33C
 <<','<<t[i].field_398<<','<<t[i].field_39C<<','<<(t[i].mHandleTwo?t[i].mHandleTwo->id:-1)<<','<<t[i].field_314<<']';}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
 }}
'''
    rng=random.Random(0x4D7020);cases=[]
    for i in range(800):
        op=rng.choice([0,0,0,1,2]);clock=rng.choice([0,1,20,2147483647]);cachedClock=rng.choice([0,clock-1,clock,clock+1 if clock<2147483647 else -2147483648])
        base=[op,clock,cachedClock,rng.choice([0,1792,1793,-1]),rng.randrange(7),rng.randrange(16),rng.choice([0,304,312,42]),
              rng.choice([0,160,-1,2147483647]),rng.choice([-9,4,14]),rng.choice([3,4]),
              rng.choice([1791,1792,1793,5000])<<12,0,0]
        for j in range(4):
            x=0 if j==0 else rng.choice([1,100,2047,2048,10655,10656,-100,100])<<12
            y=0 if j==0 else rng.choice([0,0,2047,2048,10000])<<12
            z=0 if j==0 else rng.choice([0,0,100])<<12
            base.extend([304 if j==0 else rng.choice([304,312,42]),rng.choice([0,1,2,5,13,19,21,25]),
                         rng.choice([0,0,1,-1]),rng.choice([0,0,1,-1]),x,y,z])
        cases.append(base)
    # Explicit all seven successful response choices, including choice6's different bank on each side.
    for choice in range(7):
        cases.append([0,20,0,0,choice,15,304,160,4,4,100<<12,0,0,
                      304,1,0,0,0,0,0,304,1,0,0,100<<12,0,0,312,1,0,0,50<<12,0,0,304,1,0,0,100<<12,0,0])
    for op in [0,2]:
        for extreme in [-2147483648,2147483647,-1,0]:
            cases.append([op,2147483647,-2147483648,-1,6,15,304,160,4,4,0,0,0,
                          304,1,0,0,0,0,0,304,1,0,0,extreme,extreme,extreme,
                          312,1,0,0,100<<12,0,0,304,1,0,0,200<<12,0,0])
    with tempfile.TemporaryDirectory(prefix='openspidey-conversation-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    result=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-conversation.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(f'Generated {len(result)} original conversation selection/admission cases.')


if __name__=='__main__':main()
