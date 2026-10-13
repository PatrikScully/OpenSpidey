"""Capture original CCamera::SetTripodMotion417710 with signed, wrapped rate calculation and live body reset."""
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
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 long long v[13];while(std::cin>>v[0]){for(int i=1;i<13;i++)std::cin>>v[i];
  alignas(4)unsigned char camera[0x300]={},body[0x100]={};int target[3];
  for(int i=0;i<3;i++){at<int>(camera,0x104+i*4)=v[i];at<int>(body,8+i*4)=v[3+i];target[i]=v[6+i];}
  at<int>(camera,0x128)=v[9];at<void*>(camera,0xFC)=v[10]?body:nullptr;
  at<unsigned short>(body,0x46)=v[11]?0x40:0;
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*,const int*,unsigned)>(0x417710)(camera,target,v[12]);
  std::cout<<'['<<at<int>(camera,0x128);
  for(unsigned offset=0x104;offset<0x128;offset+=4)std::cout<<','<<at<int>(camera,offset);
  std::cout<<"]\n";
 }}
'''
    rng = random.Random(0x417710)
    cases = [[*[rng.randint(-2147483648, 2147483647) for _ in range(9)], previous, exists, dead, duration]
             for previous in [0, 1, 16, -1] for exists in [0, 1] for dead in [0, 1]
             for duration in [1, 2, 16, 65534, 2147483647, 2147483648, 4294967295]]
    with tempfile.TemporaryDirectory(prefix='openspidey-tripod-motion-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode())
    fixtures = [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-tripod-motion.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} untouched original tripod motion setups with live-body reset and signed vector rates.')


if __name__ == '__main__':
    main()
