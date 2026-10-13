"""Execute original Utils_VblankProcessing packed fade instructions without unrelated XA/render leaves."""
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
__attribute__((naked,noinline))void pixel(unsigned*){asm volatile(
 "push %ebp;push %ebx;push %esi;push %edi;mov 20(%esp),%edi;mov $1,%ebp;mov $0x48A900,%eax;call *%eax;"
 "pop %edi;pop %esi;pop %ebx;pop %ebp;ret");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // The first source block is untouched. Only its boundary restores original EBX and returns before XA work.
 *reinterpret_cast<unsigned short*>(0x4E5C46)=0xC35B;
 // Execute one entire original two-pixel sepia loop; only its exit before outer-row work becomes RET.
 *reinterpret_cast<unsigned char*>(0x48A9C9)=0xC3;
 unsigned initial;int frames;while(std::cin>>initial>>frames){if(frames==0){unsigned word=initial;pixel(&word);std::cout<<word<<'\n';continue;}
  *reinterpret_cast<unsigned*>(0x6B4C9C)=initial;
  std::cout<<'[';for(int frame=0;frame<frames;frame++){if(frame)std::cout<<',';
   reinterpret_cast<void(*)()>(0x4E5C10)();std::cout<<*reinterpret_cast<unsigned*>(0x6B4C9C);}std::cout<<"]\n";
 }}
'''
    rng = random.Random(0x4E5C10)
    cases = [dict(initial=value,frames=frames) for value,frames in
             [(0,5),(1,5),(90,100),(0x10000,5),(0x10001,5),(0x1E005A,125),
              (0xFFFF,8),(0x80000000,8),(0xFFFFFFFF,8),(0x100FF,300)]]
    cases += [dict(initial=rng.getrandbits(32),frames=rng.randrange(1,20)) for _ in range(48)]
    pixels = [0,0xFFFFFFFF,0x7FFF7FFF,0x80008000,0x80007FFF,0x001F03E0,0x7C000001]
    pixels += [rng.getrandbits(32) for _ in range(1024)]
    with tempfile.TemporaryDirectory(prefix='openspidey-source-fade-') as folder:
        cpp = Path(folder)/'reference.cpp'
        binary = Path(folder)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-I',str(root/'src'),
                        '-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        rows = '\n'.join([*[f"{case['initial']} {case['frames']}" for case in cases], *[f"{word} 0" for word in pixels]])+'\n'
        output = subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=rows.encode()).decode().splitlines()
    fixtures = [dict(**case,expected=json.loads(row)) for case,row in zip(cases,output[:len(cases)],strict=True)]
    pixel_fixtures = [[word,int(row)] for word,row in zip(pixels,output[len(cases):],strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/source-game-fade.json').write_text(json.dumps(dict(frames=fixtures,pixels=pixel_fixtures),separators=(',',':'))+'\n')
    print(f'Captured {len(fixtures)} original packed-fade sequences and {len(pixel_fixtures)} original two-pixel sepia conversions.')


if __name__ == '__main__':
    main()
