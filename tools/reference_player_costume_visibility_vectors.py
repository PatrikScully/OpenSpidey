"""Capture the original PC SpideyAI0 prologue at 4B141E: suit-5 keyboard actions40/100 toggle player57C, then CItem flag800."""
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
std::vector<long long>events;unsigned char*player;int keyA,keyB,pressA,pressB;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void mapping(unsigned action,unsigned*key){events.insert(events.end(),{1,action});*key=action==0x40?keyA:action==0x100?keyB:0x4000;}
int pressed(unsigned key,int mode){events.insert(events.end(),{2,key&255,mode});return (key&255)==(unsigned)keyA?pressA:(key&255)==(unsigned)keyB?pressB:0;}
int sound(int id,void*position,int pitch){events.insert(events.end(),{3,id,static_cast<unsigned char*>(position)-player,pitch});return 0;}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void leafStep(void*){asm volatile("sub $0x4B0,%esp;mov $0x4B140B,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x50A400,(void*)&mapping);patch(0x50A650,(void*)&pressed);patch(0x471EA0,(void*)&sound);patch(0x4B14D1,(void*)&leafEnd);
long long v[10];while(std::cin>>v[0]){for(int i=1;i<10;i++)std::cin>>v[i];alignas(4)static unsigned char state[0xEC0],controls[0x200];std::memset(state,0,sizeof state);std::memset(controls,0x5A,sizeof controls);player=state;
*reinterpret_cast<int*>(0x5559DC)=v[0];pressA=v[1];pressB=v[2];player[0x1AC]=v[3];at<int>(player,0xE18)=v[4];player[0x57C]=v[5];at<unsigned short>(player,4)=v[6];keyA=v[7];keyB=v[8];at<int>(player,0x570)=v[9];
at<void*>(player,0xE0C)=controls;at<int>(player,8)=111;at<int>(player,12)=222;at<int>(player,16)=333;events.clear();leafStep(player);
std::cout<<'['<<unsigned(player[0x57C])<<','<<at<unsigned short>(player,4)<<','<<unsigned(controls[0x51])<<','<<at<int>(player,0x570)<<','<<unsigned(controls[0x50])<<','<<unsigned(controls[0x52])<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[[suit,a,b,synth,e18,armor,flags,0x24,0x10,208]
           for suit in [5,0,4,6,-1] for a in [0,1,7] for b in [0,1] for synth in [0,1,128] for e18 in [0,1,-1]
           for armor in [0,1,255] for flags in [0x0480,0x0800,0xFFFF]]
    cases+=[[5,1,1,0,0,armor,flags,key,key,brightness] for armor in [0,1] for flags in [0,0xF7FF] for key in [0,0x24] for brightness in [0,-1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-costume-visibility-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-costume-visibility.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original PC SpideyAI0 suit-5 costume switch and flag800 cases, including the shared-key and guard orders.')


if __name__=='__main__':main()
