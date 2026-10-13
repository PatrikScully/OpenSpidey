"""Capture actual DoSwingingPhysics ordered hook/vertical probes without replacing its collision response."""
import argparse,json,random,subprocess,tempfile
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
struct Vector{int x,y,z;};template<class T>T&at(void*p,unsigned o){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+o);}
Vector before,after,arc,hitPosition;short normal[3];int hitAt,probeCount,hookCount;std::vector<int>events;
void patch(unsigned a,void*t){auto*p=(unsigned char*)a;p[0]=0xE9;*(unsigned*)(p+1)=(unsigned)t-a-5;}
void hook(Vector*out,void*,int n){assert(n==2);*out=hookCount++?after:before;}
void __attribute__((thiscall)) params(void*,Vector*out){*out=arc;}
void init(void*p){at<void*>(p,0x68)=nullptr;}
void line(void*p,int flags){assert(flags==1);auto&s=at<Vector>(p,0);auto&e=at<Vector>(p,12);events.insert(events.end(),{s.x,s.y,s.z,e.x,e.y,e.z});if(++probeCount==hitAt){at<void*>(p,0x68)=reinterpret_cast<void*>(1);at<Vector>(p,0x6C)=hitPosition;for(int i=0;i<3;i++)at<short>(p,0x78+2*i)=normal[i];}}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x453D60,(void*)&hook);patch(0x4F7270,(void*)&params);patch(0x4524C0,(void*)&init);patch(0x4549A0,(void*)&line);long long v[20];while(std::cin>>v[0]){for(int i=1;i<20;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={};at<Vector>(player,8)={int(v[0]),int(v[1]),int(v[2])};arc={int(v[3]),int(v[4]),int(v[5])};before={int(v[6]),int(v[7]),int(v[8])};after={int(v[9]),int(v[10]),int(v[11])};hitAt=v[12];hitPosition={int(v[13]),int(v[14]),int(v[15])};for(int i=0;i<3;i++)normal[i]=v[16+i];at<unsigned short>(player,0xEA8)=v[19];at<void*>(player,0xE64)=reinterpret_cast<void*>(1);hookCount=probeCount=0;events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x467D20)(player);auto&p=at<Vector>(player,8);auto&vel=at<Vector>(player,0x60);std::cout<<'['<<p.x<<','<<p.y<<','<<p.z<<','<<vel.x<<','<<vel.y<<','<<vel.z<<','<<int(player[0xE0])<<','<<(at<void*>(player,0xB74)?1:0)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng=random.Random(0x467D20)
    cases=[[12345,-54321,67890,23456,-65432,78901,11,-12,13,21,-22,23,hit,123456,-234567,345678,3500,y,-900,clearance]
           for hit in [0,1,2,3] for y in [-32768,-2601,-2600,0,3400,3401,32767] for clearance in [0,96,65535]]
    cases += [[*[rng.randint(-2147483648,2147483647) for _ in range(12)],rng.randrange(4),
               *[rng.randint(-2147483648,2147483647) for _ in range(3)],*[rng.randint(-32768,32767) for _ in range(3)],rng.randrange(65536)] for _ in range(100)]
    with tempfile.TemporaryDirectory(prefix='openspidey-swing-physics-') as directory:
        cpp=Path(directory)/'reference.cpp';binary=Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/player-swing-physics.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC swing-physics sequences with exact ordered rays, signed normal threshold and clearance response.')

if __name__=='__main__':main()
