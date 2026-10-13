"""Compile the original CThug::AI initialization branch with bounded world callbacks."""
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/actors/thug.cpp').read_text()
    start = source.index('\t\tcase 0:\n\t\t\tswitch (this->dumbAssPad)', source.index('void CThug::AI(void)'))
    end = source.index('\t\tcase 1: this->Guard();', start)
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
void print_if_false(bool, const char*) {}
int G_ATTACK_RELATED, groundValue, groundCalls, heightCalls, cycleCalls, routeCalls, routeAvailable;
int Utils_GetGroundHeight(CVector*, int above, int below, int) { groundCalls++; return groundValue; }
struct CThug {
 int dumbAssPad=0,field_330=600,field_3B0=0,field_29C=0,field_2A0=0,field_1F0=0, waypoint=0;
 short field_21E=100;
 CVector mPos;
 struct { int bothFlags=0; } field_31C;
 union { unsigned value; unsigned char Bytes[4]; } field_298;
 void SetHeight(int initial,int clearance,int range) {heightCalls++;}
 void CycleAnim(int clip,int direction) {cycleCalls++;}
 int GetNextWaypoint() {routeCalls++;if(routeAvailable)waypoint^=1;return routeAvailable;}
 void Initialize() {switch(field_31C.bothFlags) {
'''
    code += source[start:end] + r'''
 default: break; }}
};
int main() {
 int substate,birth,counter,path,ground,available,clearance;
 while(std::cin>>substate>>birth>>counter>>path>>ground>>available>>clearance) {
  CThug actor;actor.dumbAssPad=substate;actor.field_3B0=birth;actor.field_1F0=path;
  actor.field_21E=clearance;actor.field_298.value=0;actor.mPos={100,789,300};
  groundValue=ground;routeAvailable=available;G_ATTACK_RELATED=counter;
  groundCalls=heightCalls=cycleCalls=routeCalls=0;
  std::cout<<'[';
  for(int update=0;update<6;update++) {
   actor.Initialize();if(update)std::cout<<',';
   std::cout<<'['<<actor.field_31C.bothFlags<<','<<actor.dumbAssPad<<','<<actor.field_330<<','
    <<actor.mPos.vx<<','<<actor.mPos.vy<<','<<actor.mPos.vz<<','<<actor.field_29C<<','<<actor.field_2A0<<','
    <<groundCalls<<','<<heightCalls<<','<<cycleCalls<<','<<routeCalls<<','<<actor.waypoint<<']';
   G_ATTACK_RELATED++;
  }std::cout<<"]\n";
 }
}
'''
    cases = [[substate, birth, counter, path, ground, available, clearance]
             for substate in [0, 1] for birth, counter in [(0, 0), (0, 11), (0, 15), (50, 49), (100, 1000)]
             for path in [0, 1] for ground in [-1, 409600, -409600, 2147483000]
             for available in [0, 1] for clearance in [100]]
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-initialization-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root/'src'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary)], input='\n'.join(' '.join(map(str, case)) for case in cases).encode())
    fixtures = [dict(input=case, expected=json.loads(result))
                for case, result in zip(cases, output.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-initialization.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Generated {len(cases)} initialization sequences / {len(cases)*6} updates.')


if __name__ == '__main__':
    main()
