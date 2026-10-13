"""Compile CBaddy first-link traversal against deterministic waypoint graph callbacks."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/actors/baddy.cpp').read_text()
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
u16 links[4][3]={{2,1,3},{1,2,0},{1,3,0},{0,0,0}};
u16 nodes[4][7]={{1,0,0,0,0,0,0},{1000,0,0,0,0,0,0},{12,0,0,0,0,0,0},{1002,0,0,0,0,0,0}};
u16*G_OFFSETLIST[4]={nodes[0],nodes[1],nodes[2],nodes[3]};
u16*Trig_GetLinksPointer(int id){return links[id];}
u16*Trig_GetPosition(CVector*,int id){return nodes[id]+1;}
struct CBaddy{int field_1F4;unsigned field_2F0;int field_2F4,parsed=0,last=-1;int GetNextWaypoint();
 void ParseScript(u16*p){parsed++;for(int i=0;i<4;i++)if(p==nodes[i]+4)last=i;field_2F0|=2;field_2F4=123;}};
'''
    code+=controller_method(source,'i32 CBaddy::GetNextWaypoint(void)')+r'''
int main(){int initial,flags,parameter;
while(std::cin>>initial>>flags>>parameter){CBaddy actor;actor.field_1F4=initial;actor.field_2F0=flags;actor.field_2F4=parameter;
 std::cout<<'[';for(int frame=0;frame<6;frame++){int result=actor.GetNextWaypoint();if(frame)std::cout<<',';
 std::cout<<'['<<result<<','<<actor.field_1F4<<','<<actor.field_2F0<<','<<actor.field_2F4<<','<<actor.parsed<<','<<actor.last<<']';}std::cout<<"]\n";}}
'''
    cases=[dict(initial=initial,flags=flags,parameter=parameter) for initial in [-1,0,1,2,3] for flags in [0,1,255] for parameter in [0,65535]]
    with tempfile.TemporaryDirectory(prefix='openspidey-waypoint-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-I',str(root/'src'),str(cpp),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(f"{c['initial']} {c['flags']} {c['parameter']}" for c in cases).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-waypoints.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} waypoint sequences / {len(cases)*6} advances.')


if __name__=='__main__':main()
