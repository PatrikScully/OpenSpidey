"""Capture whole original SendKillFromNode and box sweeps with real CBody IsDead/Die and explicit virtual/destructor boundaries."""
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
#include <array>
#include <iostream>
#include <vector>
template<class T>T& at(void*raw,unsigned offset){return *reinterpret_cast<T*>(static_cast<unsigned char*>(raw)+offset);}
unsigned& ptr(unsigned address){return *reinterpret_cast<unsigned*>(address);}
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
constexpr unsigned globals[5]={0x56E990,0x56E994,0x60DAAC,0x60FB94,0x60DAB4};
std::array<std::array<unsigned char,256>,21>bodies{};
std::array<std::array<unsigned char,32>,2>environment{};
std::array<std::array<unsigned char,128>,3>specials{};
std::vector<int>events;int mutations[21]{},groups[21]{};bool deleted[21]{};
int identity(void*body){for(int i=0;i<21;i++)if(body==bodies[i].data())return i;assert(false);return -1;}
void diagnostic(unsigned char condition,const char*){assert(condition);}
void*findEnvironment(unsigned checksum){events.insert(events.end(),{30,static_cast<int>(checksum)});return checksum==0x11111111?environment[0].data():checksum==0x22222222?environment[1].data():nullptr;}
void __attribute__((thiscall)) die(void*body){int id=identity(body);events.insert(events.end(),{10,id});
 if(mutations[id]!=4)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4606F0)(body);
 if(mutations[id]==1)at<void*>(body,0x20)=nullptr;
 if(mutations[id]==2){void*next=at<void*>(body,0x20);if(next)at<void*>(body,0x20)=at<void*>(next,0x20);}
 if(mutations[id]==3&&!deleted[20]&&ptr(globals[1])!=reinterpret_cast<unsigned>(bodies[20].data())){
  at<void*>(bodies[20].data(),0x20)=reinterpret_cast<void*>(ptr(globals[1]));ptr(globals[1])=reinterpret_cast<unsigned>(bodies[20].data());}
}
void* __attribute__((thiscall)) destroy(void*body,unsigned flags){assert(flags==1);int id=identity(body);events.insert(events.end(),{20,id});deleted[id]=true;
 unsigned&head=ptr(globals[groups[id]]);if(head==reinterpret_cast<unsigned>(body))head=at<unsigned>(body,0x20);
 else for(void*cur=reinterpret_cast<void*>(head);cur;cur=at<void*>(cur,0x20))if(at<void*>(cur,0x20)==body){at<void*>(cur,0x20)=at<void*>(body,0x20);break;}
 at<void*>(body,0x20)=nullptr;return body;
}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4015B0,reinterpret_cast<void*>(&diagnostic));patch(0x4C9230,reinterpret_cast<void*>(&findEnvironment));
 std::array<void*,4>vtable={reinterpret_cast<void*>(&destroy),reinterpret_cast<void*>(&die),nullptr,nullptr};
 int kind,alignment,selected;std::array<int,3>minimum{},maximum{};
 while(std::cin>>kind>>alignment>>selected>>minimum[0]>>minimum[1]>>minimum[2]>>maximum[0]>>maximum[1]>>maximum[2]){
  events.clear();bodies={};environment={};specials={};for(int i=0;i<21;i++){deleted[i]=false;groups[i]=i<20?i/4:1;at<void*>(bodies[i].data(),0)=vtable.data();
   unsigned node,flags;std::cin>>node>>flags>>at<int>(bodies[i].data(),8)>>at<int>(bodies[i].data(),12)>>at<int>(bodies[i].data(),16)>>mutations[i];
   at<unsigned short>(bodies[i].data(),0xDE)=node;at<unsigned short>(bodies[i].data(),0x46)=flags;
   if(i<20&&i%4!=3)at<void*>(bodies[i].data(),0x20)=bodies[i+1].data();}
  for(int i=0;i<5;i++)ptr(globals[i])=reinterpret_cast<unsigned>(bodies[i*4].data());
  std::array<std::array<unsigned char,64>,9>nodes{};std::array<void*,9>offsets{};
  for(int i=0;i<9;i++)offsets[i]=nodes[i].data()+alignment;
  auto*source=offsets[0];at<unsigned short>(source,0)=6;at<unsigned short>(source,2)=8;for(int i=0;i<8;i++)at<unsigned short>(source,4+i*2)=i+1;
  for(int i=1;i<9;i++){at<unsigned short>(offsets[i],0)=i==1||i==2||i==7?1:i==3?2:i==4?9:i==5?5:i==6?20:7;at<unsigned short>(offsets[i],2)=i==7?409:304;}
  for(int i=3;i<=4;i++){at<unsigned short>(offsets[i],2)=0;unsigned offset=alignment?6:4;at<unsigned>(offsets[i],offset)=i==3?0x11111111:0x22222222;}
  at<unsigned short>(environment[0].data(),4)=0x400;at<unsigned short>(environment[1].data(),4)=0x80;
  for(int i=0;i<3;i++){at<unsigned char>(specials[i].data(),0x3B)=i==1?8:9;at<unsigned short>(specials[i].data(),0x6A)=i==2?0:7;if(i<2)at<void*>(specials[i].data(),8)=specials[i+1].data();}
  ptr(0x56EB34)=reinterpret_cast<unsigned>(specials[0].data());ptr(0x6B466C)=reinterpret_cast<unsigned>(offsets.data());ptr(0x6B4670)=9;
  int count=-1;if(kind==0)reinterpret_cast<void(*)(int,int)>(0x4E3190)(0,0);
  else if(kind==1)count=reinterpret_cast<int(*)(const void*,const void*)>(0x4E69D0)(minimum.data(),maximum.data());
  else count=reinterpret_cast<int(*)(const void*,const void*,void*,bool)>(0x4E6940)(minimum.data(),maximum.data(),reinterpret_cast<void*>(ptr(globals[selected])),selected!=4);
  std::cout<<"{\"count\":"<<count<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}
  std::cout<<"],\"flags\":[";for(int i=0;i<21;i++){if(i)std::cout<<',';std::cout<<at<unsigned short>(bodies[i].data(),0x46);}
  std::cout<<"],\"deleted\":[";for(int i=0;i<21;i++){if(i)std::cout<<',';std::cout<<(deleted[i]?1:0);}
  std::cout<<"],\"environmentFlags\":["<<at<unsigned short>(environment[0].data(),4)<<','<<at<unsigned short>(environment[1].data(),4)<<"],\"specialDead\":[";
  for(int i=0;i<3;i++){if(i)std::cout<<',';std::cout<<int(at<unsigned char>(specials[i].data(),0x37));}
  std::cout<<"],\"orders\":[";for(int i=0;i<5;i++){if(i)std::cout<<',';std::cout<<'[';bool first=true;
   for(void*body=reinterpret_cast<void*>(ptr(globals[i]));body;body=at<void*>(body,0x20)){if(!first)std::cout<<',';first=false;std::cout<<identity(body);}std::cout<<']';}
  std::cout<<"]}\n";
 }
}
'''
    rng = random.Random(0x4E69D0)
    cases = []
    for kind in [0, 1, 2]:
        for mutation in [0, 1, 2, 3, 4]:
            for flags in [0x16, 0x56, 0x96]:
                bodies = [dict(node=[1, 2, 5, 6][index % 4], flags=flags, position=[0, 0, 0], mutation=0) for index in range(21)]
                bodies[0]['mutation'] = mutation
                bodies[20] = dict(node=2, flags=0x16, position=[0, 0, 0], mutation=0)
                cases.append(dict(kind=kind, alignment=2 if flags == 0x96 else 0, selected=4, minimum=[0, 0, 0], maximum=[0, 0, 0], bodies=bodies))
    for _ in range(120):
        kind = rng.randrange(3)
        bodies = [dict(node=rng.choice([0, 1, 2, 5, 6, 7, 8, 65535]), flags=rng.choice([0, 0x16, 0x40, 0x80, 0x20, 0x56]),
                       position=[rng.choice([-11, -10, -1, 0, 1, 10, 11, -2147483648, 2147483647]) for _ in range(3)], mutation=0) for _ in range(21)]
        bodies[0]['mutation'] = rng.randrange(5)
        bodies[20] = dict(node=2, flags=0x16, position=[0, 0, 0], mutation=0)
        cases.append(dict(kind=kind, alignment=rng.choice([0, 2]), selected=rng.randrange(5), minimum=[-10, -10, -10], maximum=[10, 10, 10], bodies=bodies))
    rows = []
    for case in cases:
        values = [case['kind'], case['alignment'], case['selected'], *case['minimum'], *case['maximum']]
        for body in case['bodies']: values.extend([body['node'], body['flags'], *body['position'], body['mutation']])
        rows.append(' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-actor-death-') as folder:
        source, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        source.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(source), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    fixture = dict(sourceSendKill='4E3190', sourceWholeBox='4E69D0', sourceBox='4E6940', sourceBaseDie='4606F0', sourceIsDead='460700',
                   boundaries=[dict(input=case, expected=json.loads(line)) for case, line in zip(cases, output.decode().splitlines(), strict=True)])
    (root/'modern/OpenSpidey.Tests/Fixtures/mission-actor-death.json').write_text(json.dumps(fixture, separators=(',', ':'))+'\n')
    print(f"Captured {len(cases)} whole original mission death/list sweeps, including actual CBody death tests, source virtual/destructor calls, next-pointer mutation and PC five-list ordering.")


if __name__ == '__main__':
    main()
