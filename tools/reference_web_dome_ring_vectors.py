"""Verify complete CDomeRing ctor and deforming AI against the actual PC instructions."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path);args=parser.parse_args()
    root=Path(__file__).resolve().parent.parent
    code=r'''
#include "core/fixed_vector.h"
#include <iostream>
#include <vector>
#include <sstream>
#include <cassert>
#include <cmath>
#include <cstring>
#include <cstdlib>
#ifdef VALIDATE_ORIGINAL
#include "original_executable.h"
#endif
struct Body{unsigned vt=0;unsigned short flags=0,inquiry=0;CVector pos;short angles[3]={};unsigned short model=0;unsigned char pad1C[3]={},region=0;unsigned next=0,rgb=0;short scale[3]={4096,4096,4096};unsigned char pad2E[0x18]={};unsigned short bodyFlags=0x16;unsigned char tail[0xAC]={};};
struct Vertex{short x,y,z,pad;};
struct Model{unsigned short flags=8,count=4,normals=0,faces=0;char pad8[20]={};Vertex vertices[4];};
struct Ring:Body{int scratch=0;short*saved=nullptr,*directions=nullptr;Model*sourceModel=nullptr;int expansion=100,fade=3,fire=0;};
static_assert(sizeof(Ring)==0x110);static_assert(offsetof(Model,vertices)==28);
std::vector<void*>allocations;int modelKind;
void*allocate(unsigned size){void*p=calloc(1,size?size:1);allocations.push_back(p);return p;}
int squareRoot(int value){if(value<=-32768)return 32768;if(value<0)return 0;return int(std::sqrt(value));}
void init(Ring&r,Model&m,CVector position,int fire){r.fire=fire;r.flags|=fire?0x200:0x600;r.rgb=0x808080;r.pos=position;r.pos.vy-=0x5A000;r.sourceModel=&m;r.saved=static_cast<short*>(allocate(6*m.count));r.directions=static_cast<short*>(allocate(6*m.count));
for(int i=0;i<m.count;i++){auto v=m.vertices[i];r.saved[i*3]=v.x;r.saved[i*3+1]=v.y;r.saved[i*3+2]=v.z;int length=squareRoot(int(v.x)*v.x+int(v.z)*v.z);assert(length);r.directions[i*3]=int(v.x)*100/length;r.directions[i*3+2]=int(v.z)*100/length;}}
void move(Ring&r){for(int i=0;i<r.sourceModel->count;i++){auto&v=r.sourceModel->vertices[i];v.x=short(v.x+r.directions[i*3]);v.z=short(v.z+r.directions[i*3+2]);}int channel=int(r.rgb&255)-r.fade;if(channel<=0){channel=0;r.bodyFlags|=0x40;}r.rgb=channel|(channel<<8)|(channel<<16);}
void output(Ring&r){std::cout<<'['<<r.pos.vx<<','<<r.pos.vy<<','<<r.pos.vz<<','<<r.fire<<','<<r.flags<<','<<r.bodyFlags<<','<<r.rgb<<",[";
for(int i=0;i<r.sourceModel->count;i++){if(i)std::cout<<',';auto v=r.sourceModel->vertices[i];std::cout<<'['<<v.x<<','<<v.y<<','<<v.z<<','<<r.directions[i*3]<<','<<r.directions[i*3+2]<<']';}std::cout<<"]]";}
std::string capture(Ring&r){std::ostringstream t;auto*old=std::cout.rdbuf(t.rdbuf());output(r);std::cout.rdbuf(old);return t.str();}
#ifdef VALIDATE_ORIGINAL
void patch(unsigned a,void*t){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(t)-a-5;}
void*__attribute__((thiscall)) baseCtor(void*p){*static_cast<Body*>(p)=Body();return p;}
void __attribute__((thiscall)) initItem(void*,const char*name){modelKind=std::strcmp(name,"firering")==0?1:0;}
void __attribute__((thiscall)) attach(void*,void*){}
void*allocateDc(unsigned bytes,int,int,void*,bool){return allocate(bytes);}
void printAssert(unsigned char value,const char*){assert(value);}
#endif
int main(int argc,char**argv){
#ifdef VALIDATE_ORIGINAL
assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x460080,reinterpret_cast<void*>(&baseCtor));patch(0x460020,reinterpret_cast<void*>(&initItem));patch(0x460260,reinterpret_cast<void*>(&attach));patch(0x4581A0,reinterpret_cast<void*>(&allocateDc));patch(0x4015B0,reinterpret_cast<void*>(&printAssert));
for(auto range:std::vector<std::pair<unsigned,int>>{{0x4F5517,6},{0x4F551E,7},{0x4F56F1,7}})std::memset(reinterpret_cast<void*>(range.first),0x90,range.second);
#endif
CVector position;int fire;Model original;while(std::cin>>position.vx>>position.vy>>position.vz>>fire){for(auto&v:original.vertices)std::cin>>v.x>>v.y>>v.z;
Model sourceModel=original;Ring source;init(source,sourceModel,position,fire);std::vector<std::string>states{capture(source)};for(int frame=1;frame<=50;frame++){if(!(source.bodyFlags&0x40))move(source);states.push_back(capture(source));}
#ifdef VALIDATE_ORIGINAL
Model actualModel=original;Model*models[]={&actualModel};*reinterpret_cast<Model***>(0x6B2454)=models;*reinterpret_cast<unsigned*>(0x6B559C)=0;
float vertices[16]={};float*dcModel[]={vertices};*reinterpret_cast<float***>(0x5F6764)=dcModel;
Ring actual;reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,int)>(0x4F5510)(&actual,&position,fire);assert(modelKind==int(fire!=0)&&capture(actual)==states[0]);
for(int frame=1;frame<=50;frame++){if(!(actual.bodyFlags&0x40))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F5800)(&actual);assert(capture(actual)==states[frame]);for(int i=0;i<4;i++)assert(vertices[i*4]==float(actualModel.vertices[i].x)&&vertices[i*4+2]==float(actualModel.vertices[i].z));}
#endif
std::cout<<'[';for(unsigned i=0;i<states.size();i++){if(i)std::cout<<',';std::cout<<states[i];}std::cout<<"]\n";for(void*p:allocations)free(p);allocations.clear();}}
'''
    rng=random.Random(0x4F5800)
    cases=[]
    for fire in [0,1,255]:
        for vertex in [(1,0,0),(-1,10,1),(100,20,200),(32767,-32768,-32768),(-32768,32767,-32768)]:
            cases.append([12345,-54321,67890,fire,*vertex,100,-100,200,-200,100,100,200,0,-100])
    for _ in range(33):
        values=[rng.randint(-32768,32767) for _ in range(12)]
        cases.append([*[rng.randint(-2147483648,2147483647) for _ in range(3)],rng.choice([0,1,255]),*values])
    with tempfile.TemporaryDirectory(prefix='openspidey-dome-ring-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        flags=['-DVALIDATE_ORIGINAL'] if args.original_exe else []
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie',*flags,
            '-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),str(root/'src/core/fixed_vector.cpp'),'-o',str(binary)],check=True)
        command=[str(binary)]+([str(args.original_exe.resolve())] if args.original_exe else [])
        lines=subprocess.check_output(command,input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/web-dome-rings.json').write_text(json.dumps(
        [dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} CDomeRing ctor/50AI cases; original PC vertices and pairedDC float uploads verified={bool(args.original_exe)}.')


if __name__=='__main__':main()
