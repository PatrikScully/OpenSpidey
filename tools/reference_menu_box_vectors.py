"""Execute the original PC PShell_DrawMenuBox 0x47A700 and record every panel draw it submits.

The textured-quad (0x4626A0) and flat-shaded (0x462D60) panel calls are replaced by recorders; the two
POLY_FT4 allocators (0x462B30, 0x462B90) hand out scratch polys. Frames come from gAnimTable[15] (0x56EAA0).
"""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

CODE = r'''
#include "original_executable.h"
#include <cstring>
#include <iostream>
#include <vector>
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
unsigned char&byte(unsigned address){return *reinterpret_cast<unsigned char*>(address);}
void jump(unsigned address,void*target){byte(address)=0xE9;word(address+1)=reinterpret_cast<int>(target)-address-5;}
unsigned char frames[9*8];unsigned char polys[32][0x28];int next;std::vector<std::vector<long long>>calls;
void*poly(){void*p=polys[next++&31];std::memset(p,0,0x28);return p;}
void*framePoly(void*,int,int,int){return poly();}
void*plainPoly(void*,int){return poly();}
int frameIndex(void*frame){return (static_cast<unsigned char*>(frame)-frames)/8;}
void textured(float z,void*,void*frame,int x,int y,int w,int h,int,unsigned flags){
 calls.push_back({0,(long long)(z*1000),frameIndex(frame),x,y,w,h,flags,0,0,0});}
void flat(float z,int x,int y,int w,int h,unsigned char r,unsigned char g,unsigned char b,int,int blend){
 calls.push_back({1,(long long)(z*1000),-1,x,y,w,h,r,g,b,blend});}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 jump(0x462B30,reinterpret_cast<void*>(&framePoly));jump(0x462B90,reinterpret_cast<void*>(&plainPoly));
 jump(0x4626A0,reinterpret_cast<void*>(&textured));jump(0x462D60,reinterpret_cast<void*>(&flat));
 word(0x56EAA0)=reinterpret_cast<int>(frames);
 int x,y,w,h,fill,scroll,pos,thumb,sort;
 while(std::cin>>x>>y>>w>>h>>fill>>scroll>>pos>>thumb>>sort){calls.clear();
  *reinterpret_cast<unsigned short*>(0x60D5A6)=sort;
  int result=reinterpret_cast<int(*)(int,int,int,int,int,int,int,int)>(0x47A700)(x,y,w,h,fill,scroll,pos,thumb);
  std::cout<<"{\"result\":"<<result<<",\"calls\":[";
  for(unsigned i=0;i<calls.size();i++){if(i)std::cout<<',';std::cout<<'[';
   for(unsigned j=0;j<calls[i].size();j++){if(j)std::cout<<',';std::cout<<calls[i][j];}std::cout<<']';}
  std::cout<<"]}\n";std::cout.flush();}}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    rng = random.Random(0x47A700)
    inputs = [[32, 50, 45, 34, 1, 0, 0, 28, 0], [256 - 70, 120, 140, 64, 1, 0, 0, 28, 0x100],
              [100, 40, 200, 150, 1, 1, 0, 28, 0xFFE], [100, 40, 200, 150, 0, 1, 255, 28, 0xFFF]]
    for _ in range(96):
        inputs.append([rng.randrange(-40, 520), rng.randrange(-40, 260), rng.randrange(0, 400), rng.randrange(0, 240),
                       rng.choice([0, 1, 5]), rng.choice([0, 1]), rng.randrange(0, 256), rng.choice([0, 12, 28, 40]),
                       rng.choice([0, 0x80, 0xFFD, 0xFFE, 0xFFF, 0x1000])])
    with tempfile.TemporaryDirectory() as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(CODE)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = '\n'.join(' '.join(map(str, row)) for row in inputs) + '\n'
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=rows.encode())
    results = [json.loads(line) for line in output.decode().splitlines()]
    cases = [dict(input=row, **result) for row, result in zip(inputs, results, strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/menu-box.json').write_text(json.dumps(cases, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} original PShell_DrawMenuBox calls.')


if __name__ == '__main__':
    main()
