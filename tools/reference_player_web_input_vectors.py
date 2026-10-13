"""Capture original PC CheckWebShot including directional selection and its exact torso/dome/zip boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
import shutil


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cstring>
std::vector<long long>events;int paid,allocated,targetPresent;alignas(4)unsigned char target[0x14],storage[0x11C];
void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
void patch(unsigned address,void*callee){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(callee)-address-5;}
void __attribute__((thiscall)) animation(void*,int clip,int first,int last){emit({1,clip,first,last});}
char __attribute__((thiscall)) decrease(void*,int amount){emit({2,amount});return paid;}
void* __attribute__((thiscall)) chooseTarget(void*,int range,int minimum,int maximum,int flags){emit({3,range,minimum,maximum,flags});return targetPresent?target:nullptr;}
void*allocate(unsigned){return allocated?storage:nullptr;}
void* __attribute__((thiscall)) dome(void*self,void*,int variant){emit({4,variant});return self;}
int sound(unsigned id,CVector*position,int pitch){emit({5,id,position->vx,position->vy,position->vz,pitch});return 0;}
void __attribute__((thiscall)) torso(void*,short wanted,bool use){emit({6,wanted,use});}
void __attribute__((thiscall)) point(void*,CVector*target){emit({7,target->vx,target->vy,target->vz});}
unsigned long long handle(void*pointer){emit({8,pointer?1:0});return reinterpret_cast<unsigned>(pointer)|(static_cast<unsigned long long>(pointer?555:0)<<32);}
short __attribute__((thiscall)) heading(void*){emit({9});return 1234;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x460C10,reinterpret_cast<void*>(&animation));patch(0x4BB0A0,reinterpret_cast<void*>(&decrease));patch(0x4C8410,reinterpret_cast<void*>(&chooseTarget));patch(0x455390,reinterpret_cast<void*>(&allocate));patch(0x4FA640,reinterpret_cast<void*>(&dome));patch(0x471EA0,reinterpret_cast<void*>(&sound));patch(0x4C6970,reinterpret_cast<void*>(&torso));patch(0x4C68A0,reinterpret_cast<void*>(&point));patch(0x458360,reinterpret_cast<void*>(&handle));patch(0x4C6AA0,reinterpret_cast<void*>(&heading));
for(auto entry:std::vector<std::pair<unsigned,int>>{{0x4C0510,6},{0x4C051E,7},{0x4C0895,7},{0x4C0997,7},{0x4C0A54,7},{0x4C0B55,7},{0x4C0B71,7}})std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
unsigned sounds[300]={};*reinterpret_cast<void**>(0x6A82B8)=sounds;*reinterpret_cast<int*>(0x6A7FFC)=123;*reinterpret_cast<CVector*>(target+8)=CVector(11,-12,13);
long long v[20];while(std::cin>>v[0]){for(int i=1;i<20;i++)std::cin>>v[i];alignas(4)unsigned char raw[0xEC0]={},input[0x134]={};
raw[0x8E0]=v[0];input[0x110]=v[1];*reinterpret_cast<void**>(raw+0xE48)=v[2]?target:nullptr;raw[0x8E1]=v[3];*reinterpret_cast<unsigned*>(raw+0x8E4)=v[4];
*reinterpret_cast<unsigned*>(raw+0xE1C)=v[5];raw[0xE2D]=v[6];raw[0xE2E]=v[7];*reinterpret_cast<int*>(raw+0xEBC)=v[8];raw[0xAD4]=v[9];raw[0x8EA]=v[10];raw[0x8E9]=v[11];
*reinterpret_cast<unsigned*>(raw+0x5B4)=unsigned(v[13])-unsigned(v[12]);*reinterpret_cast<unsigned*>(0x6B4CA8)=v[13];paid=v[14];allocated=v[15];targetPresent=v[16];raw[0x5E8]=v[17];
input[0x120]=v[18];input[0x130]=v[19];*reinterpret_cast<void**>(raw+0xE0C)=input;*reinterpret_cast<void**>(raw+0xDCC)=targetPresent?target:nullptr;
*reinterpret_cast<CVector*>(raw+8)=CVector(123456,-54321,76543);raw[0x8F8]=7;raw[0x552]=9;raw[0x8ED]=11;*reinterpret_cast<int*>(raw+0x544)=13;raw[0xAE]=255;*reinterpret_cast<int*>(raw+0xDF8)=77;events.clear();
int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(0x4C0510)(raw);
std::cout<<'['<<result<<','<<unsigned(raw[0x8E0])<<','<<unsigned(raw[0x8E1])<<','<<*reinterpret_cast<unsigned*>(raw+0x8E4)<<','<<unsigned(raw[0x8F8])<<','<<unsigned(raw[0x552])<<','<<unsigned(raw[0x8ED])<<','<<*reinterpret_cast<int*>(raw+0x544)<<','<<*reinterpret_cast<unsigned*>(raw+0xE1C)<<','<<*reinterpret_cast<int*>(raw+0xDF8)<<','<<unsigned(raw[0xAE])<<','<<*reinterpret_cast<int*>(raw+0x374)<<','<<(*reinterpret_cast<void**>(raw+0xAB8)?1:0)<<','<<*reinterpret_cast<int*>(raw+0xDE0)<<','<<(*reinterpret_cast<void**>(raw+0xDD8)?1:0)<<",[";
for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases=[];rng=random.Random(0x4C0510)
    for side,forward in [(0,0),(1,0),(-1,0),(0,-1),(0,1),(1,-1),(1,1),(-1,-1),(-1,1)]:
        for action in [1,2,4]:
            for ramp in [6,7,16]:
                for cooldown in [30,31]:
                    for crawling in [0,1]:
                        cases.append([0,1,0,0,0,action,side,forward,ramp,crawling,0,0,cooldown,1000,1,1,1,0,0,0])
    for _ in range(2500):
        cases.append([rng.choice([0,1,255]),rng.choice([0,1,255]),rng.randrange(2),rng.choice([0,1,255]),
            rng.choice([0,15,0xFFFF0000]),rng.choice([1,2,4,0x8000]),rng.choice([-128,-1,0,1,127]),rng.choice([-128,-1,0,1,127]),
            rng.choice([0,6,7,16]),rng.randrange(2),rng.choice([0,1,255]),rng.randrange(2),rng.choice([0,30,31,32,0xFFFFFFFF]),
            rng.choice([0,1000,0xFFFFFFFA]),rng.randrange(2),rng.randrange(2),rng.randrange(2),rng.choice([0,1,255]),rng.randrange(2),rng.randrange(2)])
    with tempfile.TemporaryDirectory(prefix='openspidey-player-web-input-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        try:
            lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,case)) for case in cases)+'\n').encode()).decode().splitlines()
        except subprocess.CalledProcessError:
            shutil.copy2(binary,'/tmp/openspidey-web-input-reference');shutil.copy2(cpp,'/tmp/openspidey-web-input-reference.cpp')
            Path('/tmp/openspidey-web-input-cases.txt').write_text('\n'.join(' '.join(map(str,case)) for case in cases)+'\n')
            raise
    (root/'modern/OpenSpidey.Tests/Fixtures/player-web-input.json').write_text(json.dumps([dict(input=case,expected=json.loads(line)) for case,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC CheckWebShot cases, including held object, fullDWORD latch masks, signed aim, cooldown wrapping, dome/zip and torso callback order.')


if __name__=='__main__':main()
