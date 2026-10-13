"""Capture the whole unmodified original SwitchToDeathMode(true), its training boundary and unchanged raw player bytes."""
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
#include <iostream>
int level,queries,training;
int levelId(){queries++;return level;}
void trainingDeath(const char*name,unsigned slot,const unsigned*arguments,unsigned*result){assert(std::strcmp(name,"training")==0&&slot==1&&arguments==nullptr&&result==nullptr);training++;}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4DE770,(void*)&levelId);patch(0x47D470,(void*)&trainingDeath);
 int status;unsigned seed;while(std::cin>>level>>status>>seed){
  alignas(4)unsigned char player[0xF20],previous[0xF20];unsigned value=seed;
  for(auto&byte:player){value=value*1664525u+1013904223u;byte=value>>24;}std::memcpy(previous,player,sizeof(player));
  queries=training=0;*(int*)0x60CFA4=status;
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*,bool)>(0x4BDFF0)(player,true);
  std::cout<<'['<<*(int*)0x60CFA4<<','<<queries<<','<<training<<','<<(std::memcmp(player,previous,sizeof(player))==0)<<"]\n";
 }}
'''
    levels = [0, 0x104, 0x8FF, 0x900, 0x901, 0x17FF, 0x1800, -1, -2147483648]
    cases = [[level, status, 0x4BDFF0] for level in levels for status in [0, 2, -1, 0x7FFFFFFF]]
    rng = random.Random(0x4BDFF0)
    for _ in range(300):
        cases.append([rng.choice([rng.randrange(0x2000), rng.randrange(-2147483648, 2147483648)]),
                      rng.randrange(-2147483648, 2147483648), rng.randrange(2**32)])
    with tempfile.TemporaryDirectory(prefix='openspidey-player-script-death-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    path = root / 'modern/OpenSpidey.Tests/Fixtures/player-script-death.json'
    path.write_text(json.dumps([dict(input=row, expected=json.loads(line))
        for row, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original scripted death requests: unsigned level-group selection, actual training function1/null arguments, status-only exit and all0xF20 raw player bytes unchanged.')


if __name__ == '__main__':
    main()
