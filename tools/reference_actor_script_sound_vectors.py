"""Capture inherited CBaddy sound commands through the actual PC CPlatform dispatcher."""
import argparse
import json
from pathlib import Path
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
unsigned sounded;int volume,pitch,calls;
int play(unsigned sound,int v,int p){sounded=sound;volume=v;pitch=p;calls++;return 0;}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset(reinterpret_cast<void*>(0x4050B0),0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 std::memset(reinterpret_cast<void*>(0x4050BE),0x90,7);
 std::memset(reinterpret_cast<void*>(0x406ACB),0x90,7);
 jump(0x471C50,reinterpret_cast<void*>(&play));int opcode,value;
 while(std::cin>>opcode>>value){alignas(4) unsigned char body[0x400]={};unsigned short operand=value;
  *reinterpret_cast<unsigned short*>(body+0x38)=402;*reinterpret_cast<unsigned short**>(body+0x24C)=&operand;
  calls=0;sounded=0;volume=pitch=-1;
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x469690)(body,opcode);
  std::cout<<'['<<result<<','<<calls<<','<<sounded<<','<<volume<<','<<pitch<<','
   <<(*reinterpret_cast<unsigned char**>(body+0x24C)-reinterpret_cast<unsigned char*>(&operand))<<"]\n";
 }}
'''
    cases = [dict(opcode=opcode, operand=value) for opcode in [0x428E, 0x4290]
             for value in [0, 1, 52, 90, 299, 300, 309, 310, 0x2000, 0x212B, 32767, 32768, 65535]]
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-script-sound-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [f"{case['opcode']} {case['operand']}" for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/actor-script-sound.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC CPlatform/CBaddy script sound commands; raw word reading and signed promotion remain untouched.')


if __name__ == '__main__':
    main()
