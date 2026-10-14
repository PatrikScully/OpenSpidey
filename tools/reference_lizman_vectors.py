"""Capture CLizMan DoLizmanPhysics and ClearAttackFlags from SpideyPC.exe; constructor from recovered stores."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cmath>
#include <cstring>
#include <iostream>
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
alignas(4) unsigned char player[0x600]={};
void fillSin(){for(int i=0;i<4096;i++){double a=i*0.001536096911877394;*(short*)(0x610C48+i*4)=(short)(sin(a)*4096);*(short*)(0x610C4A+i*4)=(short)(cos(a)*4096);}}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);fillSin();
*(void**)0x6a9038=player;*(int*)0x60d9e0=111;*(int*)0x60d9e4=222;*(int*)0x60d9e8=0;
char mode;while(std::cin>>mode){
if(mode=='P'){alignas(4) unsigned char b[0x3C0]={};int anim,frame,ticks;std::cin>>anim>>frame>>ticks;
*(unsigned short*)(b+0x12a)=anim;*(short*)(b+0x128)=frame;*(int*)(b+0x80)=ticks;
for(int o:{8,0x60,0x6c})for(int i=0;i<3;i++){int x;std::cin>>x;*(int*)(b+o+i*4)=x;}
for(int o:{0x14,0x88,0x8e})for(int i=0;i<3;i++){int x;std::cin>>x;*(short*)(b+o+i*2)=x;}
for(int i=0;i<3;i++){int x;std::cin>>x;b[0x78+i]=x;}for(int i=0;i<3;i++){int x;std::cin>>x;b[0x94+i]=x;}
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x450920)(b);
std::cout<<'['<<*(int*)(b+8)<<','<<*(int*)(b+12)<<','<<*(int*)(b+16)<<','
<<*(int*)(b+0x60)<<','<<*(int*)(b+0x64)<<','<<*(int*)(b+0x68)<<','
<<*(int*)(b+0x6c)<<','<<*(int*)(b+0x70)<<','<<*(int*)(b+0x74)<<','
<<*(short*)(b+0x14)<<','<<*(short*)(b+0x16)<<','<<*(short*)(b+0x18)<<','
<<*(short*)(b+0x88)<<','<<*(short*)(b+0x8a)<<','<<*(short*)(b+0x8c)<<','
<<*(short*)(b+0x8e)<<','<<*(short*)(b+0x90)<<','<<*(short*)(b+0x92)<<','
<<*(int*)(b+0xa8)<<','<<*(int*)(b+0xac)<<','<<*(int*)(b+0xb0)<<','
<<*(unsigned short*)(b+0xe0)<<','<<*(int*)(b+0x2ac)<<"]\n";}
else{alignas(4) unsigned char b[0x3C0]={};int global,slot,mask,self;std::cin>>global>>slot>>mask>>self;
*(unsigned char*)(b+0x39c)=slot;*(unsigned char*)(b+0x39d)=mask;
*(void**)0x682c44=global?(void*)b:nullptr;*(unsigned char*)0x682b6e=self;player[1404]=1;
reinterpret_cast<int(__attribute__((thiscall))*)(void*)>(0x44afc0)(b);
std::cout<<'['<<(int)(*(void**)0x682c44!=nullptr)<<','<<(int)*(unsigned char*)0x682b6e<<','
<<(int)*(unsigned char*)(b+0x39c)<<','<<(int)*(unsigned char*)(b+0x39d)<<"]\n";}
}}
'''


def constructor_expected(level, node, attack=42, total=1000):
    model2 = 1 if level in (0x503, 0x504, 0x601, 0x602) else 0
    field398 = 0x32000 if level == 0x505 else 0x3C000
    field212 = 0xF if level == 0x505 else 0x1E
    behavior = 0x8000 if level == 0x505 else 0
    combat = 0x2000000 if level == 0x505 else 0
    return [317, 100, level, attack, 0x1000, 0x21, 0x30, 0x80, 0x20, 0x40, node, node & 0xFFFF,
            0, 0, field398, field212, behavior, combat, 0x02020201, 0, total - 0x131, model2]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x317)
    cases = []
    for level in [0x503, 0x504, 0x505, 0x601, 0x602, 0x603, 0x201]:
        for node in [10, 135, 0xFFFF]:
            cases.append(dict(mode='C', input=[level, 0, node, 42, 1000], expected=constructor_expected(level, node)))
    physics_inputs = []
    for anim in [0, 2, 8, 0x23, 5]:
        for frame in [0, 1, 7, 15, 23]:
            for ticks in [1, 2, 4]:
                physics_inputs.append([anim, frame, ticks,
                    1000, -2000, 3000, 64, -32, 16, 0, 0x2000, 0,
                    10, -20, 30, 1, -2, 3, 4, -5, 6,
                    1, 1, 1, 1, 1, 1])
    for _ in range(40):
        physics_inputs.append([rng.choice([0, 2, 8, 0x23, 5]), rng.randrange(32), rng.choice([1, 2, 3]),
            *[rng.randint(-100000, 100000) for _ in range(9)],
            *[rng.randint(-32768, 32767) for _ in range(9)],
            *[rng.randrange(1, 4) for _ in range(6)]])
    for row in physics_inputs:
        cases.append(dict(mode='P', input=row))
    for global_me in [0, 1]:
        for slot in [0, 2, 3]:
            for mask in [0, 1, 2, 4]:
                for flag in [0, 1, 3, 7, 15]:
                    cases.append(dict(mode='A', input=[global_me, slot, mask, flag]))
    rows = [case['mode'] + ' ' + ' '.join(map(str, case['input'])) for case in cases if case['mode'] != 'C']
    with tempfile.TemporaryDirectory(prefix='openspidey-lizman-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode())
    exe_rows = result.decode().splitlines()
    fixtures, exe_i = [], 0
    for case in cases:
        if case['mode'] == 'C':
            fixtures.append(case)
        else:
            expected = json.loads(exe_rows[exe_i])
            exe_i += 1
            fixtures.append(dict(**case, expected=expected))
    assert exe_i == len(exe_rows)
    out = root / 'modern/OpenSpidey.Tests/Fixtures/lizman.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} LizMan cases '
          f'({sum(1 for c in fixtures if c["mode"]=="C")} recovered constructor, '
          f'{sum(1 for c in fixtures if c["mode"]=="P")} original physics, '
          f'{sum(1 for c in fixtures if c["mode"]=="A")} original ClearAttackFlags).')


if __name__ == '__main__':
    main()
