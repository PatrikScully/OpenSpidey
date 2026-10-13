"""Capture whole original CBaddy4298 and native CCamera::Shake profiles, including missing-camera variable reads."""
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
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
int reads;short __attribute__((thiscall)) variable(void*p,unsigned short word){reads++;return reinterpret_cast<short(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4072A0)(p,word);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
unsigned char shakeBytes[5];int calls,mode,observed[3];unsigned char*caller;unsigned short*origin;int entry;
void __attribute__((thiscall)) shake(void*camera,void*position,int magnitude){calls++;mode=magnitude;
 for(int i=0;i<3;i++)observed[i]=((int*)position)[i];entry=(unsigned char*)at<unsigned short*>(caller,0x24C)-(unsigned char*)origin;
 std::memcpy((void*)0x416880,shakeBytes,5);reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*,int)>(0x416880)(camera,position,magnitude);patch(0x416880,(void*)&shake);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Remove Windows SEH only; original GetVariable, argument mapping, presence guard and all Shake stores execute unchanged.
 std::memset((void*)0x4050B0,0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 for(unsigned a:{0x4050BEu,0x406ACBu})std::memset((void*)a,0x90,7);
 *(unsigned char*)0x4015B0=0xC3;std::memcpy(shakeBytes,(void*)0x416880,5);patch(0x416880,(void*)&shake);
 // Execute the genuine C++ static initializers for the source shake tables, absent from the mapped BSS.
 for(unsigned initializer:{0x415D20u,0x415D40u,0x415D60u,0x415D80u,0x415DA0u,0x415DC0u})reinterpret_cast<void(*)()>(initializer)();
 long long fields[9];while(std::cin>>fields[0]){for(int i=1;i<9;i++)std::cin>>fields[i];
  alignas(4)unsigned char body[0x330]={},camera[0x350]={};caller=body;void*table[17]={};table[16]=(void*)&variable;at<void*>(body,0)=table;
  at<unsigned short>(body,0xDE)=fields[1];*(unsigned short*)0x54D474=fields[1];
  for(int i=0;i<3;i++)at<int>(body,0x8+i*4)=fields[3+i];
  for(unsigned n:{0x270u,0x27Au})for(int i=0;i<3;i++)at<short>(camera,n+i*2)=fields[6+i];
  for(int i=0;i<3;i++)at<unsigned char>(camera,0x276+i)=fields[6+i];
  *(void**)0x56F3B8=fields[2]?camera:nullptr;unsigned short words[]={static_cast<unsigned short>(fields[0]),77};origin=words;at<unsigned short*>(body,0x24C)=words;
  reads=calls=0;mode=entry=-1;for(auto&v:observed)v=0;
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(body,0x4298);
  std::cout<<"{\"result\":"<<result<<",\"consumed\":"<<((unsigned char*)at<unsigned short*>(body,0x24C)-(unsigned char*)words)<<",\"reads\":"<<reads<<",\"calls\":"<<calls<<",\"mode\":"<<mode<<",\"entry\":"<<entry<<",\"position\":[";
  for(int i=0;i<3;i++){if(i)std::cout<<',';std::cout<<observed[i];}std::cout<<"],\"profile\":[";
  for(int i=0;i<3;i++){if(i)std::cout<<',';std::cout<<at<short>(camera,0x270+i*2);}for(int i=0;i<3;i++)std::cout<<','<<unsigned(at<unsigned char>(camera,0x276+i));for(int i=0;i<3;i++)std::cout<<','<<at<short>(camera,0x27A+i*2);std::cout<<"]}\n";
 }}
'''
    cases = []
    for present in [0, 1]:
        for operand, resolved in [(0, 0), (1, 1), (2, 2), (3, 3), (32768, 32768), (0x9FFF, 0x9FFF),
                                  (0x212A, 0), (0x212A, 1), (0x212A, 2), (0x212A, 65535), (0x2139, 32768)]:
            cases.append([operand, resolved, present, -1234567, 9876543, -7654321, -10, 20, -30])
    rng = random.Random(0x4298)
    for _ in range(300):
        operand = rng.choice([rng.randrange(8192), 0x212A, 0x2139, 0x8000, 0x9FFF])
        resolved = rng.randrange(65536)
        cases.append([operand, resolved, rng.randrange(2), *[rng.randint(-2147483648, 2147483647) for _ in range(3)],
                      *[rng.randint(-32768, 32767) for _ in range(3)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-camera-shake-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/actor-camera-shake.json').write_text(json.dumps(
        [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original4298 commands and full CCamera::Shake stores with source variable getters, absent-camera consumption, native position and callback cursor ordering.')


if __name__ == '__main__':
    main()
