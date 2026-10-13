"""Execute the original PC CPowerUp constructor/AI/TakeEffect with typed world, asset and resource boundaries."""
import argparse
import itertools
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
std::vector<long long>events;int height,accepted,level;void*current;unsigned short links[2];unsigned char bit[0x68];
int rnd(int n){events.insert(events.end(),{1,n});return n-1;}
int ground(void*p,int above,int below,int flags){assert(flags==0);events.insert(events.end(),{2,at<int>(p,0),at<int>(p,4),at<int>(p,8),above,below});return height;}
void __attribute__((thiscall)) shadowOn(void*p){at<unsigned short>(p,0x46)|=8;events.push_back(3);}
void __attribute__((thiscall)) killShadow(void*p){at<unsigned short>(p,0x46)&=~8;events.push_back(4);}
void __attribute__((thiscall)) init(void*,const char*name){assert(std::strcmp(name,"items")==0);events.push_back(5);}
int model(unsigned checksum,int){events.insert(events.end(),{6,checksum,at<short>(current,0xE2)});return 2;}
void __attribute__((thiscall)) attach(void*,void**list){assert(list==(void**)0x60FB94);events.push_back(7);}
unsigned short*getLinks(int node){links[0]=1;links[1]=node;return links;}
void pulse(unsigned short*p){events.insert(events.end(),{8,p[1]});}
void signal(unsigned short*p){assert(p[0]==1);events.insert(events.end(),{9,p[1]});}
void glow(void*p,int radius,int red,int green,int blue,int points,int random,int width){events.insert(events.end(),{10,at<int>(p,0),at<int>(p,4),at<int>(p,8),radius,red,green,blue,points,random,width});}
void __attribute__((thiscall)) createBit(void*p){assert(at<unsigned short>(p,0x38)==18);at<void*>(p,0x130)=bit;events.insert(events.end(),{11,at<short>(p,0xE2)});}
int __attribute__((thiscall)) setPos(void*,void*p){events.insert(events.end(),{12,at<int>(p,0),at<int>(p,4),at<int>(p,8)});return 1;}
void __attribute__((thiscall)) deleteStuff(void*p){killShadow(p);if(at<void*>(p,0x130)){events.push_back(13);at<void*>(p,0x130)=nullptr;}if(at<void*>(p,0xF8)){events.push_back(14);at<void*>(p,0xF8)=nullptr;}}
int __attribute__((thiscall)) incHealth(void*,int n){events.insert(events.end(),{15,n});return accepted;}
unsigned char __attribute__((thiscall)) incWeb(void*,int n){events.insert(events.end(),{16,n});return accepted;}
unsigned char __attribute__((thiscall)) fire(void*){events.push_back(17);return accepted;}
unsigned char __attribute__((thiscall)) armor(void*,bool active){assert(active);events.push_back(18);return accepted;}
int getLevel(){return level;}
unsigned char speech(int bank,int slot,int priority){events.insert(events.end(),{19,bank,slot,priority});return 1;}
void sound(unsigned n,short volume,int pitch){events.insert(events.end(),{20,n,volume,pitch});}
extern "C" __attribute__((naked)) void ctorEnd(){asm volatile("mov %esi,%eax;pop %edi;pop %esi;pop %ebp;pop %ebx;add $16,%esp;ret $24");}
extern "C" __attribute__((naked,thiscall)) void construct(void*,int,void*,void*,unsigned,int,int){asm volatile("sub $16,%esp;push %ebx;push %ebp;push %esi;push %edi;mov %ecx,%esi;mov $0x46AFE5,%eax;jmp *%eax");}
void state(void*p){std::vector<long long>s={at<unsigned short>(p,0x46),at<unsigned short>(p,4),at<unsigned>(p,0x24)};
for(unsigned off:{8u,12u,16u,0x110u,0x114u,0x118u,0x60u,0x64u,0x68u,0x70u})s.push_back(at<int>(p,off));
for(unsigned off:{0x78u,0x79u,0x7Au})s.push_back(at<unsigned char>(p,off));
s.insert(s.end(),{at<short>(p,0x16),at<short>(p,0x8A),at<short>(p,0xE2),at<unsigned short>(p,0xDC),at<unsigned>(p,0xE4),at<unsigned char>(p,0x100),at<unsigned short>(p,0x106),at<unsigned short>(p,0x108)});
for(unsigned off:{0x101u,0x102u,0x103u,0x104u,0x105u})s.push_back(at<unsigned char>(p,off));s.push_back(at<int>(p,0x10C));
for(unsigned off:{0xB8u,0xBCu,0xC0u})s.push_back(at<int>(p,off));
s.insert(s.end(),{at<unsigned short>(p,0xD0),at<short>(p,0xD4),at<unsigned short>(p,0x11E),at<unsigned short>(p,0x120),at<short>(p,0x122),at<unsigned char>(p,0x124),at<int>(p,0x128),at<unsigned short>(p,0x12C),at<unsigned>(p,0x134),at<void*>(p,0x130)!=nullptr,at<void*>(p,0xF8)!=nullptr});
std::cout<<'[';for(unsigned i=0;i<s.size();i++){if(i)std::cout<<',';std::cout<<s[i];}std::cout<<']';}
void output(void*p,int result,int revision){std::cout<<"{\"state\":";state(p);std::cout<<",\"result\":"<<result<<",\"revision\":"<<revision<<",\"comic\":"<<*(unsigned*)0x6828E4<<",\"costume\":"<<*(unsigned*)0x6828D8<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4E5DA0,(void*)&rnd);patch(0x4E6840,(void*)&ground);patch(0x460560,(void*)&shadowOn);patch(0x460570,(void*)&killShadow);patch(0x460020,(void*)&init);patch(0x4C93B0,(void*)&model);patch(0x460260,(void*)&attach);patch(0x4E3880,(void*)&getLinks);patch(0x4DFD30,(void*)&pulse);patch(0x4DFFE0,(void*)&signal);patch(0x43C150,(void*)&glow);patch(0x46B470,(void*)&createBit);patch(0x408950,(void*)&setPos);patch(0x46B600,(void*)&deleteStuff);patch(0x46B3EA,(void*)&ctorEnd);
patch(0x4BD750,(void*)&incHealth);patch(0x4BAFE0,(void*)&incWeb);patch(0x4BAFC0,(void*)&fire);patch(0x4BAEC0,(void*)&armor);patch(0x4DE770,(void*)&getLevel);patch(0x479EE0,(void*)&speech);patch(0x471C50,(void*)&sound);
*(unsigned char*)0x4015B0=0xC3;int family;
while(std::cin>>family){events.clear();alignas(4)unsigned char body[0x140]={},player[0xF10]={};current=body;at<unsigned short>(body,0x46)=0x16;at<unsigned char>(body,0x78)=at<unsigned char>(body,0x79)=at<unsigned char>(body,0x7A)=1;at<unsigned short>(body,0xD0)=32;at<short>(body,0xD4)=200;
unsigned table[5]={0,0x46B6F0,0,0,reinterpret_cast<unsigned>(&deleteStuff)};at<void*>(body,0)=table;*(unsigned*)0x6828E4=*(unsigned*)0x6828D8=0;
if(family==0){int type,flags,lifetime,signalNode;unsigned comic;int p[3],v[3];std::cin>>type>>flags>>lifetime>>signalNode>>height>>comic;for(auto&n:p)std::cin>>n;for(auto&n:v)std::cin>>n;*(unsigned*)0x6828E4=comic;
construct(body,type,p,v,flags,lifetime,signalNode);output(body,0,0);
}else if(family==1){int type,lifetime,is3d,item,flash,fade,distance,falling,checked,groundHeight,logic,ticks,health;std::cin>>type>>lifetime>>is3d>>item>>flash>>fade>>distance>>falling>>checked>>groundHeight>>logic>>ticks>>height>>health;
at<unsigned short>(body,0x38)=type;at<unsigned short>(body,0x12C)=lifetime;at<unsigned char>(body,0x101)=is3d;at<unsigned short>(body,4)=item;at<unsigned char>(body,0x124)=flash;at<int>(body,0x128)=fade;at<unsigned>(body,0xE4)=distance;at<unsigned char>(body,0x103)=falling;at<unsigned char>(body,0x104)=checked;at<int>(body,0x10C)=groundHeight;*(int*)0x60CFA8=logic;at<int>(body,0x80)=ticks;at<short>(body,0xE2)=health;
at<unsigned short>(body,0x11E)=at<unsigned short>(body,0x120)=16;at<unsigned char>(body,0x105)=128;at<short>(body,0x8A)=24;at<int>(body,0x70)=32768;at<unsigned char>(body,0x79)=5;at<int>(body,0x110)=123456;at<int>(body,0x114)=-234567;at<int>(body,0x118)=345678;at<int>(body,0x60)=12345;at<int>(body,0x64)=54321;at<int>(body,0x68)=-6789;at<unsigned short>(body,0x108)=0xFFFF;
reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x46BB20)(body);output(body,0,0);
}else{int type,health,hasNode,fade,busy,max,revision;unsigned clock,last,comic,costume;std::cin>>type>>health>>hasNode>>fade>>busy>>clock>>last>>accepted>>level>>max>>revision>>comic>>costume;
at<unsigned short>(body,0x38)=type;at<short>(body,0xE2)=health;at<unsigned char>(body,0x100)=hasNode;at<unsigned short>(body,0x106)=123;at<unsigned short>(body,0x108)=321;at<int>(body,0x128)=fade;at<unsigned>(body,0x134)=last;at<int>(body,8)=123456;at<int>(body,12)=-234567;at<int>(body,16)=345678;at<int>(player,0xEF0)=max;at<int>(player,0x5D0)=revision;*(unsigned char*)0x682770=busy;*(unsigned*)0x6B4CA8=clock;*(unsigned*)0x6828E4=comic;*(unsigned*)0x6828D8=costume;
int result=reinterpret_cast<unsigned char(__attribute__((thiscall))*)(void*,void*)>(0x46B860)(body,player);output(body,result,at<int>(player,0x5D0));
}}
}
'''
    constructors=[[type,flags,life,signal,height,comic,*p,12345,-23456,34567]
                  for type,flags,life,signal,height,comic,p in itertools.product([8,10,11,12,13,14,15,16,18],
                    [0,4,8,12,16,24],[0,10,31,65535],[65535,123],[-1,1000000],[0,0x80000401],[(123456,999999,-234567),(0,0,0)])]
    ai_rows=([type,life,is3d,item,flash,fade,distance,falling,checked,ground,logic,ticks,height,health]
         for (type,is3d),life,item,flash,fade,distance,falling,checked,ground,logic,ticks,height,health in itertools.product(
             [(8,1),(18,0)], [65535,61,60,59,32,31,30,2,1,0], [0,1,0xFFFF], [0,1], [0,2,3,64,-1], [0,8000,8001,-1],
             [0,1], [0,1], [-5,-4,-1,0,1000000], [0,1,2,3], [0,2,6], [-1,999999], [20]))
    # Exercise every switch and signed/unsigned boundary without generating a large cross-product of equivalent frames.
    ais=list(itertools.islice(ai_rows,0,None,307))+[[18,1,0,1,1,0,8000,1,0,-4,1,6,-1,20],[8,1,1,0,0,0,0,1,0,0,1,2,0,50]]
    effects=[[type,health,node,fade,busy,clock,last,accepted,level,max,revision,comic,costume]
             for type,health,node,fade,busy,clock,last,accepted,level,max,revision,comic,costume in itertools.product(
                 [8,10,11,12,13,14,15,16,18],[0,20,31,-1,4096],[0,1],[0,2],[0,1],[0,301,0xFFFFFFFF],[0,301],
                 [0,1],[0x101,0x1601],[100],[2147483647],[0x80000000],[0x41])]
    effects=effects[::17]+[[11,0,1,0,0,301,0,0,0x101,100,0,0,0],[10,31,1,0,0,301,0,1,0x101,100,0,0,0]]
    rows=[(0,'constructor',row) for row in constructors]+[(1,'ai',row) for row in ais]+[(2,'effect',row) for row in effects]
    with tempfile.TemporaryDirectory(prefix='openspidey-powerup-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(str(family)+' '+' '.join(map(str,row)) for family,_,row in rows)+'\n').encode()).decode().splitlines()
    fixtures=[dict(kind=kind,input=row,expected=json.loads(line)) for (_,kind,row),line in zip(rows,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/powerup.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Captured {len(constructors)} actual PC constructors, {len(ais)} AI frames and {len(effects)} complete effects.')


if __name__=='__main__':
    main()
