"""Capture CTurret constructor stores from SpideyPC.exe (0x4E4780)."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cassert>
#include <cstring>
#include <iostream>
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void __attribute__((thiscall)) initItem(void*,const char*){}
void __attribute__((thiscall)) attach(void*,void**){}
void __attribute__((thiscall)) readHooks(void*,void*){}
void __attribute__((thiscall)) runAnim(void*,int,int,int){}
int main(int argc,char**argv){
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x460020,(void*)&initItem);patch(0x460260,(void*)&attach);patch(0x453c30,(void*)&readHooks);patch(0x460c10,(void*)&runAnim);
char mode;while(std::cin>>mode){alignas(4) unsigned char b[0x400]={};short data[6];int node,level;std::cin>>node>>level;
for(int i=0;i<3;i++){int x;std::cin>>x;*(int*)(data+i*2)=x;}for(int i=0;i<3;i++){int x;std::cin>>x;*(short*)(data+6+i)=x;}
reinterpret_cast<void*(__attribute__((thiscall))*)(void*,short*,int)>(0x4E4780)(b,(short*)data,node);
std::cout<<'['<<*(unsigned short*)(b+4)<<','<<*(short*)(b+0x21e)<<','<<*(unsigned short*)(b+0xde)<<','
<<*(int*)(b+0x2a8)<<','<<*(short*)(b+0xdc)<<','<<*(short*)(b+0x12a)<<','<<*(int*)(b+0x20c)<<"]\n";}
return 0;}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rows = []
    for node in [4, 37, 200]:
        for level in [0x802, 0x504]:
            rows.append(f"C {node} {level} 100 -50 200 1024 -31 4095")
    with tempfile.TemporaryDirectory(prefix='openspidey-turret-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode())
    fixtures = []
    for line, row in zip(result.decode().splitlines(), rows):
        parts = row.split()
        fixtures.append(dict(mode='C', input=[int(parts[1]), int(parts[2]), 100, -50, 200, 1024, -31, 4095],
                             expected=json.loads(line)))
    out = root / 'modern/OpenSpidey.Tests/Fixtures/turret.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} CTurret constructor cases.')


if __name__ == '__main__':
    main()
