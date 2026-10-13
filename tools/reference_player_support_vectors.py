"""Compile recovered platform riding and the support-dependent standing jump impulse."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    finalizer=(root/'src/game/player/player_frame_finalizer.cpp').read_text()
    player=(root/'src/game/player/spidey.cpp').read_text()
    jump=player[player.index('\t\tCBody *pGround = this->mSupportBody;'):player.index('\t\tu8 flags =',player.index('\t\tCBody *pGround = this->mSupportBody;'))]
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
struct CBody{CVector mVel;};
struct CPlayer{CVector mPos;CBody*mSupportBody;int field_E80=0;u16 airborne;void CaptureJump();};
#define PLR_U16(player,offset) ((player)->airborne)
struct SLineInfo{CVector MinCoords,MaxCoords,Position,Normal,StartCoords,EndCoords;CBody*pItem=nullptr;};
bool blocked,queried;CBody wall;CVector rayStart,rayEnd;
void M3dColij_InitLineInfo(SLineInfo*){}
void M3dZone_LineToItem(SLineInfo*line,int){queried=true;rayStart=line->StartCoords;rayEnd=line->EndCoords;line->pItem=blocked?&wall:nullptr;}
struct PlayerFrameFinalizer{CPlayer&player_;void rideSupportBody(SLineInfo&);};
'''
    code+=controller_method(finalizer,'void PlayerFrameFinalizer::rideSupportBody(SLineInfo& lineInfo)')+'\n'
    code+='void CPlayer::CaptureJump(){\n'+jump+'}\n'
    code+=r'''
int main(){CPlayer player;CBody platform;int airborne,supported,obstructed;
while(std::cin>>player.mPos.vx>>player.mPos.vy>>player.mPos.vz>>platform.mVel.vx>>platform.mVel.vy>>platform.mVel.vz>>airborne>>supported>>obstructed){
 player.airborne=airborne;player.mSupportBody=supported?&platform:nullptr;blocked=obstructed;queried=false;rayStart=rayEnd=CVector();
 SLineInfo line;PlayerFrameFinalizer{player}.rideSupportBody(line);player.CaptureJump();
 std::cout<<'['<<player.mPos.vx<<','<<player.mPos.vy<<','<<player.mPos.vz<<','<<queried
 <<','<<rayStart.vx<<','<<rayStart.vy<<','<<rayStart.vz<<','<<rayEnd.vx<<','<<rayEnd.vy<<','<<rayEnd.vz<<','<<player.field_E80<<"]\n";
}}
'''
    rng=random.Random(0x4b76c0)
    cases=[]
    for vertical in [0,1,-1,4096,-262143,-262144,-262145,-2147483648,2147483647]:
        for horizontal in [(0,0),(4096,-4096),(2147483647,-2147483648)]:
            for airborne in [False,True]:
                for supported in [False,True]:
                    for blocked in [False,True]:
                        cases.append(dict(position=[rng.randint(-2147483648,2147483647) for _ in range(3)],velocity=[horizontal[0],vertical,horizontal[1]],airborne=airborne,supported=supported,blocked=blocked))
    lines=[' '.join(map(str,[*c['position'],*c['velocity'],int(c['airborne']),int(c['supported']),int(c['blocked'])])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-support-reference-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-I',str(root/'src'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        results=subprocess.check_output([str(binary)],input='\n'.join(lines).encode()).decode().splitlines()
    for case,result in zip(cases,results,strict=True):case['expected']=json.loads(result)
    (root/'modern/OpenSpidey.Tests/Fixtures/player-support.json').write_text(json.dumps(cases,separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source support riding/jump vectors.')


if __name__=='__main__':main()
