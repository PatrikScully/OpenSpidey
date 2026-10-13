"""Capture original story208/CTextBox/Move/display and219 clear traversal without starting Windows."""
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
#include <array>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
alignas(4) unsigned char storage[3][0x44],poly[1024];int allocated,draws;std::vector<int>deleted;
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void*allocate(unsigned n){assert(n==0x44&&allocated<3);return storage[allocated++];}
void __attribute__((thiscall)) destroy(void*p,unsigned){deleted.push_back((static_cast<unsigned char*>(p)-storage[0])/0x44);
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*)>(0x408900)(p,(void*)0x56E9DC);--*(int*)0x56EB48;}
void draw(float,float,float,float,unsigned,float,float,float,float,unsigned,float,float,float,float,unsigned,float,float,float,float,unsigned,float){draws++;}
void display(void*p){*(unsigned*)0x56FB04=(unsigned)poly;*(unsigned*)0x5FCD1C=(unsigned)(poly+sizeof(poly));
 *(unsigned char*)0x54D341=1;*(int*)0x568154=640;*(int*)0x568158=480;*(unsigned*)0x61B5FC=512;*(unsigned*)0x628614=240;
 draws=0;reinterpret_cast<void(*)(void**)>(0x40D7C0)(&p);assert(draws==1);}
void snapshot(void*p){display(p);std::cout<<'['<<at<short>(p,0xC)<<','<<at<int>(p,0x3C)<<','<<at<int>(p,0x40)<<','<<unsigned(at<unsigned char>(p,0x37));
 for(unsigned n:{8u,10u,12u,18u})std::cout<<','<<at<short>(poly,n);
 unsigned color=0xA0000000u|(unsigned(at<unsigned char>(poly,4))<<16)|(unsigned(at<unsigned char>(poly,5))<<8)|at<unsigned char>(poly,6);
 std::cout<<','<<color<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Omit only Windows SEH transport and native heap/render transport; all original command/ctor/motion/quad math remains intact.
 std::memset((void*)0x4E021D,0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 std::memset((void*)0x4E0224,0x90,7);std::memset((void*)0x4E2BA0,0x90,7);
 *(unsigned char*)0x4015B0=0xC3;*(unsigned char*)0x46CB90=0xC3;*(unsigned char*)0x506440=0xC3;
 patch(0x4088A0,(void*)&allocate);patch(0x507910,(void*)&draw);*(unsigned*)0x53B394=(unsigned)&destroy;
 long long fields[14];while(std::cin>>fields[0]){for(int i=1;i<14;i++)std::cin>>fields[i];
  std::memset(storage,0,sizeof(storage));allocated=0;deleted.clear();*(unsigned*)0x56E9DC=0;*(int*)0x56EB48=0;
  *(unsigned*)0x60D594=fields[8];*(int*)0x60D230=fields[9];*(int*)0x6B4CA8=fields[10];
  unsigned short program[10]={208};for(int i=0;i<8;i++)program[i+1]=fields[i];program[9]=65535;
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program,65535,0);assert(allocated==1);void*p=storage[0];
  std::cout<<"{\"definition\":["<<at<int>(p,0x10)<<','<<at<int>(p,0x14)<<','<<at<int>(p,0x1C)<<','<<at<int>(p,0x20)<<','<<at<unsigned short>(p,0xE);
  for(unsigned n:{0x34u,0x35u,0x36u})std::cout<<','<<unsigned(at<unsigned char>(p,n));
  std::cout<<"],\"states\":[";at<short>(p,0xC)=fields[11];snapshot(p);
  for(int i=0;i<2;i++){*(int*)0x6B4CA8=fields[12+i];if(!at<unsigned char>(p,0x37))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x40D770)(p);std::cout<<',';snapshot(p);}
  std::cout<<"],\"clear\":[";
  // The explicit clear ignores mProtected, reads next before native destruction and never clears messages.
  at<unsigned char>(p,0x3A)=1;*(int*)0x6B4CA8=fields[13];
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int,int,int,int,unsigned,void*)>(0x40D650)(storage[1],1,2,3,4,5,(void*)((unsigned char*)p+0x34));
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int,int,int,int,unsigned,void*)>(0x40D650)(storage[2],6,7,8,9,10,(void*)((unsigned char*)p+0x34));
  unsigned short clear[]={219,65535};reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(clear,65535,0);
  for(unsigned i=0;i<deleted.size();i++){if(i)std::cout<<',';std::cout<<deleted[i];}
  std::cout<<"],\"remaining\":"<<(*(unsigned*)0x56E9DC!=0)<<",\"bitCount\":"<<*(int*)0x56EB48<<"}\n";
 }}
'''
    cases = []
    for value in range(256):
        cases.append([64, 30, 999, 36, 125, value | 0xAB00, ((value + 85) & 255) | 0xCD00,
                      ((value + 170) & 255) | 0xEF00, 256, 153, 100, 0, 102, 140])
    for age in [-32768, -1, 0, 1, 15, 16, 17, 109, 110, 111, 124, 125, 126, 32767]:
        for life in [0, 1, 15, 16, 17, 125, 32767, 32768, 65535]:
            cases.append([256, 80, 10000, 36, life, 120, 20, 255, 256, 153, 100, age, 102, 140])
    rng = random.Random(0x40D770)
    for _ in range(360):
        timer = rng.choice([0, 1, 100, -1, -2147483648, 2147483647])
        cases.append([*[rng.randrange(65536) for _ in range(8)],
                      rng.choice([0, 1, 256, 0x80000000, 0xFFFFFFFF]),
                      rng.choice([0, 1, 153, -1, -2147483648, 2147483647]), timer,
                      rng.choice([-32768, -1, 0, 15, 16, 125, 32767]),
                      rng.choice([timer, 0, 2, -1, 32768, 2147483647]),
                      rng.choice([0, 4, 140, -32768, -2147483648, 2147483647])])
    with tempfile.TemporaryDirectory(prefix='openspidey-textbox-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    fixtures = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-textbox.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} original208/219 sequences: signed operands, font-width globals, RGB low bytes, real constructor/60Hz Move, signed16 grow quads and unconditional source head clear.')


if __name__ == '__main__':
    main()
