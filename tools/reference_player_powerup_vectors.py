"""Capture whole original PC power-up collection, IncHealth and SetArmor leaves at genuine resource boundaries."""
import argparse
import itertools
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
std::vector<int>events;int accepted;unsigned char*picks;
unsigned char __attribute__((thiscall)) take(void*p,void*){events.insert(events.end(),{1,int((static_cast<unsigned char*>(p)-picks)/0x140)});return accepted;}
void sound(unsigned n,short volume,int pitch){events.insert(events.end(),{2,int(n),volume,pitch});}
void*anim(const char*name,int keep){assert(std::strcmp(name,"costarm")==0&&keep==1);events.push_back(3);return (void*)0x1234;}
void swap(int from,int to){events.insert(events.end(),{4,from,to});}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void collect(void*){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;mov $0x4B7A74,%eax;jmp *%eax");}
void output(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x46B860,(void*)&take);patch(0x471C50,(void*)&sound);patch(0x4B7B1E,(void*)&leafEnd);patch(0x4CAB50,(void*)&anim);patch(0x4B8D80,(void*)&swap);
int family;while(std::cin>>family){events.clear();alignas(4)unsigned char player[0xF10]={};
if(family==0){int action,jingle,type,flags,radius,x,y,z,count;std::cin>>action>>jingle>>type>>flags>>radius>>x>>y>>z>>accepted>>count;
alignas(4)unsigned char pickup[0x280]={};picks=pickup;at<unsigned>(player,0xE1C)=action;at<unsigned char>(player,0x370)=jingle;
for(int i=0;i<count;i++){void*p=pickup+i*0x140;at<unsigned short>(p,0x38)=type;at<unsigned short>(p,0x46)=i==0?flags:0;at<unsigned short>(p,0xDC)=radius;at<int>(p,8)=i==0?x:0;at<int>(p,12)=i==0?y:0;at<int>(p,16)=i==0?z:0;at<void*>(p,0x20)=i+1<count?pickup+(i+1)*0x140:nullptr;}
*(void**)0x60FB94=count?pickup:nullptr;collect(player);std::cout<<"{\"jingle\":"<<int(at<unsigned char>(player,0x370))<<",\"flash\":["<<int(at<unsigned char>(pickup,0x124))<<','<<int(at<unsigned char>(pickup+0x140,0x124))<<"],\"events\":";output();std::cout<<"}\n";
}else if(family==1){int health,max,amount,revision;unsigned clock,oldclock;std::cin>>health>>max>>amount>>revision>>clock>>oldclock;
at<short>(player,0xE2)=health;at<int>(player,0xEF0)=max;at<int>(player,0x5D0)=revision;at<unsigned>(player,0x5E0)=oldclock;*(unsigned*)0x6B4CA8=clock;
int result=reinterpret_cast<int(__attribute__((thiscall))*)(void*,int)>(0x4BD750)(player,amount);
std::cout<<'['<<result<<','<<at<short>(player,0xE2)<<','<<at<int>(player,0x5D0)<<','<<at<unsigned>(player,0x5E0)<<"]\n";
}else{int active,current,field,amount,difficulty,low,processing,suit;std::cin>>active>>current>>field>>amount>>difficulty>>low>>processing>>suit;
at<unsigned char>(player,0x5E9)=field;at<int>(player,0x5EC)=amount;*(int*)0x54D474=difficulty;*(unsigned char*)0x6A9040=current;*(int*)0x6B78F8=low;*(unsigned char*)0x6A9041=processing;*(int*)0x5559DC=suit;*(void**)0x60F754=(void*)0xABCD;
int result=reinterpret_cast<unsigned char(__attribute__((thiscall))*)(void*,bool)>(0x4BAEC0)(player,active);
std::cout<<"{\"state\":["<<result<<','<<int(at<unsigned char>(player,0x5E9))<<','<<int(*(unsigned char*)0x6A9040)<<','<<at<int>(player,0x5EC)<<','<<int(*(unsigned char*)0x6A9041)<<','<<int(*(void**)0x60F754!=nullptr)<<"],\"events\":";output();std::cout<<"}\n";
}}
}
'''
    collection = [[action,jingle,kind,flags,128,*point,result,count]
                  for action,jingle,kind,flags,point,result,count in itertools.product(
                      [1,16,2,0x100], [0,1,7], [8,10,11,12,13,14,15,16,18], [0,0x40],
                      [(0,0,0),(127<<12,0,0),(128<<12,0,0),(-128<<12,0,0),(90<<12,90<<12,0),
                       (-1,-1,-1),(2147483647,-2147483648,1)], [0,1], [0,1,2])]
    health = [[a,b,c,d,e,f] for a,b,c,d,e,f in itertools.product([-1,0,1,49,99,100,32767],[100,32768],
              [-32769,-100,0,1,50,65536],[0,2147483647],[0,0xFFFFFFFF],[17])]
    armor = [list(row) for row in itertools.product([0,1],[0,1],[0,1],[77],[-1,0,1,2,3,4],[0,1],[0,1],[0,7])]
    rows = [(0,'collection',row) for row in collection]+[(1,'health',row) for row in health]+[(2,'armor',row) for row in armor]
    with tempfile.TemporaryDirectory(prefix='openspidey-powerup-player-') as folder:
        cpp=Path(folder)/'reference.cpp';binary=Path(folder)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-O0','-fwrapv','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        lines=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(str(family)+' '+' '.join(map(str,row)) for family,_,row in rows)+'\n').encode()).decode().splitlines()
    fixtures=[dict(kind=kind,input=row,expected=json.loads(line)) for (_,kind,row),line in zip(rows,lines,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-powerup.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Captured {len(collection)} complete original collection, {len(health)} health and {len(armor)} armor cases.')


if __name__=='__main__':
    main()
