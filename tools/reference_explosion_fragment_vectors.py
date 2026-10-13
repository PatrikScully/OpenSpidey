"""Compile recovered CFrag and compare snapshots with original PC constructor/motion."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile

from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', required=True, type=Path)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    source = (root / 'src/engine/world/bit.cpp').read_text()
    constructor = controller_method(source, 'CFrag::CFrag(')
    move = controller_method(source, 'void CFrag::Move(')
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cassert>
unsigned seed;int frames;
int Rnd(int bound){seed=seed*1664525u+1013904223u;return bound==0?0:int(seed%unsigned(bound));}
struct CFlatBit {
 unsigned char raw[0x68]={};
 CVector& mPos=*reinterpret_cast<CVector*>(raw+0x10);
 CVector& mVel=*reinterpret_cast<CVector*>(raw+0x1C);
 CVector& mAcc=*reinterpret_cast<CVector*>(raw+0x28);
 CFriction& mFric=*reinterpret_cast<CFriction*>(raw+0x34);
 short& mAge=*reinterpret_cast<short*>(raw+0xC);
 unsigned short& mLifetime=*reinterpret_cast<unsigned short*>(raw+0xE);
 short& mScale=*reinterpret_cast<short*>(raw+0x56);
 unsigned char& mNumFrames=raw[0x51];signed char& mFrame=*reinterpret_cast<signed char*>(raw+0x52);
 CFlatBit(){*reinterpret_cast<unsigned*>(raw+0x40)=0x2C808080;*reinterpret_cast<unsigned*>(raw+0x60)=0x10001000;raw[0x65]=32;}
 void SetAnim(int){mNumFrames=frames;mFrame=0;}
 void SetSemiTransparent(){*reinterpret_cast<unsigned*>(raw+0x40)|=0x02000000;}
 void SetTint(unsigned char r,unsigned char g,unsigned char b){auto& color=*reinterpret_cast<unsigned*>(raw+0x40);color=(color&0xFF000000)|r|(g<<8)|(b<<16);}
 void Die(){raw[0x37]=1;}
};
struct CFrag:CFlatBit{CFrag(CVector*,unsigned char,unsigned char,unsigned char,int,unsigned short,int,int,int,int);void Move();};
'''
    code += constructor + '\n' + move + '\n'
    code += r'''
void patch(unsigned address,void* fn){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(fn)-address-5;}
void diagnostic(int,const char*){}
std::vector<long long> snapshot(unsigned char* p){std::vector<long long> result;
 for(int offset:{0x10,0x14,0x18,0x1C,0x20,0x24,0x28,0x2C,0x30})result.push_back(*reinterpret_cast<int*>(p+offset));
 result.push_back(*reinterpret_cast<short*>(p+0xC));result.push_back(*reinterpret_cast<unsigned short*>(p+0xE));
 result.push_back(*reinterpret_cast<short*>(p+0x56));result.push_back(*reinterpret_cast<signed char*>(p+0x52));result.push_back(p[0x51]);
 result.push_back(*reinterpret_cast<unsigned*>(p+0x40));result.push_back(*reinterpret_cast<unsigned*>(p+0x60));
 result.push_back(p[0x65]);result.push_back(*reinterpret_cast<unsigned short*>(p+0x38));result.push_back(p[0x37]==0);return result;}
void emit(const std::vector<long long>& values){std::cout<<'[';for(unsigned i=0;i<values.size();i++){if(i)std::cout<<',';std::cout<<values[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4E5DA0,reinterpret_cast<void*>(&Rnd));patch(0x4015B0,reinterpret_cast<void*>(&diagnostic));
 auto* p=reinterpret_cast<unsigned char*>(0x40FDA7);assert(p[0]==0x64);p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);
 for(unsigned address:{0x40FDAEu,0x40FF17u}){p=reinterpret_cast<unsigned char*>(address);assert(p[0]==0x64);std::memset(p,0x90,7);}
 alignas(4) unsigned char animation[32]={};
 for(unsigned index=0;index<29;index++)*reinterpret_cast<void**>(0x56EA64+index*4)=animation+8;
 CVector position;int r,g,b,anim,scale,semi,spread,gravity,lifetime,updates;unsigned initialSeed;
 while(std::cin>>position.vx>>position.vy>>position.vz>>r>>g>>b>>anim>>scale>>semi>>spread>>gravity>>lifetime>>frames>>initialSeed>>updates){
  animation[4]=frames;*reinterpret_cast<unsigned*>(0x56EA50)=0;
  seed=initialSeed;CFrag recovered(&position,r,g,b,anim,scale,semi,spread,gravity,lifetime);
  alignas(4) unsigned char pc[0x68]={};seed=initialSeed;
  reinterpret_cast<void*(__attribute__((thiscall))*)(void*,CVector*,unsigned char,unsigned char,unsigned char,int,unsigned short,int,int,int,int)>(0x40FDA0)(pc,&position,r,g,b,anim,scale,semi,spread,gravity,lifetime);
  void* originalFrame=*reinterpret_cast<void**>(pc+0x4C);
  std::cout<<'[';
  for(int update=0;update<=updates;update++){
   assert(snapshot(pc)==snapshot(recovered.raw));assert(*reinterpret_cast<void**>(pc+0x4C)==originalFrame);
   if(update)std::cout<<',';emit(snapshot(pc));
   if(pc[0x37] || update==updates)break;
   recovered.Move();reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x40FFE0)(pc);
  }
  std::cout<<"]\n";
 }
}
'''
    rng = random.Random(0x40FDA0)
    cases = []
    for frames in [1, 2, 8, 127, 128, 255]:
        for life in [0, 1, 30, 65535]:
            for spread in [0, 1, 80, 16384]:
                cases.append([2147483600, -2147483600, 0, 128, 64, 255, 6, 750, 1,
                              spread, 12288, life, frames, rng.getrandbits(32), 160])
    for _ in range(128):
        cases.append([*[rng.randint(-2147483648, 2147483647) for _ in range(3)],
                      *[rng.randrange(256) for _ in range(3)], rng.randrange(29), rng.randrange(65536),
                      rng.randrange(2), rng.randrange(512), rng.randint(-2147483648, 2147483647),
                      rng.choice([1, 30, 65535]), rng.choice([1, 8, 127, 128, 255]), rng.getrandbits(32), 160])
    with tempfile.TemporaryDirectory(prefix='openspidey-explosion-fragment-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fwrapv', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    rows = [dict(input=case, states=json.loads(result)) for case, result in zip(cases, output, strict=True)]
    target = root / 'modern/OpenSpidey.Tests/Fixtures/explosion-fragment.json'
    target.write_text(json.dumps(rows, separators=(',', ':')) + '\n')
    print(f'Generated {len(rows)} CFrag trajectories, {sum(len(case["states"]) for case in rows)} snapshots; actual PC constructor/motion and retained frame0 pointer match recovered source.')


if __name__ == '__main__':
    main()
