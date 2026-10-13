"""Compile common CBaddy::Die on typed hostage fields; compare original PC stage state writes."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include <iostream>
#include <vector>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
std::vector<int>events;
struct CBaddy{unsigned mCBodyFlags=0,field_2A8=0,mRGB=0;unsigned short mFlags=0;int mTRN=0,field_1F8=0;int Die(int);
 bool IsDead(){return (mCBodyFlags&0x40)!=0;}void SendDeathPulse(){events.push_back(1);}void KillShadow(){mCBodyFlags&=~8u;events.push_back(2);}};
void print_if_false(int,const char*){}
'''
    code+=controller_method((root/'src/game/actors/baddy.cpp').read_text(),'int CBaddy::Die(')+'\n'
    code+=r'''
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);
#endif
unsigned body,behavior,tint;int item,transparency,count;while(std::cin>>body>>behavior>>item>>transparency>>tint>>count){CBaddy actor{body,behavior,tint,unsigned short(item),transparency,count};
#ifdef VALIDATE_ORIGINAL
alignas(4)unsigned char raw[0x300]={};*reinterpret_cast<unsigned short*>(raw+0x46)=body;*reinterpret_cast<unsigned*>(raw+0x2A8)=behavior;*reinterpret_cast<unsigned short*>(raw+4)=item;
*reinterpret_cast<unsigned*>(raw+0x24)=tint;*reinterpret_cast<unsigned short*>(raw+0x30)=transparency;*reinterpret_cast<int*>(raw+0x1F8)=count;*reinterpret_cast<unsigned short*>(raw+0xDE)=65535;
#endif
std::cout<<'[';for(int frame=0;frame<44;frame++){int stage;std::cin>>stage;events.clear();int result=actor.Die(stage);
#ifdef VALIDATE_ORIGINAL
int original=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,int)>(0x404320)(raw,stage);assert(original==result&&*reinterpret_cast<unsigned short*>(raw+0x46)==actor.mCBodyFlags&&*reinterpret_cast<unsigned short*>(raw+4)==actor.mFlags&&*reinterpret_cast<unsigned*>(raw+0x24)==actor.mRGB&&*reinterpret_cast<unsigned short*>(raw+0x30)==actor.mTRN&&*reinterpret_cast<int*>(raw+0x1F8)==actor.field_1F8);
#endif
if(frame)std::cout<<',';std::cout<<'['<<result<<','<<actor.mCBodyFlags<<','<<actor.mFlags<<','<<actor.mTRN<<','<<actor.mRGB<<','<<actor.field_1F8<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";}std::cout<<"]\n";}}
'''.replace('unsigned short(item)','static_cast<unsigned short>(item)')
    rng=random.Random(0x404320);cases=[]
    for _ in range(128):
        stages=[rng.choice([0,1,2,3]) for _ in range(44)]
        cases.append([rng.choice([0x20e,0x24e,65535]),rng.choice([0,0x4000,0xffffffff]),rng.randrange(65536),rng.randrange(256),rng.randrange(2**32),rng.choice([-2147483648,-1,0,39,40,2147483647]),*stages])
    cases.extend([[0x20e,0,0,0,0,0,1,*([2]*42),3],[0x20e,0x4000,0,0,0,0,1,*([2]*42),3]])
    with tempfile.TemporaryDirectory(prefix='openspidey-hostage-death-reference-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        flags=['-m32','-fno-pie','-no-pie','-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv',*flags,'-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        cmd=[str(binary)]
        if args.original_exe:cmd.append(str(args.original_exe.resolve()))
        lines=subprocess.check_output(cmd,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/hostage-death.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated{len(cases)} source hostage death sequences; all numeric stages match original PC when requested, pulse/shadow ownership explicit.')

if __name__=='__main__':main()
