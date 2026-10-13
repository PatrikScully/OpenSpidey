"""Compile original attack-slot assignment and clearing with deterministic participant callbacks."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root/'src/game/actors/thug.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
struct Player {CVector mPos;int field_57C;};
Player player;Player* G_MECHLIST_PLAYER=&player;
struct CThug;CThug* G_GLOBAL_THUG;unsigned G_ATTACK_FLAG_RELATED;
std::vector<int> events;
int my_abs(int x){return x<0?-x:x;}
struct CThug {
 int id,distance,field_29C;
 CVector mPos;
 unsigned char field_3BC,field_3BD,field_3BE;
 struct {int bothFlags;}field_31C;
 int DistanceToPlayer(int cache){events.insert(events.end(),{1,id,cache});return distance;}
 void DetermineFightState(){events.insert(events.end(),{2,id});}
 int SetAnimMode(int mode,int play){events.insert(events.end(),{3,id,mode,play});return 0;}
 void ClearAttackFlags();int SetAttackFlags();
};
'''
    code += controller_method(source, 'void CThug::ClearAttackFlags(void)')
    code += controller_method(source, 'i32 CThug::SetAttackFlags(void)') + r'''
int main(){int primary,mask,flag,owned,cursor,x,z,y,ground,otherGround,distance,otherDistance,otherState,unavailable;
while(std::cin>>primary>>mask>>flag>>owned>>cursor>>x>>z>>y>>ground>>otherGround>>distance>>otherDistance>>otherState>>unavailable){
 CThug actors[2];actors[0].id=0;actors[0].distance=distance;actors[0].mPos={x,0,z};actors[0].field_29C=ground;
 actors[0].field_3BC=flag;actors[0].field_3BD=owned;actors[0].field_3BE=cursor;actors[0].field_31C.bothFlags=4;
 actors[1].id=1;actors[1].distance=otherDistance;actors[1].mPos={0,0,0};actors[1].field_29C=otherGround;
 actors[1].field_3BC=1;actors[1].field_3BD=0;actors[1].field_3BE=0;actors[1].field_31C.bothFlags=otherState;
 player.mPos={0,y,0};player.field_57C=unavailable;
 G_GLOBAL_THUG=primary<0?nullptr:&actors[primary];G_ATTACK_FLAG_RELATED=mask;events.clear();
 int result=actors[0].SetAttackFlags();std::cout<<'['<<result<<','<<(G_GLOBAL_THUG?G_GLOBAL_THUG->id:-1)<<','<<G_ATTACK_FLAG_RELATED
 <<','<<static_cast<int>(actors[0].field_3BC)<<','<<static_cast<int>(actors[0].field_3BD)<<','<<static_cast<int>(actors[0].field_3BE)
 <<','<<static_cast<int>(actors[1].field_3BC)<<','<<static_cast<int>(actors[1].field_3BD)<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    cases = [[primary, mask, flags, owned, cursor, x, z, y, ground, other_ground, distance, other_distance, other_state, unavailable]
             for primary in [-1, 0, 1] for mask in [0, 1, 85, 255]
             for flags, owned in [(0, 0), (2, 4)] for cursor in [0, 3, 7, 255]
             for x, z in [(-4096, -4096), (4096, -4096), (-4096, 4096), (4096, 4096)]
             for y, ground, other_ground, distance, other_distance, other_state, unavailable in [
                 (0, 0, 0, 100, 600, 4, 0), (0, 0, 0, 450, 600, 5, 0),
                 (409600, 0, 0, 819200, 600, 1, 0), (425984, 0, 0, 100, 600, 4, 0),
                 (425983, 0, 0, 100, 600, 5, 0), (0, 0, 0, 100, 600, 4, 1)]]
    with tempfile.TemporaryDirectory(prefix='openspidey-thug-ownership-') as directory:
        cpp = Path(directory)/'reference.cpp'
        binary = Path(directory)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root/'src'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary)], input='\n'.join(' '.join(map(str, case)) for case in cases).encode())
    fixtures = [dict(input=case, expected=json.loads(result))
                for case, result in zip(cases, output.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/thug-ownership.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Generated {len(cases)} source attack ownership cases.')


if __name__ == '__main__':
    main()
