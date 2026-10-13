"""Compile original line-particle motion with a deterministic source clock."""
import json
from pathlib import Path
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root/'src/engine/world/bit2.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
int G_TIMER_RELATED;
struct CGLineParticle {
 CVector mStart,mEnd,mVel,mAcc;
 u16 mLifetime;
 int field_5C;
 bool dead=false;
 void Die(){dead=true;}
 void Move();
};
'''
    code += controller_method(source, 'void CGLineParticle::Move(void)') + r'''
int main(){int x,y,z,vx,vy,vz,acc,life,clock,increment;
while(std::cin>>x>>y>>z>>vx>>vy>>vz>>acc>>life>>clock>>increment){
 CGLineParticle p;p.mStart={x,y,z};p.mEnd=p.mStart;p.mVel={vx,vy,vz};p.mAcc={0,acc,0};p.mLifetime=life;p.field_5C=clock;
 G_TIMER_RELATED=clock;std::cout<<'[';
 for(int update=0;update<8;update++){
  G_TIMER_RELATED+=increment;if(!p.dead)p.Move();if(update)std::cout<<',';
  std::cout<<'['<<p.mStart.vx<<','<<p.mStart.vy<<','<<p.mStart.vz<<','
   <<p.mEnd.vx<<','<<p.mEnd.vy<<','<<p.mEnd.vz<<','<<p.mVel.vx<<','<<p.mVel.vy<<','<<p.mVel.vz<<','
   <<p.mLifetime<<','<<static_cast<unsigned>(p.field_5C)<<','<<(!p.dead)<<']';
 }std::cout<<"]\n";
}}
'''
    cases = [[x, y, z, vx, vy, vz, acceleration, life, clock, step]
             for x, y, z, vx, vy, vz in [(100, 200, 300, -6, 0, 8),
                 (2147483000, -2147483000, 123, 2000000000, -2000000000, -17)]
             for acceleration in [0, 4096, -4096, 2147483647]
             for life in [0, 1, 2, 20, 65535]
             for clock in [0, -3, 2147483646]
             for step in [0, 1, 2, 3, -1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-line-particle-') as directory:
        cpp = Path(directory)/'reference.cpp'
        binary = Path(directory)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root/'src'), str(cpp), '-o', str(binary)], check=True)
        results = subprocess.check_output([str(binary)], input='\n'.join(' '.join(map(str, case)) for case in cases).encode())
    fixtures = [dict(input=case, expected=json.loads(result))
                for case, result in zip(cases, results.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/line-particle.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Generated {len(cases)} line-particle sequences / {len(cases)*8} updates.')


if __name__ == '__main__':
    main()
