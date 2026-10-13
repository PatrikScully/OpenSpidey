"""Verify actual CFireySpark constructor/Move with original PC instructions and observed source RNG."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/game/actors/simby.cpp').read_text()
    code = r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cstring>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct CBit{unsigned vtable=0,previous=0,next=0;short age=0;unsigned short lifetime=0;CVector mPos,mVel,acceleration;unsigned char friction[3]={1,1,1},dead=0;unsigned short depth=0;unsigned char protect=0,mType=0;void Die(){dead=1;}};
struct CPixel:CBit{unsigned tag=0;unsigned char r0=0,g0=0,b0=0,code=0;unsigned mWidthHeight=0;};
struct CFireySpark:CPixel{int field_48=0,field_4C=0;CFireySpark(CVector*,CVector*,int);void Move();};
static_assert(sizeof(CFireySpark)==0x50);std::vector<int>events;int cursor;
int Rnd(int bound){int value=(cursor++*7+1)%bound;events.insert(events.end(),{bound,value});return value;}
'''
    code += controller_method(source, 'CFireySpark::CFireySpark(') + '\n'
    code += controller_method(source, 'void CFireySpark::Move(') + r'''
void output(CFireySpark&s){std::cout<<'['<<s.mPos.vx<<','<<s.mPos.vy<<','<<s.mPos.vz<<','<<s.mVel.vx<<','<<s.mVel.vy<<','<<s.mVel.vz<<','<<s.field_4C<<','<<s.tag<<','<<unsigned(s.code)<<','<<s.mWidthHeight<<','<<s.field_48<<','<<unsigned(s.r0)<<','<<unsigned(s.g0)<<','<<unsigned(s.b0)<<','<<int(s.dead)<<']';}
std::string capture(CFireySpark&s){std::ostringstream text;auto*old=std::cout.rdbuf(text.rdbuf());output(s);std::cout.rdbuf(old);return text.str();}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void* __attribute__((thiscall)) baseConstructor(void*p){*static_cast<CPixel*>(p)=CPixel();return p;}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x40F080,reinterpret_cast<void*>(&baseConstructor));patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4A3CB7,6},{0x4A3CBE,7},{0x4A3D81,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
#endif
CVector position,velocity;int ground,seed;
while(std::cin>>position.vx>>position.vy>>position.vz>>velocity.vx>>velocity.vy>>velocity.vz>>ground>>seed){events.clear();cursor=seed;CFireySpark s(&position,&velocity,ground);auto randomEvents=events;
std::vector<std::string>states{capture(s)};for(int frame=1;frame<30;frame++){if(!s.dead)s.Move();states.push_back(capture(s));}
#ifdef VALIDATE_ORIGINAL
events.clear();cursor=seed;alignas(4)unsigned char raw[0x50]={};auto&actual=*reinterpret_cast<CFireySpark*>(raw);
reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,CVector*,int)>(0x4A3CB0)(raw,&position,&velocity,ground);
assert(events==randomEvents&&capture(actual)==states[0]);for(int frame=1;frame<30;frame++){if(!actual.dead)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4A3DC0)(raw);assert(capture(actual)==states[frame]);}
#endif
std::cout<<"[[";for(unsigned i=0;i<randomEvents.size();i++){if(i)std::cout<<',';std::cout<<randomEvents[i];}std::cout<<"],[";for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]]\n";}}
'''
    cases = [[12345, -54321, 67890, x, y, z, ground, seed]
             for x, y, z in [(0, 0, 0), (4096, -4096, -8192), (-4096, 40000, 8192)]
             for ground in [-54322, -54321, -54320, 150000] for seed in [0, 1, 2]]
    rng = random.Random(0x4A3DC0)
    for _ in range(28):
        cases.append([*[rng.randint(-2000000000, 2000000000) for _ in range(6)],
                      rng.randint(-2000000000, 2000000000), rng.randrange(10)])
    with tempfile.TemporaryDirectory(prefix='openspidey-fire-spark-') as directory:
        cpp = Path(directory) / 'reference.cpp'
        binary = Path(directory) / 'reference'
        cpp.write_text(code)
        flags = ['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', *flags,
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        command = [str(binary)] + ([str(args.original_exe.resolve())] if args.original_exe else [])
        lines = subprocess.check_output(command, input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/fire-sparks.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} fiery spark ctor/30frame cases; original PC verified={bool(args.original_exe)}.')


if __name__ == '__main__':
    main()
