"""Capture whole original464270 Scorpion/Jonah display through actual PC quad submission and original stretch helpers."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&global(unsigned a){return *reinterpret_cast<T*>(a);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)f-a-5;}
unsigned bits(float v){unsigned n;std::memcpy(&n,&v,4);return n;}
std::vector<std::vector<unsigned>> draws;unsigned textureId,blend;
void useTexture(unsigned id,int mode){textureId=id;blend=mode;}
void quad(float x0,float y0,float u0,float v0,unsigned c0,float x1,float y1,float u1,float v1,unsigned c1,float x2,float y2,float u2,float v2,unsigned c2,float x3,float y3,float u3,float v3,unsigned c3,float z){
 draws.push_back({textureId,blend,bits(z),bits(x0),bits(y0),bits(x1),bits(y1),bits(x2),bits(y2),bits(x3),bits(y3),bits(u0),bits(v0),bits(u1),bits(v1),bits(u2),bits(v2),bits(u3),bits(v3),c0,c1,c2,c3});}
void flat(float x,float y,float w,float h,float u0,float v0,float u1,float v1,unsigned colour,float depth,bool){quad(x,y,u0,v0,colour,x+w,y,u1,v0,colour,x,y+h,u0,v1,colour,x+w,y+h,u1,v1,colour,depth);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x506440,(void*)&useTexture);patch(0x507910,(void*)&quad);patch(0x507470,(void*)&flat);*(unsigned char*)0x46CB90=0xC3;
 alignas(4)unsigned char primary[0xC28]={},secondary[0xC28]={},textures[5][44]={},frames[32]={},polygons[8192]={};
 for(unsigned i=0;i<5;i++){at<unsigned short>(textures[i],2)=i+10;textures[i][10]=63;textures[i][11]=63;global<void*>(0x60F658+4*i)=textures[i];}
 global<void*>(0x60F758)=frames;at<void*>(frames,28)=textures[4];global<unsigned>(0x61B5FC)=512;global<unsigned>(0x628614)=240;
 int present,dead,baseline,health,flash,secondaryTexture,jonahBaseline,jonahHealth,jonahDead,offset,offX,offY,frameW,frameH,width,height;
 while(std::cin>>present>>dead>>baseline>>health>>flash>>secondaryTexture>>jonahBaseline>>jonahHealth>>jonahDead>>offset>>offX>>offY>>frameW>>frameH>>width>>height){
  draws.clear();global<void*>(0x60F788)=present?primary:nullptr;global<void*>(0x60F78C)=secondary;global<int>(0x60F654)=310;global<int>(0x60F744)=baseline;global<int>(0x60F748)=jonahBaseline;global<int>(0x60F76C)=offset;
  global<void*>(0x60F660)=secondaryTexture?textures[2]:nullptr;at<unsigned short>(primary,0x46)=dead;at<short>(primary,0xE2)=health;at<int>(primary,0x3EC)=flash;at<unsigned short>(secondary,0x46)=jonahDead;at<short>(secondary,0xE2)=jonahHealth;
  frames[24]=offX;frames[25]=offY;frames[26]=frameW;frames[27]=frameH;global<void*>(0x56FB04)=polygons;global<void*>(0x5FCD1C)=polygons+sizeof(polygons);global<int>(0x568154)=width;global<int>(0x568158)=height;
  reinterpret_cast<void(*)()>(0x464270)();std::cout<<"{\"owners\":["<<(global<void*>(0x60F788)!=nullptr)<<','<<(global<void*>(0x60F78C)!=nullptr)<<"],\"draws\":[";
  for(unsigned i=0;i<draws.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned j=0;j<draws[i].size();j++){if(j)std::cout<<',';std::cout<<draws[i][j];}std::cout<<']';}std::cout<<"]}\n";
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for baseline in [1, 2, 120, 900, 32767, -1, -32768]:
        for health in [-32768, -1, 0, 1, 119, 120, 450, 899, 900, 901, 32767]:
            for flash in [0, 30]:
                cases.append([1, 0, baseline, health, flash, 1, 120, 60, 0, 0, -8, -8, 16, 16, 640, 480])
    for present, dead in [(0, 0), (1, 0x40), (1, 0x41), (1, 0x80)]:
        for secondary in [0, 1]:
            cases.append([present, dead, 900, 450, 1, secondary, 120, -1, 0x40, 29, -128, 127, 255, 1, 1280, 720])
    rng = random.Random(0x464270)
    for _ in range(180):
        cases.append([1, rng.choice([0, 1, 0x10, 0x80]), rng.choice([1, 120, 900, 32767, -32768]), rng.randint(-32768, 32767),
            rng.choice([-2147483648, -1, 0, 1, 30, 2147483647]), rng.randrange(2), rng.choice([1, 2, 120, 32767, -32768]),
            rng.randint(-32768, 32767), rng.choice([0, 0x40, 0x80]), rng.choice([-32768, -1, 0, 29, 32767]),
            rng.randint(-128, 127), rng.randint(-128, 127), rng.randrange(1, 256), rng.randrange(1, 256), rng.choice([640, 1280]), rng.choice([480, 720])])
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-health-display-') as folder:
        source, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        source.write_text(HARNESS)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(source), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    fixture = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-health-display.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original464270 Scorpion/Jonah PC displays, including signed health truncation,3EC selection, source sprite offsets, native quad coordinates/UV/colours/depth/blend and exact primary-death owner clearing.')


if __name__ == '__main__':
    main()
