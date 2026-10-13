"""Capture original Ob_AI traversal, with isolated timing and synthetic native actors."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
#include <vector>
struct Body { unsigned char raw[0x280]{}; int id,list,kind,target; bool fired=false; };
std::array<Body,256> bodies; std::array<Body*,7> heads{};
std::vector<int> events; int nextId,tick;
Body*& prev(Body*b){return *reinterpret_cast<Body**>(b->raw+0x34);}
Body*& next(Body*b){return *reinterpret_cast<Body**>(b->raw+0x20);}
unsigned short& flags(Body*b){return *reinterpret_cast<unsigned short*>(b->raw+0x46);}
void detach(Body*b){reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*)>(0x460280)(b,&heads[b->list]);}
void __attribute__((thiscall)) destroy(Body*b,int){events.insert(events.end(),{-1,b->id});detach(b);}
void __attribute__((thiscall)) timing(Body*b){events.insert(events.end(),{0,b->id});}
void __attribute__((thiscall)) ai(Body*b);
void* vtable[]={reinterpret_cast<void*>(&destroy),nullptr,reinterpret_cast<void*>(&ai)};
Body* create(int list,int kind,int target,int dead){
 Body*b=&bodies[nextId];*b=Body{};b->id=nextId++;b->list=list;b->kind=kind;b->target=target;
 *reinterpret_cast<void***>(b->raw)=vtable;flags(b)=dead==2?0xC0:dead==1?0x40:0;
 next(b)=heads[list];if(next(b))prev(next(b))=b;heads[list]=b;return b;
}
void __attribute__((thiscall)) ai(Body*b){
 events.insert(events.end(),{tick,b->id});if(b->fired)return;b->fired=true;
 switch(b->kind){
 case 1:create(b->target,0,0,0);break;
 case 2:flags(b)|=0x40;break;
 case 3:detach(b);break;
 case 4:if(next(b))detach(next(b));break;
 case 5:if(next(b))flags(next(b))|=0x40;break;
 }
}
void patch(unsigned address,void* target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;
 *reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x460ED0,reinterpret_cast<void*>(&timing));Body player{};*reinterpret_cast<Body**>(0x6A9038)=&player;
 int count;while(std::cin>>count){nextId=0;events.clear();heads.fill(nullptr);
 for(int i=0;i<count;i++){int list,kind,target,dead;std::cin>>list>>kind>>target>>dead;create(list,kind,target,dead);}
 for(tick=1;tick<=3;tick++)for(int list=0;list<7;list++)reinterpret_cast<void(*)(Body**,int)>(0x460FC0)(&heads[list],0);
 std::cout<<"{\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}
 std::cout<<"],\"heads\":[";for(int l=0;l<7;l++){if(l)std::cout<<',';std::cout<<'[';
 bool comma=false;for(Body*b=heads[l];b;b=next(b)){if(comma)std::cout<<',';comma=true;std::cout<<b->id;}std::cout<<']';}
 std::cout<<"]}\n";
 }}
'''
    rng = random.Random(0x460FC0)
    cases = [dict(actors=[[group, 0, 0, dead] for group in range(7)]) for dead in range(3)]
    cases.extend(dict(actors=[[source, 0, 0, 0], [source, 1, target, 0]])
                 for source in range(7) for target in range(7))
    cases.extend(dict(actors=[[group, 0, 0, 0], [group, kind, 0, 0]])
                 for group in range(7) for kind in range(2, 6))
    for _ in range(160):
        cases.append(dict(actors=[[rng.randrange(7), rng.randrange(6), rng.randrange(7),
                                   rng.randrange(3)] for _ in range(rng.randrange(1, 40))]))
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-list-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [len(case['actors'])] + [v for actor in case['actors'] for v in actor])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/actor-list.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} original Ob_AI schedules; only the separate EveryFrame timing leaf is isolated.')


if __name__ == '__main__':
    main()
