"""Compile original small/big explosion helpers with explicit CFrag/GlowFlash constructor trace leaves."""
import json
from pathlib import Path
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/effects/exp.cpp').read_text()
    methods = [controller_method(source, signature) for signature in
               ['void Exp_SmallExplosion(', 'void Exp_BigExplosion(', 'void Exp_Frag(', 'void Exp_GlowFlash(']]
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
int LowMemory, countValue, afterRandom, afterFragment;
std::vector<int> events;
int Rnd(int range){events.insert(events.end(),{1,range});LowMemory=afterRandom;return countValue;}
struct CFrag {
 CFrag(CVector* p,u8 r,u8 g,u8 b,int animation,u16 scale,int semi,int spread,int gravity,int lifetime){
 events.insert(events.end(),{2,p->vx,p->vy,p->vz,r,g,b,animation,scale,semi,spread,gravity,lifetime});
 LowMemory=afterFragment;}
};
struct CGlowFlash {
 CGlowFlash(CVector* p,int points,u8 r,u8 g,u8 b,int fade,u8 sr,u8 sg,u8 sb,int sf,int width,int wd,int choose,int odd,int oddRange,int even,int evenRange,int decay,int life){
 events.insert(events.end(),{3,p->vx,p->vy,p->vz,points,r,g,b,fade,sr,sg,sb,sf,width,wd,choose,odd,oddRange,even,evenRange,decay,life});}
};
void Exp_Frag(CVector*,int,int,int,int);
void Exp_GlowFlash(CVector*,int,u8,u8,u8,int,int,int);
'''
    code += '\n'.join(methods) + r'''
int main(){int small;while(std::cin>>small>>LowMemory>>countValue>>afterRandom>>afterFragment){
 events.clear();CVector position(100,200,300);
 if(small)Exp_SmallExplosion(&position);else Exp_BigExplosion(&position);
 std::cout<<'[';for(unsigned index=0;index<events.size();index++){if(index)std::cout<<',';std::cout<<events[index];}std::cout<<"]\n";
}}
'''
    cases = [[small, low_memory, count, after_random, after_fragment]
             for small in [0, 1] for low_memory in [0, 1] for count in range(20)
             for after_random in [0, 1] for after_fragment in [0, 1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-crate-explosion-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-I', str(root / 'src'),
                        str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary)],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    target = root / 'modern/OpenSpidey.Tests/Fixtures/crate-explosion.json'
    target.write_text(json.dumps([dict(input=case, events=json.loads(result))
        for case, result in zip(cases, output, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} original Small/Big/Frag/Glow helper traces, all fragment counts and low-memory read stages.')


if __name__ == '__main__':
    main()
