"""Run actual mapped PC CManipOb AI0x4574D0 and real LineToSphere at6144; record explicit world/hit/Chunk boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True);args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <cstdlib>
#include <cstdint>
std::vector<int>events;int seed,worldHit;int contact[3];short normal[3];
template<class T>T&at(void*p,int offset){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+offset);}
void patch(unsigned address,void*fn){auto p=reinterpret_cast<unsigned char*>(address);*p=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(fn)-address-5;}
int rnd(int range){events.insert(events.end(),{1,range});unsigned value=unsigned(seed)*1664525u+1013904223u;seed=int(value);return range>0?int((value>>1)%unsigned(range)):0;}
void initLine(void*line){at<void*>(line,0x68)=nullptr;}
void world(void*line,int mode){events.push_back(4);for(int i=0;i<6;i++)events.push_back(at<int>(line,i*4));events.push_back(mode);at<void*>(line,0x68)=worldHit?reinterpret_cast<void*>(1):nullptr;std::memcpy(static_cast<char*>(line)+0x6C,contact,12);std::memcpy(static_cast<char*>(line)+0x78,normal,6);}
void deleteArray(void*p){std::free(p);}
void __attribute__((thiscall)) die(void*self){at<unsigned short>(self,0x46)|=0x40;}
int __attribute__((thiscall)) targetHit(void*,void*hit){events.insert(events.end(),{5,int(at<unsigned char>(hit,0)),at<int>(hit,4),int(at<unsigned short>(hit,8)),at<int>(hit,12),at<int>(hit,16),at<int>(hit,20),int(at<short>(hit,24)),int(at<unsigned short>(hit,26))});return 1;}
void __attribute__((thiscall)) chunk(void*self,void*line,void*){events.insert(events.end(),{9,int(at<short>(line,0x78)),int(at<short>(line,0x7A)),int(at<short>(line,0x7C)),at<int>(self,8),at<int>(self,12),at<int>(self,16)});}
void __attribute__((thiscall)) shake(void*,void*pos,int amount){events.insert(events.end(),{8,at<int>(pos,0),at<int>(pos,4),at<int>(pos,8),amount});}
void jsonEvents(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4E5DA0,reinterpret_cast<void*>(&rnd));patch(0x4524C0,reinterpret_cast<void*>(&initLine));patch(0x4549A0,reinterpret_cast<void*>(&world));patch(0x458210,reinterpret_cast<void*>(&deleteArray));patch(0x456E90,reinterpret_cast<void*>(&chunk));patch(0x416880,reinterpret_cast<void*>(&shake));
int active;while(std::cin>>active){alignas(4)unsigned char self[0x128]={},target[0x100]={};void*vt[4]={nullptr,reinterpret_cast<void*>(&die),nullptr,reinterpret_cast<void*>(&targetHit)};at<void**>(self,0)=vt;at<void**>(target,0)=vt;at<unsigned short>(self,0x46)=0x16;
int offsets[]={8,12,16,96,100,104,108,112,116};for(int off:offsets)std::cin>>at<int>(self,off);for(int off:{20,22,24,136,138,140})std::cin>>at<short>(self,off);for(int off:{120,121,122}){int x;std::cin>>x;at<unsigned char>(self,off)=x;}
int flags,impact,count,index;std::cin>>flags>>impact>>seed>>count>>index;at<int>(self,0x10C)=flags;at<short>(self,0x106)=impact;at<int>(self,0xF8)=count;at<int>(self,0x100)=index;
if(active){int*p=static_cast<int*>(std::malloc(count*12));for(int i=0;i<count*3;i++)std::cin>>p[i];at<void*>(self,0xFC)=p;}
int targetPresent,type,radius,bodyFlags,ignore;std::cin>>targetPresent>>type>>radius>>bodyFlags>>ignore;at<unsigned short>(target,0x38)=type;at<unsigned short>(target,0xDC)=radius;at<unsigned short>(target,0x46)=bodyFlags;for(int off:{8,12,16})std::cin>>at<int>(target,off);at<void*>(reinterpret_cast<void*>(0x56E990),0)=targetPresent?target:nullptr;at<int>(reinterpret_cast<void*>(0x5FBEF0),0)=ignore;
for(int&i:contact)std::cin>>i;for(short&i:normal)std::cin>>i;
std::cout<<'[';for(int update=0;update<8;update++){std::cin>>at<int>(self,0x80)>>worldHit;events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4574D0)(self);if(update)std::cout<<',';std::cout<<'[';bool comma=false;for(int off:offsets){if(comma)std::cout<<',';comma=true;std::cout<<at<int>(self,off);}for(int off:{20,22,24,136,138,140})std::cout<<','<<at<short>(self,off);std::cout<<','<<at<int>(self,0x10C)<<','<<at<short>(self,0x106)<<','<<at<unsigned short>(self,0x46)<<','<<at<int>(self,0xF8)<<','<<at<int>(self,0x100)<<','<<(at<void*>(self,0xFC)!=nullptr)<<',';jsonEvents();std::cout<<']';}std::cout<<"]\n";std::free(at<void*>(self,0xFC));}
}
'''
    rng=random.Random(0x4574d0);cases=[]
    for i in range(350):
        pos=[rng.randint(-20000,20000)*4096+rng.randrange(4096) for _ in range(3)]
        vel=[rng.randint(-150,150)*4096+rng.randrange(4096) for _ in range(3)]
        acc=[rng.randint(-8192,8192) for _ in range(3)]
        angles=[rng.randint(-32768,32767) for _ in range(3)];angular=[rng.randint(-128,128) for _ in range(3)]
        pathPresent=rng.randrange(3)==0;count=rng.choice([2,3,4,9]);index=rng.choice([0,1,count-1,count,2147483647])
        points=[[p+rng.randint(-300,300)*4096 for p in pos] for _ in range(count)] if pathPresent else []
        targetPos=[pos[0]+rng.randint(-300,300)*4096,pos[1],pos[2]]
        steps=[[rng.choice([0,1,2,3,5]),rng.randrange(2)] for _ in range(8)]
        cases.append(dict(pathPresent=pathPresent,position=pos,velocity=vel,acceleration=acc,angles=angles,angular=angular,damping=[rng.choice([0,1,4,12,31,32]) for _ in range(3)],flags=rng.choice([0,1,5,9,17,25,33,41]),impact=rng.choice([-32768,-1,0,1,32767]),seed=rng.randint(-2147483648,2147483647),count=count,index=index,points=points,targetPresent=rng.randrange(2),targetType=rng.choice([304,305,307,310,312,313,317,324,401]),targetRadius=rng.choice([0,1,150,400]),targetFlags=rng.choice([0,0x40]),ignore=rng.randrange(2),targetPosition=targetPos,contact=[p+rng.randint(-100,100)*4096 for p in pos],normal=rng.choice([[0,-4096,0],[4096,0,0],[0,4096,0],[32767,-32768,0]]),steps=steps))
    rows=[]
    for c in cases:
        row=[int(c['pathPresent']),*c['position'],*c['velocity'],*c['acceleration'],*c['angles'],*c['angular'],*c['damping'],c['flags'],c['impact'],c['seed'],c['count'],c['index']]
        row.extend(v for point in c['points'] for v in point)
        row.extend([c['targetPresent'],c['targetType'],c['targetRadius'],c['targetFlags'],c['ignore'],*c['targetPosition'],*c['contact'],*c['normal']]);row.extend(v for step in c['steps'] for v in step);rows.append(' '.join(map(str,row)))
    with tempfile.TemporaryDirectory(prefix='openspidey-manipulation-pc-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(rows)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/manipulation-ai.json').write_text(json.dumps([dict(input=c,expected=json.loads(line)) for c,line in zip(cases,lines,strict=True)],separators=(',',':'))+'\n')
    print(f'Generated {len(cases)} actual-PC manipulation AI sequences,8updates each; real sphere math6144, explicit world/hit/chunk/camera/RNG boundaries.')

if __name__=='__main__':main()
