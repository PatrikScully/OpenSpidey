"""Compile actual CPlayer::CreateWebDrips with observed source hook/RNG/line-particle calls."""
import json
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/player/spidey.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
using VECTOR=CVector;std::vector<int>events;int cursor;
void emit(std::initializer_list<int> values){events.insert(events.end(),values);}
int Rnd(int bound){int value=(cursor++*7+1)%bound;emit({1,bound,value});return value;}
struct CPlayer{CVector mForwardDirection;void CreateWebDrips(bool,bool);};
void M3dUtils_GetHookPosition(VECTOR*p,CPlayer*,int hook){emit({2,hook});*p=CVector(12345+hook*4097,-54321-hook*8193,67890+hook*12289);}
struct CGLineParticle{unsigned mCodeBGR0=0;CGLineParticle(CVector&p,CVector&v,unsigned short lifetime,int gravity){emit({3,p.vx,p.vy,p.vz,v.vx,v.vy,v.vz,lifetime,gravity});}
void SetRGB0(int r,int g,int b){emit({4,r,g,b});}void SetRGB1(int r,int g,int b){emit({5,r,g,b});}};
'''
    code += controller_method(source, 'void CPlayer::CreateWebDrips(') + r'''
int main(){int first,second;CPlayer player;while(std::cin>>first>>second>>player.mForwardDirection.vx>>player.mForwardDirection.vy>>player.mForwardDirection.vz>>cursor){events.clear();player.CreateWebDrips(first,second);std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";}}
'''
    cases = [[first, second, *direction, cursor]
             for first in [0, 1] for second in [0, 1]
             for direction in [(0, 0, 4096), (4096, 0, 0), (0, -4096, 0),
                               (1234, -2345, 3456), (-32768, 32767, -32768),
                               (2147483647, -2147483648, 0)]
             for cursor in [0, 1, 2]]
    with tempfile.TemporaryDirectory(prefix='openspidey-web-drips-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root / 'src'),
                        str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary)], input=('\n'.join(' '.join(map(str, case))
                                         for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-web-drips.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)],
        separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} extracted source web-drip hook/RNG/gravity/gradient cases.')


if __name__ == '__main__':
    main()
