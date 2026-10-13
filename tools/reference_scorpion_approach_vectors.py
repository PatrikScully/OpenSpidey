"""Capture whole original l2a1lsc Monitor/Clear and mutable eight-slot CPlayer user-function loop."""
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
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
std::vector<int>events;std::array<std::array<int,3>,8>positions;std::array<std::array<unsigned char,4>,8>nodes;std::array<void*,8>offsets;
void*position(int*out,int node){assert(node>=0&&node<8);events.insert(events.end(),{1,node});for(int i=0;i<3;i++)out[i]=positions[node][i];return nullptr;}
void pulse(int node){events.insert(events.end(),{2,node});}
void diagnostic(unsigned char condition,const char*){assert(condition==0);events.push_back(3);}
int mutation;const char*names[]={"l2a1lsc","thug"};
void userFunction(const char*name,unsigned slot,const unsigned*arguments,unsigned*result){assert(arguments==nullptr&&result==nullptr);events.insert(events.end(),{std::strcmp(name,names[0])==0?0:1,int(slot)});if(events.size()==2){if(mutation==1){*(const char**)0x6A9048=names[1];*(unsigned*)0x6A904C=0x84;}else if(mutation==2){*(const char**)0x6A9048=nullptr;*(unsigned*)0x6A904C=0;}else if(mutation==3){*(const char**)0x6A9048=names[1];*(unsigned*)0x6A904C=0xFF;}}}
extern "C" __attribute__((naked)) void userLoop(){asm volatile("push %ebx;push %esi;push %edi;xor %ebx,%ebx;mov $0x4C685F,%eax;jmp *%eax");}
void printEvents(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
void printState(){std::cout<<'['<<unsigned(*(unsigned char*)0x60F771)<<','<<unsigned(*(unsigned char*)0x60F772)<<','<<*(unsigned*)0x60F774<<','<<*(unsigned*)0x54E8D4<<','<<*(unsigned*)0x60F778;
 for(unsigned base:{0x5FB480u,0x5FB488u})for(int i=0;i<5;i++)std::cout<<','<<unsigned(*(unsigned char*)(base+i));std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4E3940,(void*)&position);patch(0x4DFC20,(void*)&pulse);patch(0x4015B0,(void*)&diagnostic);patch(0x47D470,(void*)&userFunction);
 int family;while(std::cin>>family){events.clear();if(family==2){unsigned present,mask;std::cin>>present>>mask>>mutation;*(const char**)0x6A9048=present?names[0]:nullptr;*(unsigned*)0x6A904C=mask;userLoop();std::cout<<'['<<(*(const char**)0x6A9048==nullptr?-1:std::strcmp(*(const char**)0x6A9048,names[0])==0?0:1)<<','<<*(unsigned*)0x6A904C<<',';printEvents();std::cout<<"]\n";continue;}
 long long v[17];for(auto&value:v)std::cin>>value;*(unsigned char*)0x60F771=v[0];*(unsigned char*)0x60F772=v[1];*(unsigned*)0x60F774=v[2];*(unsigned*)0x54E8D4=v[3];*(unsigned*)0x60F778=v[4];for(unsigned base:{0x5FB480u,0x5FB488u})for(int i=0;i<5;i++)*(unsigned char*)(base+i)=v[5+(base==0x5FB480?0:5)+i];*(int*)0x54D474=v[15];*(unsigned*)0x6B4CA8=v[16];
 if(family==0){int count;std::cin>>count;for(int i=0;i<8;i++){int kind;std::cin>>kind>>positions[i][0]>>positions[i][1]>>positions[i][2];at<unsigned short>(nodes[i].data(),0)=kind;offsets[i]=nodes[i].data();}*(void**)0x6B466C=offsets.data();*(int*)0x6B4670=count;reinterpret_cast<void(*)(const unsigned*,unsigned*)>(0x4476B0)(nullptr,nullptr);std::cout<<'[';printState();std::cout<<',';printEvents();std::cout<<"]\n";}
 else{for(int i=0;i<5;i++)*(unsigned*)(0x5FB46C+4*i)=12345+i;reinterpret_cast<void(*)()>(0x447670)();std::cout<<'[';printState();std::cout<<",[";for(int i=0;i<5;i++){if(i)std::cout<<',';std::cout<<*(unsigned*)(0x5FB46C+4*i);}std::cout<<"]]\n";}
 }}
'''
    rng = random.Random(0x4476B0)
    target = [-419 << 12, -2730 << 12, -3826 << 12]
    monitor = []
    for difficulty in range(4):
        for running in [0, 1, 255]:
            for remaining in [0, 1, 65535, 0xFFFFFFFF]:
                for done in [0, 1]:
                    state = [0, running, remaining, 77, 88, 1, 2, 3, 4, done, 5, 6, 7, 8, 9, difficulty, 123456]
                    node = [[6, 0, 0, 0], [8, *target], [1, 0, 0, 0], [1, *target], [1, *target], [7, *target], [1, 0, 0, 0], [255, 0, 0, 0]]
                    monitor.append(dict(state=state, count=8, nodes=node))
    for _ in range(140):
        nodes = [[rng.choice([1, 6, 7, 8]), *[p + rng.choice([-4096, -1, 0, 1, 4095, 4096]) for p in target]] for _ in range(8)]
        state = [rng.randrange(256), rng.choice([0, 1]), rng.choice([0, 1, 0xFFFFFFFF]), rng.randrange(2**32), rng.randrange(2**32),
                 *[rng.randrange(256) for _ in range(4)], rng.choice([0, 1]), *[rng.randrange(256) for _ in range(5)], rng.randrange(4), rng.randrange(2**32)]
        monitor.append(dict(state=state, count=rng.randrange(9), nodes=nodes))
    clears = [[rng.randrange(256), rng.randrange(256), *[rng.randrange(2**32) for _ in range(3)],
               *[rng.randrange(256) for _ in range(10)], rng.randrange(4), rng.randrange(2**32)] for _ in range(64)]
    hooks = [[present, mask, mutation] for present in [0, 1] for mask in [0, 1, 2, 0x80, 0xFF, 0x100, 0xFFFFFFFF] for mutation in range(4)]
    rows = [(0, case, [*case['state'], case['count'], *[value for node in case['nodes'] for value in node]]) for case in monitor]
    rows += [(1, case, case) for case in clears] + [(2, case, case) for case in hooks]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-approach-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, [family, *values])) for family, _, values in rows) + '\n').encode()).decode().splitlines()
    path = root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-approach.json'
    path.write_text(json.dumps([dict(family=family, input=request, expected=json.loads(line))
        for (family, request, _), line in zip(rows, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(monitor)} whole original MonitorTimer +{len(clears)} module clear +{len(hooks)} mutable post-AI user-hook sequences; source flags, four difficulties, uint clocks, ascending actor search, first-match pulse, strict Q12 position and dynamic module mask/name.')


if __name__ == '__main__':
    main()
