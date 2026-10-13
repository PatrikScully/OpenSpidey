"""Capture whole original4477F0 plus genuine Panel_DrawTexturedPoly/SetStretchedScreenCoords, at speech/texture/graphics boundaries."""
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
#include "engine/rendering/texture.h"
#include <array>
#include <iostream>
#include <vector>
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
std::vector<long long>events;std::array<Texture,5>textures;std::array<unsigned char,4096>polys;unsigned available;int speechMutation,playCount,currentTexture;
const char*names[]={"scorpion_approach","jonah_approach","scorpion_approach_bar","scorpion_approach_bar_leftend","scorpion_approach_bar_rightend"};
Texture*find(char*name){for(int i=0;i<5;i++)if(std::strcmp(name,names[i])==0){events.insert(events.end(),{1,i});return available&(1u<<i)?&textures[i]:nullptr;}assert(false);return nullptr;}
void play(int bank,int slot,int priority){assert(bank==8&&priority==0);events.insert(events.end(),{2,bank,slot,priority});for(unsigned a:{0x5FB480u,0x5FB488u})for(int i=0;i<5;i++)events.push_back(*(unsigned char*)(a+i));if(playCount++==0){if(speechMutation==1)*(unsigned*)0x60F774=0;else if(speechMutation==2){*(unsigned*)0x60F774=0xFFFFFFFF;*(unsigned char*)0x550D81=0;}else if(speechMutation==3){*(unsigned char*)0x60CFC5=1;*(unsigned char*)0x550D81=1;}}}
void texture(int clut,int blend){assert(blend==1);currentTexture=clut-10;events.insert(events.end(),{3,currentTexture,blend});}
void quad(float x0,float y0,float u0,float v0,unsigned c0,float x1,float y1,float u1,float v1,unsigned c1,float x2,float y2,float u2,float v2,unsigned c2,float x3,float y3,float u3,float v3,unsigned c3,float depth){assert(u0==0&&v0==0&&u1==1&&v1==0&&u2==0&&v2==1&&u3==1&&v3==1);assert(x0==x2&&x1==x3&&y0==y1&&y2==y3&&c0==c1&&c0==c2&&c0==c3);events.insert(events.end(),{4,currentTexture,int(x0),int(y0),int(x1),int(y2),c0,int(depth)});}
void stub(){}
void diagnostic(unsigned char condition,const char*){assert(condition!=0);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C94B0,(void*)&find);patch(0x479EE0,(void*)&play);patch(0x506440,(void*)&texture);patch(0x507910,(void*)&quad);patch(0x46CB90,(void*)&stub);patch(0x4015B0,(void*)&diagnostic);
for(int i=0;i<5;i++){textures[i].clut=i+10;textures[i].u1=textures[i].u3=31;textures[i].v2=textures[i].v3=31;}
long long v[22];while(std::cin>>v[0]){for(int i=1;i<22;i++)std::cin>>v[i];events.clear();playCount=0;*(int*)0x54D474=v[0];*(unsigned*)0x60F774=v[1];*(int*)0x60F76C=v[2];*(unsigned char*)0x60CFC5=v[3];*(unsigned char*)0x550D81=v[4];*(unsigned char*)0x682770=v[5];*(int*)0x681D1C=v[6];*(int*)0x6612C0=v[7];for(unsigned a:{0x5FB480u,0x5FB488u})for(int i=0;i<5;i++)*(unsigned char*)(a+i)=v[8+(a==0x5FB480?0:5)+i];available=v[18];for(int i=0;i<5;i++)*(Texture**)(0x5FB46C+i*4)=v[19]&(1u<<i)?&textures[i]:nullptr;speechMutation=v[20];*(int*)0x568154=*(int*)0x61B5FC=512;*(int*)0x568158=*(int*)0x628614=240;*(void**)0x56FB04=polys.data();*(void**)0x5FCD1C=polys.data()+polys.size();
reinterpret_cast<void(*)(const unsigned*,unsigned*)>(0x4477F0)(nullptr,nullptr);std::cout<<'['<<*(unsigned*)0x60F774<<','<<unsigned(*(unsigned char*)0x60CFC5)<<','<<unsigned(*(unsigned char*)0x550D81)<<",[";for(unsigned a:{0x5FB480u,0x5FB488u})for(int i=0;i<5;i++){if(a!=0x5FB480||i)std::cout<<',';std::cout<<unsigned(*(unsigned char*)(a+i));}unsigned mask=0;for(int i=0;i<5;i++)if(*(Texture**)(0x5FB46C+i*4))mask|=1u<<i;std::cout<<"],"<<mask<<','<<((unsigned char*)*(void**)0x56FB04-polys.data())/40<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    cases = []
    durations = [16260, 16260, 12660, 9060]
    for difficulty, duration in enumerate(durations):
        thresholds = [duration*3//4, duration//2, duration//4, duration//8]
        times = sorted(set([0, 1, 2, 15, 31, 32, 33, duration, duration+1, 0xFFFFFFFF] +
                           [value+offset for value in thresholds for offset in [-1, 0, 1]]))
        for remaining in times:
            for alternate in [0, 1]:
                for phase in range(4):
                    primary = [0]*5 if phase < 2 else [1]*5
                    secondary = [0]*5 if phase != 3 else [1]*5
                    cases.append([difficulty, remaining, 0, alternate, int(phase != 0), phase & 1, 8, [6, 10, 11, 15][phase],
                                  *primary, *secondary, 31, 0 if phase < 2 else 31, 0, 0])
    rng = random.Random(0x4477F0)
    for _ in range(220):
        remaining = rng.choice([0, 1, 31, 32, 33, rng.randrange(2**32)])
        available, cached = rng.randrange(32), rng.randrange(32)
        if remaining < 32 and (available | cached) & 2 and not (available | cached) & 1:
            available |= 1  # Original's uninitialized poly case has no defined quad to compare.
        cases.append([rng.randrange(4), remaining, rng.choice([-2147483648, -32768, -123, 0, 200, 32767, 2147483647]),
                      rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.choice([0, 7, 8, 9]), rng.randrange(5, 17),
                      *[rng.randrange(256) for _ in range(10)], available, cached, rng.randrange(4), 0])
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-progress-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    path = root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-progress.json'
    path.write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original progress displays: genuine panel allocation/6554 rounding, all speech thresholds/mutation, pulse, unsigned remaining, signed-short wrap, lazy texture lookup, tile order and final reused portrait poly.')


if __name__ == '__main__':
    main()
