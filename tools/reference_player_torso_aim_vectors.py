"""Capture complete original4B70D5..717F torso-aim slice, including branch4B71CC and exact source short/bool arguments."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) target(void*,short wanted,bool fast){events.insert(events.end(),{1,wanted,fast});}
void __attribute__((thiscall)) lock(void*){events.push_back(2);}
extern "C" __attribute__((naked)) void finish(){asm volatile("movswl %dx,%eax;pop %edi;pop %esi;pop %ebp;pop %ebx;ret");}
extern "C" __attribute__((naked)) short step(void*){asm volatile("push %ebx;push %ebp;push %esi;push %edi;mov 0x14(%esp),%esi;xor %ebp,%ebp;mov $0x4B70D5,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C6970,(void*)&target);patch(0x4C6960,(void*)&lock);patch(0x4B717F,(void*)&finish);long long v[8];while(std::cin>>v[0]){for(int i=1;i<8;i++)std::cin>>v[i];alignas(4)unsigned char player[0xF20]={},camera[0x2A4]={};at<unsigned>(player,0xE1C)=v[0];player[0xE2D]=v[1];player[0xE2E]=v[2];at<short>(player,0xE32)=v[3];at<short>(camera,0x23A)=v[4];player[0x8E8]=v[5];player[0x1AC]=v[6];player[0xAE6]=v[7];*reinterpret_cast<void**>(0x56F3B8)=camera;events.clear();step(player);std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";}}
'''
    cases=[[action,*axes,heading,camera,wall,script,locked]for action in [1,2,4,8,16,18,32,64,128,0x100,0x200,0x400,0x800000]
           for axes in [(0,0),(1,0),(0,-1)]for heading in [-32768,-1,0,256,257,3839,3840,32767]
           for camera in [-32768,0,32767]for wall in [0,1]for script in [0,1]for locked in [0,1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-torso-finalizer-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,row))for row in cases)+'\n').encode()).decode().splitlines()
    output=[dict(input=row,expected=json.loads(line))for row,line in zip(cases,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-torso-aim.json').write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original full torso-aim slices: exact source action16 priority, short wrapping/script fast flag, idle/airlock and independentAE6.')


if __name__=='__main__':main()
