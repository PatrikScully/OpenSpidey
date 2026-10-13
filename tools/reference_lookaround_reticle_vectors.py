"""Capture complete actual PC world-lookaround reticle/quad calls with observed native projection and allocation boundaries."""
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
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;CVector loaded;int loads,depth,allocationFail;short x,y;unsigned poly[20],texture[4];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void rotation(void*){}void zero(){}void project(){}void load(void*p){loaded=at<CVector>(p,0);loads++;}
void screen(int*p){*p=(unsigned short)x|((unsigned)(unsigned short)y<<16);}void z(int*p){*p=depth;}
void*allocate(void*,int mode){assert(mode==0);events.push_back(1);memset(poly,0,sizeof poly);return allocationFail?nullptr:poly;}
void useTexture(int id,int mode){events.insert(events.end(),{2,id,mode});assert(id==7&&mode==1);}
void debug(...){}void assertion(int pass,const char*){assert(pass);}
void quad(float x0,float y0,float u0,float v0,unsigned c0,float x1,float y1,float u1,float v1,unsigned c1,float x2,float y2,float u2,float v2,unsigned c2,float x3,float y3,float u3,float v3,unsigned c3,float layer){events.insert(events.end(),{3,(int)x0,(int)y0,(int)x1,(int)y1,(int)x2,(int)y2,(int)x3,(int)y3,c0,(int)(layer*10000)});assert(c0==c1&&c1==c2&&c2==c3&&layer==6&&u0==0&&v0==0&&u1==1&&v1==0&&u2==0&&v2==1&&u3==1&&v3==1);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x46D7B0,(void*)&rotation);patch(0x46E460,(void*)&zero);patch(0x46D840,(void*)&load);patch(0x46DBC0,(void*)&project);patch(0x46DF70,(void*)&z);patch(0x46DF80,(void*)&screen);patch(0x462B90,(void*)&allocate);patch(0x506440,(void*)&useTexture);patch(0x507910,(void*)&quad);patch(0x46CB90,(void*)&debug);patch(0x4015B0,(void*)&assertion);
*reinterpret_cast<int*>(0x568154)=512;*reinterpret_cast<int*>(0x568158)=240;*reinterpret_cast<int*>(0x61B5FC)=512;*reinterpret_cast<int*>(0x628614)=240;*reinterpret_cast<unsigned char*>(0x54D341)=1;at<unsigned short>(texture,2)=7;
long long v[16];while(std::cin>>v[0]){for(int i=1;i<16;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},frame[8]={};player[0xDE4]=v[0];at<CVector>(player,0xDC0)=CVector(v[1],v[2],v[3]);at<CVector>(reinterpret_cast<void*>(0x56F1B4),0)=CVector(v[4],v[5],v[6]);x=v[7];y=v[8];depth=v[9];at<unsigned>(player,0xDE8)=v[10];frame[0]=v[11];frame[1]=v[12];frame[2]=v[13];frame[3]=v[14];at<void*>(frame,4)=texture;at<void*>(player,0xDEC)=frame;allocationFail=v[15];events.clear();loaded=CVector();loads=0;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C4940)(player);std::cout<<'['<<loads<<','<<loaded.vx<<','<<loaded.vy<<','<<loaded.vz<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = [[1, 123456, -234567, 345678, 12, -23, 34, 256, 120, depth, colour, -32, -32, 64, 64, failed]
             for depth in [-2147483648, -1, 0, 200, 768, 2304, 2305, 3072, 4096, 2147483647]
             for colour in [0x202080, 0x208020, 0x802020, 0xFFFFFFFF] for failed in [0, 1]]
    rng = random.Random(0x4C4940)
    cases += [[rng.choice([0, 1, 255]), *[rng.randint(-2147483648, 2147483647) for _ in range(6)], rng.randint(-32768, 32767),
               rng.randint(-32768, 32767), rng.randint(-2147483648, 2147483647), rng.randrange(2**32), rng.randint(-128, 127),
               rng.randint(-128, 127), rng.randrange(256), rng.randrange(256), rng.randrange(2)] for _ in range(240)]
    with tempfile.TemporaryDirectory(prefix='openspidey-lookaround-reticle-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        requests = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=requests).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/lookaround-reticle.json').write_text(json.dumps([dict(input=case, expected=json.loads(line))
        for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original PC reticle calls, original unsigned-scale/short corners, projection vector, allocation exit and all4 UV/depth/ARGB contracts.')


if __name__ == '__main__': main()
