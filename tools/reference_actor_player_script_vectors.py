"""Capture whole original470D/470E/42B9 dispatch and4BC1A0 script switching with observable native ownership transports."""
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
#include <string>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
void patch(unsigned address,void*function){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)function-address-5;}
unsigned char *actor,*player,*origin;unsigned short *script;unsigned char switchBytes[5],ignoreBytes[5];
unsigned char dome[32],held[32];int entry,mutation;unsigned actionMutation;int animation=-1,firstFrame=-2,lastFrame=-2;
bool hasDome;std::vector<std::string> calls;
void*recover(void*){calls.push_back("recover");return hasDome?dome:nullptr;}
unsigned long long make(void*){calls.push_back("clear-dome");return 0;}
void*__attribute__((thiscall)) destroy(void*object,unsigned flags){assert(flags==1);calls.push_back(object==dome?"destroy-dome":"destroy-held");if(object==held&&actionMutation!=0xFFFFFFFF)at<unsigned>(player,0xE1C)=actionMutation;return object;}
void __attribute__((thiscall)) run(void*,int clip,int from,int to){calls.push_back("animation");animation=clip;firstFrame=from;lastFrame=to;}
void __attribute__((thiscall)) exitLookaround(void*object){calls.push_back("lookaround");at<unsigned char>(object,0x8EA)=0;}
unsigned short*__attribute__((thiscall)) switchInput(void*object,unsigned short*input){
 entry=at<unsigned char*>(actor,0x24C)-origin;
 std::memcpy((void*)0x4BC1A0,switchBytes,5);
 auto*end=reinterpret_cast<unsigned short*(__attribute__((thiscall)) *)(void*,unsigned short*)>(0x4BC1A0)(object,input);
 patch(0x4BC1A0,(void*)&switchInput);at<unsigned char*>(actor,0x24C)+=mutation;return end;
}
void __attribute__((thiscall)) ignoreInput(void*object,int frames){
 entry=at<unsigned char*>(actor,0x24C)-origin;
 std::memcpy((void*)0x4C30A0,ignoreBytes,5);
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int)>(0x4C30A0)(object,frames);
 patch(0x4C30A0,(void*)&ignoreInput);at<unsigned char*>(actor,0x24C)+=mutation;
}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only Windows SEH registration bookkeeping is omitted. Whole actor dispatch, ignore-input and script-switch instructions remain original.
 for(unsigned start:{0x4050B0u,0x407A50u}){std::memset((void*)start,0x90,6);at<unsigned short>((void*)start,0)=0xC031;}
 for(unsigned address:{0x4050BEu,0x406ACBu,0x407A5Eu,0x407E21u,0x407E77u})std::memset((void*)address,0x90,7);
 patch(0x4583F0,(void*)&recover);patch(0x458360,(void*)&make);patch(0x460C10,(void*)&run);patch(0x4C3810,(void*)&exitLookaround);
 std::memcpy(switchBytes,(void*)0x4BC1A0,5);patch(0x4BC1A0,(void*)&switchInput);
 std::memcpy(ignoreBytes,(void*)0x4C30A0,5);patch(0x4C30A0,(void*)&ignoreInput);
 long long v[14];while(std::cin>>v[0]){for(int i=1;i<14;i++)std::cin>>v[i];
  alignas(4)unsigned char a[0x330]={},p[0xF20],trail0[0x100]={},trail1[0x100]={},bytes[128]={};actor=a;player=p;origin=bytes;
  int opcode=v[0],position=v[2],seed=v[9];mutation=v[3];hasDome=v[5];actionMutation=v[13];
  for(unsigned i=0;i<sizeof p;i++)p[i]=(i*37+seed)&255;
  void*table[1]={(void*)&destroy};at<void*>(dome,0)=table;at<void*>(held,0)=table;
  at<int>(p,0x60)=seed+11;at<int>(p,0x64)=seed-27;at<int>(p,0x68)=-seed;
  at<unsigned char>(p,0x1AC)=seed&255;at<unsigned char>(p,0x1B4)=(seed+3)&255;
  at<unsigned char>(p,0xAE5)=seed&255;at<unsigned char>(p,0x1A4)=(seed+1)&255;
  at<int>(p,0x1A8)=seed*31;at<int>(p,0x1B0)=seed*17;at<void*>(p,0x1B8)=nullptr;
  at<void*>(p,0x584)=(v[4]&1)?trail0:nullptr;at<void*>(p,0x588)=(v[4]&2)?trail1:nullptr;
  at<void*>(p,0xAB8)=v[5]?dome:nullptr;at<unsigned>(p,0xABC)=0x12345678;at<void*>(p,0xE48)=v[6]?held:nullptr;
  at<unsigned>(p,0xE1C)=v[7];at<unsigned short>(p,0x14C)=v[10];at<unsigned char>(p,0x8EA)=v[11];
  at<int>(p,0xE18)=-123;at<short>(p,0xE12)=-234;at<void*>(p,0x350)=nullptr;
  int s0[]={int(0xABCD0012),int(0x12345678),-1},s21[]={int(0xABCD0034),int(0x98765432),-1};
  *(void**)0x6A82B8=v[8]?s0:nullptr;*(void**)0x6A830C=v[8]?s21:nullptr;
  *(void**)0x6A9038=p;*(int*)0x660F80=-444;*(int*)0x60F76C=-555;
  unsigned short shortProgram[]={0,9,1,0,255},authored[]={0,16,124,850,1,134,1,3,7,100,100,9,1,0,255};
  script=(unsigned short*)(bytes+position);if(opcode==0x470E)std::memcpy(script,v[12]?authored:shortProgram,v[12]?sizeof authored:sizeof shortProgram);else script[0]=v[1];
  at<void*>(a,0x24C)=script;entry=-1;animation=-1;firstFrame=lastFrame=-2;calls.clear();
  unsigned char before[sizeof p];std::memcpy(before,p,sizeof p);
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x407A50)(a,opcode);
  auto ignored=[&](unsigned i){if(opcode==0x42B9)return false;if(opcode==0x470D)return(i>=0xE18&&i<0xE1C)||(v[1]&&i>=0xE12&&i<0xE14)||(v[1]&&v[11]&&i==0x8EA);
   return(i>=0x60&&i<0x6C)||i==0x1AC||i==0x1B4||(i>=0x1B8&&i<0x1BC)||i==0xAE5||i==0x1A4||(i>=0x1A8&&i<0x1AC)||(i>=0x584&&i<0x58C)||(v[5]&&i>=0xAB8&&i<0xAC0)||(v[6]&&i>=0xE48&&i<0xE4C)||(v[6]&&i>=0x350&&i<0x354)||(v[6]&&actionMutation!=0xFFFFFFFF&&i>=0xE1C&&i<0xE20)||(i>=0x1C0&&i<0x300&&((i-0x1C0)%16)<3);};
  bool preserved=true;for(unsigned i=0;i<sizeof p;i++)if(!ignored(i)&&p[i]!=before[i])preserved=false;
  std::cout<<"{\"result\":"<<result<<",\"entry\":"<<entry<<",\"cursor\":"<<(at<unsigned char*>(a,0x24C)-bytes)<<",\"preserved\":"<<(preserved?"true":"false")<<",\"state\":[";
  std::cout<<at<int>(p,0x60)<<','<<at<int>(p,0x64)<<','<<at<int>(p,0x68)<<','<<unsigned(at<unsigned char>(p,0x1AC))<<','<<unsigned(at<unsigned char>(p,0x1B4))<<','<<unsigned(at<unsigned char>(p,0xAE5))<<','<<unsigned(at<unsigned char>(p,0x1A4))<<','<<at<int>(p,0x1A8)<<','<<at<int>(p,0x1B0)<<','<<(at<void*>(p,0x584)!=nullptr)<<','<<(at<void*>(p,0x588)!=nullptr)<<','<<at<int>(trail0,0x54)<<','<<at<int>(trail1,0x54)<<','<<(at<void*>(p,0xAB8)!=nullptr)<<','<<at<unsigned>(p,0xABC)<<','<<(at<void*>(p,0xE48)!=nullptr)<<','<<at<unsigned>(p,0xE1C)<<','<<animation<<','<<firstFrame<<','<<lastFrame<<','<<at<int>(p,0xE18)<<','<<at<short>(p,0xE12)<<','<<unsigned(at<unsigned char>(p,0x8EA))<<','<<*(int*)0x660F80<<','<<*(int*)0x60F76C<<"],\"pad\":[";
  for(int i=0;i<20;i++){if(i)std::cout<<',';std::cout<<'['<<unsigned(p[0x1C0+i*16])<<','<<unsigned(p[0x1C1+i*16])<<','<<unsigned(p[0x1C2+i*16])<<']';}
  std::cout<<"],\"sounds\":["<<(unsigned)s0[0]<<','<<(unsigned)s0[1]<<','<<(unsigned)s21[0]<<','<<(unsigned)s21[1]<<"],\"calls\":[";
  for(unsigned i=0;i<calls.size();i++){if(i)std::cout<<',';std::cout<<'"'<<calls[i]<<'"';}std::cout<<"]}\n";
 }}
'''
    cases = []
    for opcode in [0x470D, 0x470E, 0x42B9]:
        for value in [0, 1, 30, 8000, 32768, 65535]:
            for presence in range(8):
                cases.append([opcode, value, 6, 2 if presence & 1 else 0, presence & 3,
                              (presence >> 1) & 1, (presence >> 2) & 1, 0x10 if presence & 1 else 1,
                              presence & 1, 77 + presence, 12345, presence & 1, presence & 1, 0xFFFFFFFF])
    rng = random.Random(0x470E)
    for _ in range(120):
        cases.append([0x470E, 0, rng.choice([0, 2, 6]), rng.choice([0, 2, 4]), rng.randrange(4),
                      rng.randrange(2), rng.randrange(2), rng.randrange(2**32), rng.randrange(2),
                      rng.randrange(256), rng.randrange(65536), rng.randrange(2), rng.randrange(2),
                      rng.choice([0xFFFFFFFF, 0, 16, 0x80000010])])
    with tempfile.TemporaryDirectory(prefix='openspidey-player-script-switch-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    path = root / 'modern/OpenSpidey.Tests/Fixtures/actor-player-scripts.json'
    rows = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)]
    assert all(row['expected']['preserved'] for row in rows)
    path.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    print(f'Captured {len(rows)} whole original470D/470E/42B9 commands, true4BC1A0 pointer/pad/state writes, native ownership callback order and cursor commits.')


if __name__ == '__main__':
    main()
