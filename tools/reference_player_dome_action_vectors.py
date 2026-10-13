"""Compile actual player20000000 dome-release source controller with typed handle/action observers."""
import json
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent;source=(root/'src/game/player/player_interaction_actions.cpp').read_text()
    code=r'''
#include <iostream>
#include <vector>
using u8=unsigned char;using u16=unsigned short;using u32=unsigned;
struct CVector{int vx=12345,vy=-54321,vz=67890;};
struct SHandle{int pointer=0,validation=999;};
std::vector<int>events;void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
u32 G_TIMER_RELATED;int valid;
struct CDome{void Burst(){emit({4});}}dome;
void*Mem_RecoverPointer(SHandle*h){emit({3});if(!valid)h->pointer=0;return valid?&dome:nullptr;}
SHandle Mem_MakeHandle(void*){emit({5});return {0,0};}
void SFX_PlayPos(int sound,CVector*p,int pitch){emit({2,sound,p->vx,p->vy,p->vz,pitch});}
struct CPlayer{int mRMinor=100;u16 mAnim;u32 field_374;CVector mPos;SHandle field_AB8;u8 buttons[0x140]={};void*field_E0C=buttons;
void SwitchToStandMode(){emit({6});}void PlaySingleAnim(int anim,int first,int last){emit({1,anim,first,last});}};
struct PlayerBehaviorController{CPlayer&player_;int completedAnimation_;void updateWebDomeTrapped();};
'''+controller_method(source,'void PlayerBehaviorController::updateWebDomeTrapped(')+r'''
int main(){u32 clock,created;int animation,completed,kick,punch,jump,pointer;while(std::cin>>animation>>completed>>clock>>created>>kick>>punch>>jump>>pointer>>valid){CPlayer p;p.mAnim=animation;p.field_374=created;p.buttons[0x120]=kick;p.buttons[0x130]=punch;p.buttons[0x100]=jump;p.field_AB8.pointer=pointer;G_TIMER_RELATED=clock;events.clear();PlayerBehaviorController c{p,completed};c.updateWebDomeTrapped();std::cout<<'['<<p.mRMinor<<','<<p.field_AB8.pointer<<','<<p.field_AB8.validation<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[[anim,complete,clock,created,kick,punch,jump,pointer,valid]
        for anim in [0,0x11B,0x11C] for complete in [0,0x11B,0x11C]
        for clock,created in [(0,0),(150,0),(151,0),(100,101),(0,4294967295)]
        for kick,punch,jump in [(0,0,0),(1,0,0),(0,1,0),(0,0,1)] for pointer,valid in [(0,0),(1,0),(1,1)]]
    with tempfile.TemporaryDirectory(prefix='openspidey-dome-action-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-dome-action.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} actual-source player dome-release timer/buttons/handle/radius cases.')


if __name__=='__main__':main()
