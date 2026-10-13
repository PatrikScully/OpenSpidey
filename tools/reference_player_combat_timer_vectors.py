"""Compile the recovered player damage timer update for invulnerability and electrification fixtures."""
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root/"src/game/player/player_status_updates.cpp").read_text()
    code = r'''
#include <cstdint>
#include <iostream>
#include <vector>
using u16=uint16_t;
struct CPlayer{int field_36C,field_534,field_50C,mElapsedTicks;};
struct PlayerFramePreparation{CPlayer&player_;void updateDamageTimers();};
int vibration=0;int*gSaveGameVibration=&vibration;std::vector<int>events;
void Pad_ActuatorOn(int controller,u16 time,int argument,int strength){events.insert(events.end(),{1,controller,time,argument,strength});}
void Effects_UnElectrify(CPlayer*){events.push_back(2);}
'''
    code += controller_method(source, "void PlayerFramePreparation::updateDamageTimers(")
    code += r'''
int main(){CPlayer p;while(std::cin>>p.field_36C>>p.field_534>>p.field_50C>>p.mElapsedTicks>>vibration){
 events.clear();PlayerFramePreparation{p}.updateDamageTimers();std::cout<<'['<<p.field_36C<<','<<p.field_534<<','<<p.field_50C<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    randomizer = random.Random(0x36C534)
    cases = [[30,240,120,2,1], [1,1,1,2,0], [0,0,0,2,1], [30,240,120,0,1]]
    for _ in range(300):
        cases.append([randomizer.choice([-2147483648,-1,0,1,30,120,240,65535,2147483647]) for _ in range(3)] +
                     [randomizer.choice([-1,0,1,2,100,2147483647]),randomizer.randrange(2)])
    requests="".join(" ".join(map(str,case))+"\n" for case in cases)
    with tempfile.TemporaryDirectory(prefix="openspidey-damage-timer-reference-") as directory:
        path=Path(directory);(path/"reference.cpp").write_text(code)
        subprocess.run(["c++","-std=c++17","-O0","-fwrapv",str(path/"reference.cpp"),"-o",str(path/"reference")],check=True)
        outputs=subprocess.check_output([str(path/"reference")],input=requests.encode()).decode().splitlines()
    result=[dict(input=case,expected=json.loads(output)) for case,output in zip(cases,outputs,strict=True)]
    destination=root/"modern/OpenSpidey.Tests/Fixtures/player-combat-timers.json"
    destination.write_text(json.dumps(result,separators=(",", ":"))+"\n")
    print(f"Generated {len(cases)} player damage timer vectors at {destination}")


if __name__ == "__main__":main()
