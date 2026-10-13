"""Capture whole original CBaddy429C, real Flash_Screen/Update and callback-before-cursor-commit ordering."""
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
const unsigned fields[]={0x5FAA64,0x5FAA5C,0x5FAA60,0x5FAA54,0x5FAA58,0x5FAA14,0x5FAA00,0x5FAA04,0x5FA9FC,0x5FA9F8,0x5FA9F4};
unsigned char savedScreen[5],*caller,*origin;int entry,mutation;unsigned observed[6];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void screen(unsigned char red,unsigned char green,unsigned char blue,int frames,unsigned char importance,int sort){
 entry=at<unsigned char*>(caller,0x24C)-origin;observed[0]=red;observed[1]=green;observed[2]=blue;observed[3]=frames;observed[4]=importance;observed[5]=sort;
 // Instrument transport while executing the entire genuine Screen callee unchanged.
 std::memcpy((void*)0x43D830,savedScreen,5);
 reinterpret_cast<void(*)(unsigned char,unsigned char,unsigned char,int,unsigned char,int)>(0x43D830)(red,green,blue,frames,importance,sort);
 patch(0x43D830,(void*)&screen);
 at<unsigned char*>(caller,0x24C)+=mutation;
}
void snapshot(){std::cout<<'[';for(unsigned i=0;i<11;i++){if(i)std::cout<<',';if(i==3)std::cout<<unsigned(*(unsigned char*)fields[i]);else if(i<5)std::cout<<*(int*)fields[i];else std::cout<<*(unsigned*)fields[i];}std::cout<<','<<(*(int*)0x5FAA64==0)<<','<<(*(int*)0x5FAA5C!=0||*(int*)0x5FAA60!=0);for(unsigned a:{0x5FAA14u,0x5FAA00u,0x5FAA04u})std::cout<<','<<unsigned((unsigned char)(*(unsigned*)a>>16));std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only Windows SEH setup/restore is omitted; the whole command and both effects remain original.
 std::memset((void*)0x4050B0,0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 for(unsigned a:{0x4050BEu,0x406ACBu})std::memset((void*)a,0x90,7);
 std::memcpy(savedScreen,(void*)0x43D830,5);patch(0x43D830,(void*)&screen);
 long long input[20];while(std::cin>>input[0]){for(int i=1;i<20;i++)std::cin>>input[i];
  alignas(4)unsigned char body[0x300]={};alignas(4)unsigned char bytes[64]={};caller=body;origin=bytes;int position=input[0];
  for(int i=0;i<5;i++)at<unsigned short>(origin,position+2*i)=input[1+i];
  for(int i=0;i<11;i++){if(i==3)*(unsigned char*)fields[i]=input[6+i];else *(unsigned*)fields[i]=input[6+i];}
  at<unsigned char*>(body,0x24C)=origin+position;entry=-1;mutation=input[18];
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(body,0x429C);
  std::cout<<"{\"transport\":["<<result<<','<<entry<<','<<(at<unsigned char*>(body,0x24C)-origin);for(auto v:observed)std::cout<<','<<v;
  std::cout<<"],\"states\":[";snapshot();for(int i=0;i<input[17];i++){reinterpret_cast<void(*)()>(0x43D8C0)();std::cout<<',';snapshot();}std::cout<<"]}\n";
 }}
'''
    cases = []
    base = [3, 7, 0, 0, -123, 0x12345678, 0xABCDEF00, 0xFEDCBA98, 123, 456, 789]
    for rgb in [(120, 20, 20), (120, 120, 120), (20, 20, 120), (20, 120, 20), (0xFFFF, 0xA001, 0x2002)]:
        for frames in [0, 1, 2, 255, 256, 32768, 65535]:
            for importance in [0, 1, 255]:
                for sort in [0, 1, 65535]:
                    initial = base.copy()
                    initial[3] = importance
                    cases.append([6, *rgb, frames, sort, *initial, 3, 0, 0])
    rng = random.Random(0x406426)
    for _ in range(320):
        initial = [rng.choice([0, 1, 2, -1]), rng.choice([0, 1, -1, 65535]), rng.choice([0, 1]),
                   rng.choice([0, 0, 0, 1, 255]), rng.randint(-2147483648, 2147483647),
                   *[rng.randrange(2**32) for _ in range(6)]]
        cases.append([rng.choice([0, 2, 6]), *[rng.randrange(65536) for _ in range(5)], *initial,
                      rng.randrange(5), rng.choice([0, 0, 2, 4]), 0])
    assert all(len(row) == 20 for row in cases)
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-screen-flash-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    path = root / 'modern/OpenSpidey.Tests/Fixtures/actor-screen-flash.json'
    path.write_text(json.dumps([dict(input=row, expected=json.loads(line))
        for row, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original429C/Flash_Screen sequences, including RGB truncation, unsigned16 duration/sort, priority rejection, original update math and re-read cursor ordering.')


if __name__ == '__main__':
    main()
