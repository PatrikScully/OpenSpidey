"""Capture untouched CBody::EveryFrame460ED0 timing, first-update flags and animation snapshots."""
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
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
int assertionCount, timingError;
void assertion(unsigned char good,const char*){++assertionCount;timingError=!good;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 auto*p=reinterpret_cast<unsigned char*>(0x4015B0);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(assertion)-0x4015B5;
 long long v[12];while(std::cin>>v[0]){for(int i=1;i<12;i++)std::cin>>v[i];
  alignas(4)unsigned char body[0x160]={};
  *reinterpret_cast<unsigned*>(0x6B4CA8)=v[0];at<unsigned>(body,0x7C)=v[1];at<int>(body,0x84)=v[2];
  at<unsigned short>(body,0x46)=v[3];at<unsigned short>(body,4)=v[4];
  at<short>(body,0x128)=v[5];at<unsigned short>(body,0x12A)=v[6];at<signed char>(body,0x141)=v[7];
  at<short>(body,0x150)=v[8];at<short>(body,0x152)=v[9];at<unsigned short>(body,0x154)=v[10];at<signed char>(body,0x143)=v[11];
  assertionCount=timingError=0;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x460ED0)(body);
  std::cout<<'['<<at<unsigned>(body,0x7C)<<','<<at<int>(body,0x80)<<','<<at<int>(body,0x84)<<','<<at<unsigned short>(body,0x46)
   <<','<<at<short>(body,0x150)<<','<<at<short>(body,0x152)<<','<<at<unsigned short>(body,0x154)<<','<<int(at<signed char>(body,0x143))
   <<','<<assertionCount<<','<<timingError<<"]\n";
 }}
'''
    rng = random.Random(0x460ED0)
    cases = [[clock, previous, accumulated, flags, item, rng.randrange(-32768, 32768), rng.randrange(65536),
              rng.randrange(-128, 128), 1234, -4321, 54321, -17]
             for clock, previous in [(0, 0), (2, 0), (6, 0), (7, 0), (1000, 1), (0, 4294967295),
                                     (2147483647, 2147483646), (2147483648, 2147483647),
                                     (0, 1), (2147483648, 0), (0, 2147483648)]
             for accumulated in [0, -1, 2147483647, -2147483648]
             for flags in [0, 2, 4, 6, 65535]
             for item in [0, 2, 65533, 65535]]
    with tempfile.TemporaryDirectory(prefix='openspidey-body-frame-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode())
    fixtures = [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/actor-frame-timing.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} original body frame updates, wrapped clock deltas, clamp6, accumulation and animation cache writes.')


if __name__ == '__main__':
    main()
