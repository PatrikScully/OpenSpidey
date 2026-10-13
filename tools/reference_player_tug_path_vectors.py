"""Capture original PC CalculateTugWebPathPoints with its actual vector normalization and trigonometry."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True); args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <cstring>
#include <cmath>
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void* allocate(unsigned size,int,int,void*,bool){return calloc(1,size);}
void* recover(unsigned*handle){return reinterpret_cast<void*>(handle[0]);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4581A0,reinterpret_cast<void*>(&allocate));patch(0x4583F0,reinterpret_cast<void*>(&recover));
auto*trig=reinterpret_cast<short*>(0x610C48);for(int i=0;i<4096;i++){double a=i*0.001536096911877394;trig[2*i]=std::sin(a)*4096.;trig[2*i+1]=std::cos(a)*4096.;}
CVector player,target;while(std::cin>>player.vx>>player.vy>>player.vz>>target.vx>>target.vy>>target.vz){alignas(4)unsigned char raw[0xE70]={},web[0x13C]={},body[0x14]={};
*reinterpret_cast<CVector*>(raw+8)=player;*reinterpret_cast<CVector*>(body+8)=target;*reinterpret_cast<void**>(raw+0xE6C)=web;*reinterpret_cast<void**>(web+0x134)=body;
auto*points=reinterpret_cast<CVector*(__attribute__((thiscall)) *)(void*)>(0x4C5C60)(raw);std::cout<<'[';if(points)for(int i=0;i<8;i++){if(i)std::cout<<',';std::cout<<points[i].vx<<','<<points[i].vy<<','<<points[i].vz;}std::cout<<"]\n";free(points);}}
'''
    cases=[]; rng=random.Random(0x4C5C60)
    for vertical in [0, -524288, 524288, -524289, 524289, 122879]:
        for delta in [[0, 0], [1, 1], [4095, -4095], [1000000, -1500000], [-2512345, 674123]]:
            cases.append([123456, -54321, 76543, 123456+delta[0], -54321+vertical, 76543+delta[1]])
    for _ in range(100):
        start=[rng.randint(-10000000,10000000) for _ in range(3)]
        cases.append([*start,start[0]+rng.randint(-5000000,5000000),start[1]+rng.randint(-524289,524289),start[2]+rng.randint(-5000000,5000000)])
    with tempfile.TemporaryDirectory(prefix='openspidey-player-tug-path-') as folder:
        cpp=Path(folder)/'reference.cpp'; binary=Path(folder)/'reference'; cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-tug-path.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC eight-point tug paths and strict vertical rejection cases.')


if __name__=='__main__':main()
