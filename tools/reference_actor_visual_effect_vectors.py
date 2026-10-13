"""Capture whole-PC42A7/42A8 laser flash and42A6 wibbling constructor/RNG with genuine original effect bodies."""
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
#include <cstdlib>
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
std::vector<void*>allocations;std::vector<long long>rng,request;void*glow,*model;int accepted,allocs;
void*allocate(unsigned n){allocs++;if(n==276&&!accepted)return nullptr;void*p=std::calloc(1,n);assert(p);allocations.push_back(p);if(n==112)glow=p;if(n==276)model=p;return p;}
void*arrays(int n,int,int,int,int){void*p=std::calloc(1,n);assert(p);allocations.push_back(p);return p;}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
unsigned char rndBytes[5],modelBytes[5];unsigned char*caller;unsigned short*origin;int entry;
int next(int bound){std::memcpy((void*)0x4E5DA0,rndBytes,5);int result=reinterpret_cast<int(*)(int)>(0x4E5DA0)(bound);patch(0x4E5DA0,(void*)&next);rng.push_back(bound);rng.push_back(result);return result;}
void __attribute__((thiscall)) initItem(void*p,const char*name){assert(std::strcmp(name,"expgrnd")==0);at<unsigned char>(p,0x1F)=0;at<unsigned short>(p,0x1A)=0;}
void*__attribute__((thiscall)) wibbling(void*p,const int*pos,char*name,int a,int b,int c,int d,int e,int f,int g,int h,int i){
 assert(std::strcmp(name,"expgrnd")==0);request={pos[0],pos[1],pos[2],a,b,c,d,e,f,g,h,i};entry=(unsigned char*)at<unsigned short*>(caller,0x24C)-(unsigned char*)origin;
 std::memcpy((void*)0x43CEA0,modelBytes,5);void*result=reinterpret_cast<void*(__attribute__((thiscall)) *)(void*,const int*,char*,int,int,int,int,int,int,int,int,int)>(0x43CEA0)(p,pos,name,a,b,c,d,e,f,g,h,i);patch(0x43CEA0,(void*)&wibbling);return result;}
void omit(unsigned a,int n){std::memset((void*)a,0x90,n);}
void sequence(const std::vector<long long>&values){std::cout<<'[';for(unsigned i=0;i<values.size();i++){if(i)std::cout<<',';std::cout<<values[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only Windows exception registration and heap/asset-lookup transport are replaced. Command,Glow,CBody,C3D and derived constructor arithmetic run original code.
 omit(0x4050B0,6);*(unsigned short*)0x4050B0=0xC031;for(unsigned a:{0x4050BEu,0x406ACBu})omit(a,7);
 for(unsigned a:{0x43B417u,0x43BEE7u,0x40C217u,0x43CBF7u}){omit(a,6);*(unsigned short*)a=0xC031;}
 for(unsigned a:{0x43B41Eu,0x43B521u,0x43B541u,0x43BEEEu,0x43BFFEu,0x40C21Eu,0x40C31Du,0x43CBFEu,0x43CCFEu})omit(a,7);
 *(unsigned char*)0x4015B0=0xC3;patch(0x4088A0,(void*)&allocate);patch(0x455390,(void*)&allocate);patch(0x4581A0,(void*)&arrays);patch(0x460020,(void*)&initItem);
 std::memcpy(rndBytes,(void*)0x4E5DA0,5);patch(0x4E5DA0,(void*)&next);std::memcpy(modelBytes,(void*)0x43CEA0,5);patch(0x43CEA0,(void*)&wibbling);
 int opcode,px,py,pz,seed,multiplier,increment,n;
 while(std::cin>>opcode>>px>>py>>pz>>seed>>multiplier>>increment>>accepted>>n){
  unsigned short words[16]={};for(int i=0;i<n;i++){unsigned x;std::cin>>x;words[i]=x;}
  alignas(4)unsigned char body[0x330]={};caller=body;origin=words;for(int i=0;i<3;i++)at<int>(body,8+i*4)=i==0?px:i==1?py:pz;at<unsigned short*>(body,0x24C)=words;
  glow=model=nullptr;allocs=0;entry=-1;rng.clear();request.clear();*(unsigned*)0x56E9E4=0;*(unsigned*)0x56EFE4=0;*(int*)0x56EB48=0;*(int*)0x5FA954=7;
  *(int*)0x6B4C7C=seed;*(int*)0x6B4B90=multiplier;*(int*)0x6B4B94=increment;
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(body,opcode);
  std::cout<<"{\"result\":"<<result<<",\"consumed\":"<<((unsigned char*)at<unsigned short*>(body,0x24C)-(unsigned char*)words)<<",\"allocs\":"<<allocs<<",\"entry\":"<<entry<<",\"random\":";sequence(rng);
  std::cout<<",\"randomState\":["<<*(int*)0x6B4C7C<<','<<*(int*)0x6B4B90<<','<<*(int*)0x6B4B94<<"],\"request\":";sequence(request);
  std::cout<<",\"count\":"<<*(int*)0x5FA954<<",\"model\":[";
  if(model&&accepted){std::vector<long long>state={at<int>(model,8),at<int>(model,12),at<int>(model,16),at<short>(model,0x28),at<short>(model,0x2A),at<short>(model,0x2C),at<int>(model,0x64),at<unsigned>(model,0x24),at<unsigned short>(model,4),at<unsigned char>(model,0xF8),at<int>(model,0xFC),at<int>(model,0x100),at<unsigned short>(model,0x1A)};for(unsigned i=0;i<state.size();i++){if(i)std::cout<<',';std::cout<<state[i];}}
  std::cout<<"],\"glow\":[";
  if(glow){std::vector<long long>state={at<int>(glow,0x10),at<int>(glow,0x14),at<int>(glow,0x18),at<unsigned>(glow,0x44),at<unsigned>(glow,0x48),at<unsigned>(glow,0x4C),at<short>(glow,0x50),at<unsigned char>(glow,0x52),at<unsigned short>(glow,0x54),at<unsigned>(glow,0x58),at<short>(glow,0xC),at<unsigned short>(glow,0xE)};
   for(unsigned a:{0x5Cu,0x5Eu,0x60u,0x62u,0x64u,0x66u,0x68u,0x6Au})state.push_back(at<short>(glow,a));state.push_back(at<unsigned char>(glow,0x6C));
   unsigned*sections=at<unsigned*>(glow,0x3C),*fringes=at<unsigned*>(glow,0x40);for(int i=0;i<32;i++)state.push_back(sections[i]);for(int i=0;i<32;i++)state.push_back(fringes[i]);for(unsigned i=0;i<state.size();i++){if(i)std::cout<<',';std::cout<<state[i];}}
  std::cout<<"]}\n";for(void*p:allocations)std::free(p);allocations.clear();
 }}
'''
    cases = []
    for opcode in [0x42A7, 0x42A8]:
        for size in [0, 1, 255, 256, 512, 32768, 65535]:
            for half in [0, 1, 65535]:
                cases.append(dict(opcode=opcode, position=[12345678, -8765432, 7654321], random=[1, 2, 3], accepted=1,
                                  words=[size, half, 0xFFFF, 123, 0x8000, 2]))
    for effect in [0, 1, 4, 65535]:
        cases.append(dict(opcode=0x42A6, position=[0, 0, 0], random=[1, 2, 3], accepted=1, words=[effect, 0xABCD]))
    rng = random.Random(0x42A6)
    for _ in range(240):
        opcode = rng.choice([0x42A6, 0x42A7, 0x42A8])
        words = [3, *[rng.randrange(65536) for _ in range(10)]] if opcode == 0x42A6 else [rng.randrange(65536) for _ in range(6)]
        cases.append(dict(opcode=opcode, position=[rng.randint(-2147483648, 2147483647) for _ in range(3)],
                          random=[rng.randint(-2147483648, 2147483647) for _ in range(3)], accepted=rng.randrange(2) if opcode == 0x42A6 else 1, words=words))
    # Each actual L1A4 fire model receives the same nine raw sub3 tuples.
    tuples = [(2048,1750,0,15,2,2,5),(2048,1750,1,27,10,10,5),(2048,1500,1,20,10,10,5),
              (3500,100,1,7,5,1,40),(2500,100,1,7,10,5,50),(2000,100,1,9,15,5,55),
              (2250,100,1,8,20,5,60),(2500,150,1,7,25,5,65),(3000,200,1,7,30,2,50)]
    for values in tuples:
        start, step, growth, fade, speed, speed_range, spread = values
        for accepted in [0, 1]:
            cases.append(dict(opcode=0x42A6, position=[-22578 << 12, -4272 << 12, 2544 << 12], random=[1, 2, 3],
                              accepted=accepted, words=[3,1,0,start,step,256,growth,fade,speed,speed_range,spread]))
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-visual-effects-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [[case['opcode'], *case['position'], *case['random'], case['accepted'], len(case['words']), *case['words']] for case in cases]
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/actor-visual-effects.json').write_text(json.dumps(
        [dict(**case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original actor laser/wibbling effects: exact raw operands, RNG and allocation order, real CGlowFlash/CBody/C3D/derived counter stores.')


if __name__ == '__main__':
    main()
