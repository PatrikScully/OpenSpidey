"""Capture actual PC screen-target visibility/state and four-triangle geometry at an explicitly observed projection boundary."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cmath>
std::vector<long long>events;short x,y;unsigned buffer[100];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void rot(void*){}void zero(){}void load(void*){}void project(){}void xy(int*p){*p=unsigned(short(x))&65535;*p|=unsigned(short(y))<<16;}void texture(int,int){}void debug(...){}
void quad(float x0,float y0,float u0,float v0,unsigned c0,float x1,float y1,float u1,float v1,unsigned c1,float x2,float y2,float u2,float v2,unsigned c2,float x3,float y3,float u3,float v3,unsigned c3,float depth){events.insert(events.end(),{(int)x0,(int)y0,(int)x1,(int)y1,(int)x2,(int)y2,c0});assert(x2==x3&&y2==y3&&c0==c1&&c1==c2&&c2==c3&&depth==5&&u0==1&&v0==0&&u1==0&&v1==0&&u2==0&&v2==1&&u3==1&&v3==1);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x46D7B0,(void*)&rot);patch(0x46E460,(void*)&zero);patch(0x46D840,(void*)&load);patch(0x46DBC0,(void*)&project);patch(0x46DF80,(void*)&xy);patch(0x506440,(void*)&texture);patch(0x46CB90,(void*)&debug);patch(0x507910,(void*)&quad);for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}*reinterpret_cast<int*>(0x568154)=512;*reinterpret_cast<int*>(0x568158)=240;*reinterpret_cast<int*>(0x61B5FC)=512;*reinterpret_cast<int*>(0x628614)=240;*reinterpret_cast<unsigned char*>(0x54D341)=1;
int shown,px,py,radius,angle;while(std::cin>>shown>>px>>py>>radius>>angle){x=px;y=py;CVector pos(123456,-234567,345678);reinterpret_cast<void(*)(CVector*,unsigned short,short)>(0x48AA50)(&pos,radius,angle);reinterpret_cast<void(*)(bool)>(0x48AA40)(shown);*reinterpret_cast<void**>(0x56FB04)=buffer;*reinterpret_cast<void**>(0x5FCD1C)=buffer+100;events.clear();reinterpret_cast<void(*)()>(0x48AA90)();std::cout<<'['<<unsigned(*reinterpret_cast<unsigned char*>(0x6A7572));for(unsigned a:{0x6A7488u,0x6A748Cu,0x6A7490u})std::cout<<','<<*reinterpret_cast<int*>(a);std::cout<<','<<*reinterpret_cast<unsigned short*>(0x6A7494)<<','<<*reinterpret_cast<short*>(0x6A7484)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng = random.Random(0x48AA90)
    cases = [[1, 256, 120, radius, angle] for radius in [0, 11, 12, 24, 65535] for angle in [0, 128, 1024, -32768, 32767]]
    cases += [[rng.randrange(2), rng.randint(-32768, 32767), rng.randint(-32768, 32767), rng.randrange(65536), rng.randint(-32768, 32767)] for _ in range(160)]
    with tempfile.TemporaryDirectory(prefix='openspidey-screen-target-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/screen-target.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} actual PC screen-target state/geometry cases, all4 primitive UV/depth/ARGB contracts verified.')


if __name__ == '__main__': main()
