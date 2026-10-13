"""Capture complete original PC CheckForwards4BF8A0 at actual heading, torso and animation boundaries."""
import argparse
import json
from pathlib import Path
import random
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
std::vector<long long>events;unsigned short effective;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
short __attribute__((thiscall)) heading(void*){return effective;}
void __attribute__((thiscall)) torso(void*,short wanted,bool fast){assert(fast);events.insert(events.end(),{1,wanted,fast});}
void __attribute__((thiscall)) run(void*p,int clip,int first,int last){events.insert(events.end(),{2,clip,first,last});at<unsigned short>(p,0x12A)=clip;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C6AA0,(void*)&heading);patch(0x4C6970,(void*)&torso);patch(0x460C10,(void*)&run);memset(reinterpret_cast<void*>(0x6A82B8),0,300*4);
long long v[17];while(std::cin>>v[0]){for(int i=1;i<17;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},controls[0x200]={},camera[0x2A4]={};at<void*>(player,0xE0C)=controls;*reinterpret_cast<void**>(0x56F3B8)=camera;controls[0x40]=v[0];player[0x8EA]=v[1];player[0xE2D]=v[2];player[0xE2E]=v[3];at<unsigned short>(player,0x12A)=v[4];player[0x8E8]=v[5];at<short>(player,0xE32)=v[6];at<short>(camera,0x23A)=v[7];effective=v[8];at<unsigned short>(player,0xDF0)=v[9];at<int>(player,0xDF8)=v[10];player[0xAD4]=v[11];player[0xAD5]=v[12];player[0x551]=v[14];player[0xAD7]=v[15];at<unsigned>(player,0xE1C)=v[16];at<short>(player,0xEA6)=456;at<void*>(player,0x350)=reinterpret_cast<void*>(1);events.clear();int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,bool)>(0x4BF8A0)(player,v[13]!=0);
std::cout<<'['<<result<<','<<at<short>(player,0xEA6)<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<at<int>(player,0xDF8)<<','<<unsigned(player[0x551])<<','<<unsigned(player[0xAD7])<<','<<(at<void*>(player,0x350)?1:0)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[[target,active,axis1,axis2,clip,surface,wanted,0,0,turnTarget,remaining,crawl,hard,allow,77,88,action]
           for target,active,axis1,axis2,action in [(0,0,1,0,8),(1,0,1,0,1),(1,0,1,0,8),(0,1,1,0,8),(0,0,0,0,8),(0,0,0,-127,8)]
           for clip in [0,11,13,123,194,200,234,227,281]
           for surface,wanted,turnTarget,remaining,crawl,hard,allow in [(0,0,0,0,0,0,1),(0,127,0,0,0,0,1),(0,128,128,1,0,0,1),
                (0,2048,0,0,0,0,1),(0,3968,4032,1,0,0,1),(0,3969,0,1,0,0,0),(0,0,0,0,1,0,1),(0,0,0,0,1,1,1),
                (1,-32768,1,0,0,0,1),(1,32767,0,1,0,0,1)]]
    rng=random.Random(0x4BF8A0)
    cases += [[rng.randrange(2),rng.randrange(2),rng.randint(-128,127),rng.randint(-128,127),rng.choice([0,12,123,194,200,234,227,281]),rng.randrange(2),
               rng.randint(-32768,32767),rng.randint(-32768,32767),rng.randrange(65536),rng.randrange(65536),rng.randint(-20,20),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.randrange(256),rng.randrange(256),rng.choice([1,8,16,0x2000000])] for _ in range(256)]
    with tempfile.TemporaryDirectory(prefix='openspidey-forward-selection-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-forward-selection.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} complete original PC CheckForwards gates, source roll/torso priority, exact held-object/crawl start clips and flags.')


if __name__=='__main__':main()
