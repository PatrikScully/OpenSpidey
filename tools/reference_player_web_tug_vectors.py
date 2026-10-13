"""Compile the original action0x20000 driver with explicit snap/path/action observers."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/player/player_web_actions.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
using u16=unsigned short;
std::vector<int> events;int jump,pathPresent;
void emit(std::initializer_list<int> value){events.insert(events.end(),value);}
struct CWeb{} web;
struct CPlayer{short field_EA6=77;int field_E20,field_544,mFrame,mAnimFinished;u16 mAnim;CWeb*field_E6C;
CVector mForwardDirection{234,-345,456},mUpDirection{-567,678,-789},mRightDirection{891,-912,123};
int CheckJump(){emit({1});return jump;}
int*CalculateTugWebPathPoints(){emit({2});return pathPresent?reinterpret_cast<int*>(1):nullptr;}
void OrientToNormal(bool use,CVector*target){emit({4,use,target->vx,target->vy,target->vz});}
void PlaySingleAnim(int animation,int first,int last){emit({5,animation,first,last});}
void SwitchToStandMode(){emit({6});}
};
void gCWeb_SwitchToSnap(CWeb*,CVector direction,CVector*path){emit({3,direction.vx,direction.vy,direction.vz,path?1:0});}
struct PlayerBehaviorController{CPlayer&player_;void updateWebTugAttack();};
''' + controller_method(source, 'void PlayerBehaviorController::updateWebTugAttack(') + r'''
int main(){int animation,frame,finished,released,current,side;while(std::cin>>animation>>frame>>finished>>released>>current>>side>>jump>>pathPresent){
CPlayer p;p.mAnim=animation;p.mFrame=frame;p.mAnimFinished=finished;p.field_E20=released;p.field_E6C=current?&web:nullptr;p.field_544=side;
events.clear();PlayerBehaviorController controller{p};controller.updateWebTugAttack();std::cout<<'['<<p.field_EA6<<','<<p.field_E20<<','<<(p.field_E6C?1:0)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = [[animation, frame, finished, released, current, side, jump, path]
        for animation in [0, 0x94, 0xFE, 0x108, 0x101, 0x102, 0x103, 0x10B, 0x10C, 0x10D]
        for frame in [8, 9, 13, 14] for finished in [0, 1] for released in [0, 1, 255]
        for current in [0, 1] for side in [0, 1, 2, 255] for jump in [0, 1] for path in [0, 1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-player-web-tug-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root / 'src'), str(cpp),
            str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary)], input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-web-tug.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} original action0x20000 release/latch/recovery cases.')


if __name__ == '__main__': main()
