"""Compile actual4000 windup and8000 firing source branches with typed ownership and action observers."""
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/player/player_web_actions.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
using u8=unsigned char;using u16=unsigned short;using i16=short;
std::vector<int>events;void emit(std::initializer_list<int>v){events.insert(events.end(),v);}
int jump,supply,fireResult,dropFire,matches;unsigned G_TIMER_RELATED;
CVector ZeroVector;CSVector gTrajectoryVector{7,-8,9};
struct SHandle{int pointer=0,validation=0;};
struct CBody{u16 mType=304;};CBody aim,other;
struct CPlayer;
CPlayer*activePlayer;
struct CWeb{int field_F8=0;u16 field_102=0;SHandle field_134;
CWeb(){emit({7});}~CWeb(){emit({10});}void SwitchToBlob(){emit({9});}};
struct CDome{CDome(CPlayer*,u8 fire){emit({14,int(fire)});}};
SHandle Mem_MakeHandle(void*){emit({15});return {1,1};}
void*Mem_RecoverPointer(SHandle*);
void SFX_Stop(unsigned sound){emit({11,int(sound)});}
void SFX_PlayPos(int sound,CVector*p,int pitch){emit({5,sound,p->vx,p->vy,p->vz,pitch});}
namespace spidey{namespace player{
enum class PlayerAction:unsigned{WebFiring=0x8000,WebAttachWindup=0x10000,KnockdownRecovery=0x800000,WebDomeTrapped=0x20000000,ZipDive=0x02000000,WebTugAttack=0x20000};
int actionWord(PlayerAction value){return int(value);}
struct CPlayer{u16 mAnim;int mFrame,mAnimFinished,mElapsedTicks,mIsCrawling,field_DE0=0,field_374=0,field_E20;
short field_EA6=77,timer;u8 field_552,field_8ED,field_8F8,field_544,field_8E1,field_8E4,field_8EA,field_5E8,animationFlags;
signed char field_E2D,field_E2E;unsigned field_5B4;int field_5E4,mActionState;
CVector mPos{12345,-54321,67890},mInteractionTargetPosition{11,-12,13},field_DA0{65543,-65544,9};
CBody*field_DCC;int*field_E6C;SHandle field_AB8,field_DD8;u8 pad[0x140]={};void*field_E0C=pad;
int CheckJump(){emit({1});return jump;}void SwitchToStandMode(){emit({2});}
void PlaySingleAnim(int anim,int first,int last){emit({3,anim,first,last});}
void RunAnim(int anim,int first,int last){emit({6,anim,first,last});}
int DecreaseWebbing(int amount){emit({4,amount});return supply;}
int FireWeb(bool held,int cost,CVector*p,bool hit,CSVector*n){auto*w=reinterpret_cast<CWeb*>(field_E6C);emit({8,held,cost,p->vx,p->vy,p->vz,hit,n->vx,n->vy,n->vz,int(w->field_102),w->field_F8});if(dropFire)field_E6C=nullptr;return fireResult;}
CBody*SelectTargetBaddy(int range,int minimum,int maximum,int flags){emit({13,range,minimum,maximum,flags});return &other;}
};
struct PlayerBehaviorController{CPlayer&player_;void updateWebAttackWindup();void updateWebFiring();};
#define PLR_U8(p,o) ((p)->animationFlags)
#define PLR_I16(p,o) ((p)->timer)
'''
    # The source's forward declaration belongs in its own namespace.
    code = code.replace('struct CPlayer;\nCPlayer*activePlayer;', 'namespace spidey{namespace player{struct CPlayer;}}\nspidey::player::CPlayer*activePlayer;')
    code = code.replace('CDome(CPlayer*,', 'CDome(spidey::player::CPlayer*,')
    code += controller_method(source, 'void PlayerBehaviorController::updateWebAttackWindup(') + '\n'
    code += controller_method(source, 'void PlayerBehaviorController::updateWebFiring(') + r'''
}}
void*Mem_RecoverPointer(SHandle*){emit({12});return matches?activePlayer->field_DCC:&other;}
int main(){using namespace spidey::player;int mode,animation,frame,finished,latch,explicitAim,targetType,current,side,webMode,blocked,leanX,leanY,webButton,kick,punch,transition,crawl,sound,timer,elapsed,variant,released,recovery,flags;unsigned clock,last;
while(std::cin>>mode>>animation>>frame>>finished>>latch>>explicitAim>>targetType>>current>>side>>webMode>>blocked>>leanX>>leanY>>webButton>>kick>>punch>>transition>>crawl>>clock>>last>>sound>>timer>>elapsed>>fireResult>>dropFire>>matches>>jump>>supply>>variant>>released>>recovery>>flags){
CPlayer p;p.mAnim=animation;p.mFrame=frame;p.mAnimFinished=finished;p.field_552=latch;p.field_8ED=explicitAim;p.field_DCC=targetType?(aim.mType=targetType,&aim):nullptr;
events.clear();CWeb*initial=current?new CWeb():nullptr;events.clear();p.field_E6C=reinterpret_cast<int*>(initial);p.field_544=side;p.field_8F8=webMode;p.field_8E4=blocked;p.field_E2D=leanX;p.field_E2E=leanY;p.pad[0x110]=webButton;p.pad[0x120]=kick;p.pad[0x130]=punch;p.field_8EA=transition;p.mIsCrawling=crawl;G_TIMER_RELATED=clock;p.field_5B4=last;p.field_5E4=sound;p.timer=timer;p.mElapsedTicks=elapsed;p.field_5E8=variant;p.field_E20=released;p.field_8E1=recovery;p.animationFlags=flags;p.mActionState=mode?0x8000:0x4000;activePlayer=&p;
PlayerBehaviorController controller{p};if(mode)controller.updateWebFiring();else controller.updateWebAttackWindup();auto*w=reinterpret_cast<CWeb*>(p.field_E6C);
std::cout<<'['<<p.field_EA6<<','<<int(p.field_552)<<','<<int(p.field_8ED)<<','<<int(p.field_8F8)<<','<<int(p.field_544)<<','<<unsigned(p.mActionState)<<','<<p.field_E20<<','<<int(p.field_8E1)<<','<<int(p.animationFlags)<<','<<(p.field_DCC?int(p.field_DCC->mType):0)<<','<<(w?1:0)<<','<<p.timer<<','<<p.field_374<<','<<p.field_AB8.pointer<<','<<p.field_DE0<<','<<p.field_DD8.pointer<<','<<p.field_5E4<<','<<p.mFrame<<','<<(w?int(w->field_102):-1)<<','<<(w?w->field_F8:-1)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    rng = random.Random(0x4B44C9)
    def case(mode, animation, frame, finished, latch, explicit_aim, current):
        result = rng.choice([0, 1, 2, 4, 8] if mode else [0, 1, 2, 4])
        return [mode, animation, frame, finished, latch, explicit_aim, rng.choice([0, 304, 407]), current,
                rng.choice([0, 1, 2, 255]), rng.choice([1, 2]), rng.choice([0, 1, 2, 4, 8, 15]),
                rng.choice([-127, -1, 0, 1, 127]), rng.choice([-127, -1, 0, 1, 127]),
                rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.randrange(2),
                rng.choice([0, 30, 31, 100, 4294967295]), rng.choice([0, 50, 4294967290]),
                rng.choice([0, 123]), rng.choice([-32768, -1, 0, 40, 32767]), rng.choice([0, 2, 6, 32767, 65535]),
                result, int(mode and result == 8), rng.randrange(2), rng.randrange(2), rng.randrange(2),
                rng.choice([0, 1, 255]), rng.choice([0, 1, 255]), rng.choice([0, 1, 255]), rng.choice([0, 255])]
    cases = []
    for mode, animations in [(0, [0, 0xFA, 0x104, 0xFB, 0x105]), (1, [0, 0x94, 0xFC, 0x106, 0xFD, 0x107, 0x9A])]:
        for animation in animations:
            for frame in [-1, 4, 5, 6, 7, 8, 9, 14]:
                for finished in [0, 1]:
                    for latch in [0, 1]:
                        for explicit_aim in [0, 1]:
                            for current in [0, 1]:
                                cases.append(case(mode, animation, frame, finished, latch, explicit_aim, current))
    with tempfile.TemporaryDirectory(prefix='openspidey-player-web-attack-') as directory:
        cpp = Path(directory) / 'reference.cpp'; binary = Path(directory) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root / 'src'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary)], input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-web-attack.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} actual-source4000/8000 windup/firing branch cases.')


if __name__ == '__main__': main()
