"""Capture original Panel_CreateHealthBar/Destroy ownership and texture lookup order."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


TEXTURES = ["scorpion", "boss", "jonah", "scorpion_wounded", "rhino", "rhino_wounded",
            "mysterio", "mysterio_wounded", "venom", "maryjane_01", "maryJane_bar",
            "venom_wounded", "dococ", "docOc_wounded", "carnage", "carnage_wounded"]

HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
template<class T>T&mem(unsigned address){return *reinterpret_cast<T*>(address);}
std::array<std::array<unsigned char,256>,4> bodies{};
std::vector<std::vector<long long>> events;
const char*names[]={"scorpion","boss","jonah","scorpion_wounded","rhino","rhino_wounded","mysterio","mysterio_wounded","venom","maryjane_01","maryJane_bar","venom_wounded","dococ","docOc_wounded","carnage","carnage_wounded"};
unsigned bodyId(void*body){if(!body)return 0;for(unsigned i=0;i<4;i++)if(body==bodies[i].data())return i+1;std::abort();}
std::vector<long long>snapshot(){std::vector<long long>v={bodyId(mem<void*>(0x60F788)),bodyId(mem<void*>(0x60F78C)),mem<int>(0x60F654),mem<int>(0x60F744),mem<int>(0x60F748)};
 for(unsigned i=0;i<5;i++)v.push_back(mem<unsigned>(0x60F658+4*i));return v;}
void numbers(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
void*findTexture(const char*name){unsigned id=0;for(unsigned i=0;i<16;i++)if(!std::strcmp(names[i],name)){id=i+1;break;}
 if(!id){std::cerr<<"Unexpected original texture name: "<<name;std::abort();}std::vector<long long>event={id};auto state=snapshot();event.insert(event.end(),state.begin(),state.end());events.push_back(event);return reinterpret_cast<void*>(id);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 auto*p=(unsigned char*)0x4C94B0;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)&findTexture-0x4C94B0-5;
 int count;while(std::cin>>count){mem<void*>(0x60F788)=mem<void*>(0x60F78C)=nullptr;mem<int>(0x60F654)=mem<int>(0x60F744)=mem<int>(0x60F748)=0;
  for(unsigned i=0;i<5;i++)mem<unsigned>(0x60F658+4*i)=0;
  std::cout<<'[';for(int step=0;step<count;step++){int operation,body,type,health;std::cin>>operation>>body>>type>>health;events.clear();
   if(operation==0){*reinterpret_cast<short*>(bodies[body].data()+0xE2)=health;reinterpret_cast<void(*)(void*,int)>(0x463610)(bodies[body].data(),type);}
   else reinterpret_cast<void(*)()>(0x4637F0)();
   if(step)std::cout<<',';std::cout<<"{\"state\":";numbers(snapshot());std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}std::cout<<"]}";
  }std::cout<<"]\n";
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    types = [307, 308, 310, 311, 313, 314, 316, -1, 0, 306, 317, 65535]
    health = [-32768, -1, 0, 1, 120, 900, 32767]
    sequences = [[[0, 0, actor, value], [0, 1, 316, 120], [0, 2, 310, 900], [1, 0, 0, 0]]
                 for actor in types for value in health]
    rng = random.Random(0x463610)
    for _ in range(100):
        sequences.append([[rng.choice([0, 0, 0, 1]), rng.randrange(4), rng.choice(types), rng.choice(health)] for _ in range(12)])
    with tempfile.TemporaryDirectory(prefix='openspidey-boss-health-bars-') as folder:
        source, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        source.write_text(HARNESS)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(source), '-o', str(binary)], check=True)
        rows = [str(len(sequence)) + ' ' + ' '.join(str(value) for step in sequence for value in step) for sequence in sequences]
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(rows) + '\n').encode()).decode().splitlines()
    cases = [dict(input=sequence, expected=json.loads(line)) for sequence, line in zip(sequences, lines, strict=True)]
    destination = root / 'modern/OpenSpidey.Tests/Fixtures/boss-health-bars.json'
    destination.write_text(json.dumps(dict(textures=TEXTURES, cases=cases), separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original health-bar sequences ({sum(map(len, sequences))} calls), including every callback-visible body/health/type/texture slot and retained destruction metadata.')


if __name__ == '__main__':
    main()
