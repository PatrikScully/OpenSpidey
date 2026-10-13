"""Capture the actual original-PC final triggered/mirror reset, preserving source slots14/15."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
extern "C" __attribute__((naked)) void done(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;ret");}
extern "C" __attribute__((naked)) void finalize(void*){asm volatile("push %ebx;push %ebp;push %esi;push %edi;mov 20(%esp),%esi;mov $0x4B867F,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);auto*p=reinterpret_cast<unsigned char*>(0x4B86B1);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(&done)-0x4B86B1-5;
int first;while(std::cin>>first){alignas(4)unsigned char player[0xE10]={},pad[0x140]={};pad[1]=first;for(int i=1;i<20;i++){int value;std::cin>>value;pad[i*16+1]=value;}for(int i=0;i<20;i++){int value;std::cin>>value;player[0x1C1+i*16]=value;}*reinterpret_cast<void**>(player+0xE0C)=pad;finalize(player);std::cout<<'[';for(int i=0;i<40;i++){if(i)std::cout<<',';std::cout<<unsigned(i<20?pad[i*16+1]:player[0x1C1+(i-20)*16]);}std::cout<<"]\n";}}
'''
    rng = random.Random(0x4B867F); cases = [[value] * 40 for value in [0, 1, 77, 255]] + [[rng.randrange(256) for _ in range(40)] for _ in range(60)]
    with tempfile.TemporaryDirectory(prefix='openspidey-input-finalizer-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-input-finalizer.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} actual PC finalizer slices / all40 raw triggered and mirror bytes.')


if __name__ == '__main__': main()
