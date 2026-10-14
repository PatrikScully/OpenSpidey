"""Capture original PC300/301/303/304 dispatch and following mission commands."""
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
#include <array>
#include <iostream>
#include <vector>
std::vector<std::array<unsigned,4>> calls;
void module(const char*name,unsigned slot,const unsigned*arguments,unsigned*result){
 assert(std::strcmp(name,"mysterio")==0);assert(arguments&&result);
 calls.push_back(slot==1?std::array<unsigned,4>{slot,arguments[0],arguments[1],arguments[2]}:std::array<unsigned,4>{slot,0,0,0});
 *result=0xDEADBEEF;
}
void patch(unsigned address,void*function){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)function-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset((void*)0x4E021D,0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 for(unsigned address:{0x4E0224u,0x4E2BA0u})std::memset((void*)address,0x90,7);
 patch(0x47D470,(void*)&module);
 const unsigned watched[]={0x660F80,0x60F76C,0x5FAA64,0x5FAA5C,0x5FAA60,0x5FAA58};
 int mode,alignment,blue,packed;
 while(std::cin>>mode>>alignment>>blue>>packed){
  calls.clear();alignas(4)std::array<unsigned short,40> storage{};auto*p=storage.data()+alignment/2;auto*start=p;
  if(mode!=1){*p++=300;*p++=301;}
  if(mode!=0){*p++=303;*p++=blue;*p++=packed;*p++=304;*p++=303;*p++=packed;*p++=blue;}
  if(mode==2){*p++=301;*p++=300;*p++=304;}
  *p++=147;*p++=0x5678;*p++=302;*p++=1234;*p++=65535;
  *(void**)0x6A9038=nullptr;*(int*)0x5FCD14=-123;*(int*)0x558050=-234;
  for(unsigned i=0;i<sizeof watched/4;i++)*(int*)watched[i]=0x13570000+i*37;
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(start,65535,0);
  std::cout<<"{\"gameLevel\":"<<*(int*)0x5FCD14<<",\"endNode\":"<<*(int*)0x558050<<",\"calls\":[";
  for(unsigned i=0;i<calls.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned j=0;j<4;j++){if(j)std::cout<<',';std::cout<<calls[i][j];}std::cout<<']';}
  std::cout<<"],\"watched\":[";for(unsigned i=0;i<sizeof watched/4;i++){if(i)std::cout<<',';std::cout<<*(int*)watched[i];}std::cout<<"]}\n";
 }
}
'''
    values = [0, 1, 31, 255, 256, 0x1234, 32767, 32768, 65535]
    cases = [[mode, alignment, blue, packed] for mode in range(3)
             for alignment in [0, 2] for blue in values for packed in values]
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-palette-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    fixtures = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-palette.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} whole-PC command sequences: speedup no-ops, ordered Mysterio slots and unsigned palette arguments, unchanged presentation globals, subsequent147/302.')


if __name__ == '__main__':
    main()
