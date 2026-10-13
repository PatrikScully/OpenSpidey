"""Execute actual PC Exp_HitEnvItem and Crate_Destroy with bounded leaf stubs."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cstring>
#include <cassert>
std::vector<int> events;
int shatterResult, randomValue;
void patch(unsigned address,void* target){auto* p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;
 *reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void diagnostic(int,const char*){}
void destroyed(void*){events.push_back(1);}
int face(void*,void*,int a,int b,int c,int d,int e){assert(a==1&&b==1&&c==1&&d==1&&e==1);events.push_back(2);return 1;}
int shattered(void*,int seen,int processed){events.insert(events.end(),{1,seen,processed});return shatterResult;}
int environmentIndex(int region){events.insert(events.end(),{2,region});return 0;}
void* commandPoint(unsigned checksum,int asserting){events.insert(events.end(),{3,int(checksum),asserting});return nullptr;}
int randomNumber(int range){events.insert(events.end(),{4,range});return randomValue;}
void smallExplosion(int* position){events.insert(events.end(),{5,position[0],position[1],position[2]});}
void bigExplosion(int* position){events.insert(events.end(),{6,position[0],position[1],position[2]});}
int main(int argc,char** argv){
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4015B0,reinterpret_cast<void*>(&diagnostic));patch(0x48C0D0,reinterpret_cast<void*>(&face));
 patch(0x48CFF0,reinterpret_cast<void*>(&shattered));patch(0x4CAFE0,reinterpret_cast<void*>(&environmentIndex));
 patch(0x4DE8D0,reinterpret_cast<void*>(&commandPoint));patch(0x4E5DA0,reinterpret_cast<void*>(&randomNumber));
 patch(0x43C430,reinterpret_cast<void*>(&smallExplosion));patch(0x43C3E0,reinterpret_cast<void*>(&bigExplosion));
 alignas(4) unsigned char item[0x40]={},decoy[0x40]={},model[32]={};
 *reinterpret_cast<unsigned short*>(item+0x1A)=0;item[0x1F]=3;decoy[0x1F]=3;
 *reinterpret_cast<unsigned char**>(decoy+0x20)=item;
 unsigned char* models[]={model};
 *reinterpret_cast<void**>(0x6B2454+68*3)=models;
 *reinterpret_cast<unsigned char*>(0x6B244A+68*3)=1;
 unsigned checksums[]={0x12345678};*reinterpret_cast<void**>(0x6AC20C)=checksums;
 int mode;
 while(std::cin>>mode){events.clear();
  if(mode==0){int present,flags,owned,modelFlags,hasFace,damage;
   std::cin>>present>>flags>>owned>>modelFlags>>hasFace>>damage;
   *reinterpret_cast<unsigned short*>(item+4)=flags;
   *reinterpret_cast<unsigned short*>(model)=modelFlags;
   *reinterpret_cast<void**>(0x6B2EFC)=owned?decoy:nullptr;
   patch(0x42FFC0,reinterpret_cast<void*>(&destroyed));
   reinterpret_cast<void(*)(void*,void*,int)>(0x43C480)(present?item:nullptr,hasFace?model:nullptr,damage);
   assert(*reinterpret_cast<unsigned short*>(model)==modelFlags);
  }else{int recursion,lowMemory;
   std::cin>>recursion>>shatterResult>>lowMemory>>randomValue;
   *reinterpret_cast<int*>(0x56FA8C)=recursion;*reinterpret_cast<int*>(0x60D224)=lowMemory;
   *reinterpret_cast<int*>(item+8)=100;*reinterpret_cast<int*>(item+12)=200;*reinterpret_cast<int*>(item+16)=300;
   *reinterpret_cast<void**>(0x6B2EFC)=decoy;
   // Restore the original Crate_Destroy entry overwritten for the preceding policy case.
   const unsigned char entry[]={0xA1,0x8C,0xFA,0x56,0x00};std::memcpy(reinterpret_cast<void*>(0x42FFC0),entry,5);
   *reinterpret_cast<unsigned short*>(model)=2;
   reinterpret_cast<void(*)(void*)>(0x42FFC0)(item);
   assert(*reinterpret_cast<int*>(0x56FA8C)==recursion);
  }
  std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";
 }
}
'''
    rng = random.Random(0x43C480)
    cases = []
    for model in [0, 1, 2, 3, 4, 0x20, 0x40, 0x100, 0xFF00, 0xFF02, 0xFFFF]:
        for flags in [0, 1, 0x1000, 0x1001]:
            for damage in [-1, 0, 1, 65534, 65535, 65536, -2147483648, 2147483647]:
                for face in [0, 1]:
                    cases.append(dict(mode=0, input=[1, flags, 1, model, face, damage]))
    for _ in range(256):
        cases.append(dict(mode=0, input=[rng.randrange(2), rng.randrange(65536), rng.randrange(2),
                                         rng.randrange(65536), rng.randrange(2), rng.choice([0, 1, 65535, rng.randint(-2147483648, 2147483647)])]))
    for recursion in [-2147483648, -1, 0, 8, 9, 10, 2147483647]:
        for shatter in [0, 1]:
            for low_memory in [0, 1]:
                for random_value in [0, 1, 4]:
                    cases.append(dict(mode=1, input=[recursion, shatter, low_memory, random_value]))
    with tempfile.TemporaryDirectory(prefix='openspidey-environment-impact-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [case['mode'], *case['input']])) for case in cases]
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(rows) + '\n').encode()).decode().splitlines()
    for case, result in zip(cases, output, strict=True):
        case['expected'] = json.loads(result)
    target = root / 'modern/OpenSpidey.Tests/Fixtures/environment-impact.json'
    target.write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Generated {len(cases)} actual PC ENV hit/crate cases; bit2 branch, full-damage gate, disabled neighbor propagation and recursion unwind verified.')


if __name__ == '__main__':
    main()
