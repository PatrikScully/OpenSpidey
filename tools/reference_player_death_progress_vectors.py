"""Compile the recovered updateDead body, preserving callback ordering and signed clock wrapping."""
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/player/player_locomotion_actions.cpp').read_text()
    code = r'''
#include <cstdint>
#include <iostream>
#include <vector>
using i32=int32_t;
struct CPlayer { int field_EA6,mAnimFinished,mElapsedTicks; };
struct PlayerBehaviorController {CPlayer&player_;void updateDead();};
std::vector<int>events;
struct SourceStore {int value,id;SourceStore&operator=(int next){value=next;events.insert(events.end(),{id,next});return *this;}};
int clockValue=0;int*gSpideyDeathTimer=&clockValue;SourceStore gInitRelatedTwo{1,1},gLevelStatus{0,2};
void Flash_Reset(){events.push_back(3);}
'''
    code += controller_method(source, 'void PlayerBehaviorController::updateDead(')
    code += r'''
int main(){CPlayer p;while(std::cin>>clockValue>>p.mAnimFinished>>p.mElapsedTicks>>p.field_EA6){
 gInitRelatedTwo=1;gLevelStatus=0;events.clear();PlayerBehaviorController{p}.updateDead();
 std::cout<<'['<<clockValue<<','<<p.field_EA6<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng = random.Random(0xDEAD120)
    cases = [[0,0,2,9],[118,1,2,9],[120,1,2,9],[120,0,2,9],[2147483647,1,2,9]]
    for _ in range(200):
        cases.append([rng.choice([-2147483648,-1,0,118,119,120,122,2147483647]),rng.randrange(2),
                      rng.choice([-1,0,1,2,10,2147483647]),rng.randrange(-32768,32768)])
    with tempfile.TemporaryDirectory(prefix='openspidey-death-clock-') as directory:
        path = Path(directory)
        (path/'reference.cpp').write_text(code)
        subprocess.run(['c++','-std=c++17','-O0','-fwrapv',str(path/'reference.cpp'),'-o',str(path/'reference')],check=True)
        outputs = subprocess.check_output([str(path/'reference')],input=''.join(' '.join(map(str,c))+'\n' for c in cases).encode()).decode().splitlines()
    target = root/'modern/OpenSpidey.Tests/Fixtures/player-death-progress.json'
    target.write_text(json.dumps([dict(input=c,expected=json.loads(o)) for c,o in zip(cases,outputs,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} source death-clock cases.')


if __name__ == '__main__':
    main()
