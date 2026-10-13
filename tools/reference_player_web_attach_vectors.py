"""Compile the original updateWebAttachWindup controller slice with typed effect observers."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root=Path(__file__).resolve().parent.parent
    source=(root/'src/game/player/player_web_actions.cpp').read_text()
    code=r'''
#include <iostream>
#include <vector>
using u16=unsigned short;using i16=short;
struct CVector{int vx=0,vy=0,vz=0;};struct CSVector{short vx=0,vy=0,vz=0;};
CVector ZeroVector;CSVector gTrajectoryVector{7,-8,9};std::vector<int>events;int jump,fireResult;
void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
struct CPlayer{u16 mAnim;int mFrame,mAnimFinished;short field_EA6=77;unsigned char field_552,field_8ED;CVector mInteractionTargetPosition{11,-12,13};
void SwitchToStandMode(){emit({1});}int CheckJump(){emit({2});return jump;}
int FireWeb(bool held,int cost,CVector*p,bool hit,CSVector*n){emit({3,held,cost,p->vx,p->vy,p->vz,hit,n->vx,n->vy,n->vz});return fireResult;}
void CreateWebDrips(bool a,bool b){emit({4,a,b});}void PlaySingleAnim(int anim,int first,int last){emit({5,anim,first,last});}
};
struct PlayerBehaviorController{CPlayer&player_;void updateWebAttachWindup();};
'''+controller_method(source,'void PlayerBehaviorController::updateWebAttachWindup(')+r'''
int main(){int animation,frame,finished,started,aim;while(std::cin>>animation>>frame>>finished>>started>>aim>>jump>>fireResult){CPlayer p;p.mAnim=animation;p.mFrame=frame;p.mAnimFinished=finished;p.field_552=started;p.field_8ED=aim;events.clear();PlayerBehaviorController c{p};c.updateWebAttachWindup();std::cout<<'['<<p.field_EA6<<','<<unsigned(p.field_552)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[]
    for anim in [0,0x8B,0xFF,0x109,0x100,0x10A]:
        for frame in [2,3,4,5,6,11,12]:
            for finished in [0,1]:
                for started in [0,1,255]:
                    for aim in [0,1]:
                        for jump in [0,1]:
                            cases.append([anim,frame,finished,started,aim,jump,(frame+started)&15])
    with tempfile.TemporaryDirectory(prefix='openspidey-web-attach-') as d:
        cpp=Path(d)/'reference.cpp';binary=Path(d)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary)],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-web-attach.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} original-source web attach release/recovery cases.')

if __name__=='__main__':main()
