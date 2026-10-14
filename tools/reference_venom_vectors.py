"""Capture CVenom wait states from SpideyPC.exe; constructor from recovered stores."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <iostream>
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void nullsub(){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4015B0,(void*)nullsub);
int enter,state,sub,cmd,op;while(std::cin>>enter>>state>>sub>>cmd>>op){
 alignas(4) unsigned char b[0x900]={};
 *(int*)(b+0x31C)=state;*(int*)(b+0x320)=sub;*(int*)(b+0x3B0)=11;*(int*)(b+0x3B4)=22;
 if(enter)reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4ECED0)(b);
 else reinterpret_cast<void(__attribute__((thiscall))*)(void*,unsigned,unsigned)>(0x4ECF00)(b,cmd,op);
 std::cout<<'['<<*(int*)(b+0x31C)<<','<<*(int*)(b+0x320)<<','<<*(int*)(b+0x3B0)<<','<<*(int*)(b+0x3B4)<<"]\n";}
}
'''


def constructor_expected(level, node, difficulty):
    model2 = 1 if level in (0x501, 0x503, 0x504, 0x601, 0x602, 0x505, 0x506, 0x9904) else 0
    minor = 0 if model2 else 0xC0
    ai, sub, health, field39c, field218 = 1, 0, 0, 0, 0
    if level == 0x603:
        sub = 1
        health = 0x258
        field39c = 0x3C if difficulty in (0, 1) else 0x14
    elif level in (0x501, 0x502, 0x503, 0x504, 0x505, 0x506, 0x601, 0x602, 0x604):
        ai = 0x4000
        if level == 0x501:
            field218 = 0x8000
        elif level == 0x502:
            health = 0x258
            field39c = 0x3C if difficulty in (0, 1) else 0x14
        elif level == 0x604:
            health = 0x258
            field39c = 0x46 if difficulty in (0, 1) else 0x32
    return [313, minor, level, ai, sub, health, field39c, field218, model2, node]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for level in [0x501, 0x502, 0x503, 0x603, 0x604, 0x201]:
        for node in [10, 135]:
            for difficulty in [0, 2]:
                cases.append(dict(mode='C', input=[level, node, difficulty], expected=constructor_expected(level, node, difficulty)))
    wait_rows = [[1, 5, 3, 0, 0], [1, 1, 0, 0, 0], [0, 1, 0, 4, 0xBEEF], [0, 1, 0, 2, 9], [0, 5, 2, 4, 3]]
    with tempfile.TemporaryDirectory(prefix='openspidey-venom-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        payload = '\n'.join(' '.join(map(str, row)) for row in wait_rows) + '\n'
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=payload.encode())
    for row, line in zip(wait_rows, result.decode().splitlines(), strict=True):
        cases.append(dict(mode='W', input=row, expected=json.loads(line)))
    path = root / 'modern/OpenSpidey.Tests/Fixtures/venom.json'
    path.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} Venom cases ({sum(1 for c in cases if c["mode"]=="C")} constructor, '
          f'{sum(1 for c in cases if c["mode"]=="W")} original wait).')


if __name__ == '__main__':
    main()
