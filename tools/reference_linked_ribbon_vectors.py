"""Capture actual PC40E840 connected ribbon quads over typed projected endpoints and genuine arithmetic/draw boundaries."""
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
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
CVector projected;std::vector<std::vector<long long>>draws;unsigned char queue[0x2000],clip[12],texture[16];
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void load(CVector*v){projected=*v;}void project(){}void z(int*p){*p=projected.vz;}void screen(unsigned*p){*p=(unsigned short)projected.vx|((unsigned)(unsigned short)projected.vy<<16);}
void use(int id,int blend){assert(id==19&&blend==2);}void debug(...){}double absolute(double x){return std::fabs(x);}
void quad(float ax,float ay,float az,float au,float av,unsigned ac,float bx,float by,float bz,float bu,float bv,unsigned bc,float cx,float cy,float cz,float cu,float cv,unsigned cc,float dx,float dy,float dz,float du,float dv,unsigned dc){assert(ac==bc&&bc==cc&&cc==dc&&az==1&&bz==1&&cz==1&&dz==1&&au==0&&av==0&&bu==1&&bv==0&&cu==0&&cv==1&&du==1&&dv==1);draws.push_back({(ac>>16&255)-1,(int)ax,(int)ay,(int)bx,(int)by,(int)cx,(int)cy,(int)dx,(int)dy});}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x46D840,(void*)&load);patch(0x46DBC0,(void*)&project);patch(0x46DF70,(void*)&z);patch(0x46DF80,(void*)&screen);patch(0x506440,(void*)&use);patch(0x508550,(void*)&quad);patch(0x46CB90,(void*)&debug);patch(0x5294C4,(void*)&absolute);
*reinterpret_cast<int*>(0x568154)=*reinterpret_cast<int*>(0x61B5FC)=512;*reinterpret_cast<int*>(0x568158)=*reinterpret_cast<int*>(0x628614)=240;*reinterpret_cast<void**>(0x64E514)=clip;at<unsigned short>(clip,8)=5;at<unsigned short>(clip,10)=8048;at<unsigned short>(texture,2)=19;memset(reinterpret_cast<void*>(0x56E6F8),0,64);*reinterpret_cast<float*>(0x56E734)=1;memset(reinterpret_cast<void*>(0x56F1B4),0,12);
int n;while(std::cin>>n){alignas(4)unsigned char bits[4][0x70]={},frames[4][8]={};for(int i=0;i<n;i++){int flags,x0,y0,z0,x1,y1,z1,scale,width;std::cin>>flags>>x0>>y0>>z0>>x1>>y1>>z1>>scale>>width;at<void*>(bits[i],8)=i+1<n?bits[i+1]:nullptr;bits[i][0x50]=flags;at<CVector>(bits[i],0x58)=CVector(x0<<12,y0<<12,z0<<12);at<CVector>(bits[i],0x64)=CVector(x1<<12,y1<<12,z1<<12);at<unsigned>(bits[i],0x40)=0x2E000000|(i+1);at<unsigned short>(bits[i],0x56)=scale;frames[i][2]=width;frames[i][3]=15;at<void*>(frames[i],4)=texture;at<void*>(bits[i],0x4C)=frames[i];}
*reinterpret_cast<void**>(0x56FB04)=queue;*reinterpret_cast<void**>(0x5FCD1C)=queue+sizeof queue;void*head=bits[0];draws.clear();reinterpret_cast<void(*)(void**)>(0x40E840)(&head);std::cout<<'[';for(unsigned i=0;i<draws.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned j=0;j<draws[i].size();j++){if(j)std::cout<<',';std::cout<<draws[i][j];}std::cout<<']';}std::cout<<"]\n";}}
'''
    cases=[]
    for winding in range(4):
        for scale in [0,1,400,32767,32768,65535]:
            for depth in [5,6,100,8048]:
                cases.append([[16|winding,30,-40,depth,190,70,depth,scale,32],
                              [winding,-123,-234,1,250,130,depth,scale,64],
                              [32|winding,-333,123,depth,290,160,depth,scale,15]])
    cases += [[[16,0,0,0,100,100,200,400,32],[0,0,0,200,300,100,300,400,32]],
              [[16,0,0,200,0,0,200,400,32],[0,100,100,200,300,100,300,400,32]],
              [[16,0,0,200,100,100,8049,400,32],[0,100,100,200,300,100,300,400,32]],
              [[0,100,100,200,300,100,300,400,32],[0,300,100,300,400,200,300,400,32]]]
    rng=random.Random(0x40E840)
    for _ in range(160):
        cases.append([[rng.randrange(64),rng.randint(-32000,32000),rng.randint(-32000,32000),rng.choice([0,1,5,200,8000,8049]),
                       rng.randint(-32000,32000),rng.randint(-32000,32000),rng.choice([0,1,5,200,8000,8049]),rng.randrange(65536),rng.randrange(256)] for _ in range(4)])
    with tempfile.TemporaryDirectory(prefix='openspidey-linked-ribbon-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(str(len(c))+' '+' '.join(str(x) for s in c for x in s) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/linked-ribbon-projection.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} original PC ribbon render lists: width+2, signed scale, source cache/failure/history, winding, short corners and constant UV/common-depth draws.')


if __name__=='__main__':main()
