"""Capture whole original Jonah GoAroundScorp and VectorNormal, replacing only CRT sqrt transport."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <cmath>
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
double squareRoot(double value){return std::sqrt(value);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x529A44,(void*)&squareRoot);
 int v[13];while(std::cin>>v[0]){for(int i=1;i<13;i++)std::cin>>v[i];alignas(4)unsigned char actor[0x380]={},scorpion[0xC28]={};
  for(int i=0;i<3;i++){at<int>(actor,8+i*4)=v[i];at<int>(scorpion,8+i*4)=v[i+3];}
  for(int i=0;i<6;i++)at<int>(actor,0x334+i*4)=v[i+6];
  for(int step=0;step<v[12];step++){
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,void*)>(0x446770)(actor,scorpion);
   std::cout<<'[';for(int i=0;i<3;i++)std::cout<<(i?",":"")<<at<int>(actor,8+i*4);for(int i=0;i<6;i++)std::cout<<','<<at<int>(actor,0x334+i*4);std::cout<<"]\n";
   // Genuine outer AI removes the preceding contact displacement before the next contact visit.
   at<int>(actor,8)-=at<int>(actor,0x334);at<int>(actor,16)-=at<int>(actor,0x338);
  }
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x446770)
    cases = []
    for dx in [-409601, -409600, -409599, -4096, -1, 0, 1, 4096, 409599, 409600, 409601]:
        for dz in [-409601, -409600, -409599, -4096, -1, 0, 1, 4096, 409599, 409600, 409601]:
            for displacement in [0, 8191, 8192, -8192, 65536, -65536]:
                cases.append([100001, -200002, 300003, 100001-dx, -200002, 300003-dz, displacement, -displacement, 111, 222, 333, 444, 1])
    for _ in range(300):
        p = [rng.randint(-2147483648, 2147483647) for _ in range(3)]
        q = [rng.randint(-2147483648, 2147483647) for _ in range(3)]
        cases.append(p + q + [rng.randint(-2147483648, 2147483647) for _ in range(6)] + [1])
    for dx, dz in [(0,0),(1,1),(409599,409599),(409600,0),(0,409600),(-409599,127)]:
        for displacement in [0, 8191, 8192, -8192, 65536, -65536, -2147483648]:
            cases.append([100001, -200002, 300003, 100001-dx, -200002, 300003-dz, displacement, -displacement if displacement != -2147483648 else displacement, 111,222,333,444,32])
    with tempfile.TemporaryDirectory(prefix='openspidey-jonah-contact-') as folder:
        cpp, binary = Path(folder)/'reference.cpp', Path(folder)/'reference'
        cpp.write_text(HARNESS)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-fwrapv','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    position = 0
    result = []
    for case in cases:
        observed = [json.loads(line) for line in output[position:position+case[12]]]
        position += case[12]
        result.append(dict(input=case, expected=observed))
    assert position == len(output)
    (root/'modern/OpenSpidey.Tests/Fixtures/jonah-contact.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} whole PC contact cases and {position} actual source visits with unpatched Q12 normalization.')


if __name__ == '__main__':
    main()
