"""Capture original CBaddy speech commands and whole PC Redbook_XAPlayPos/XAPlay bookkeeping."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned a){return *reinterpret_cast<T*>(a);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void omit(unsigned a,int n){std::memset((void*)a,0x90,n);}
unsigned char*body;unsigned short*words;std::vector<int>calls;int clockAdvance,plays,stops,lastTrack,lastVolume;
unsigned char global(int bank,int slot,int priority){calls={0,bank,slot,priority,(int)((unsigned char*)at<unsigned short*>(body,0x24C)-(unsigned char*)words)};return 1;}
unsigned char positional(int bank,int slot,const int*position,int priority){calls={1,bank,slot,priority,position[0],position[1],position[2],(int)((unsigned char*)at<unsigned short*>(body,0x24C)-(unsigned char*)words)};return 1;}
void start(int handle,int partition,int track){assert(handle==0&&partition==0);plays++;lastTrack=track;mem<unsigned>(0x6B4CA0)+=clockAdvance;}
void stop(int handle){assert(handle==0);stops++;}
void volume(int handle,int attenuation){assert(handle==0);lastVolume=attenuation;}
void diagnostic(int,const char*){}
void print(const std::vector<int>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);
 if(argv[2][0]=='c'){
  omit(0x4050B0,6);mem<unsigned short>(0x4050B0)=0xC031;for(unsigned a:{0x4050BEu,0x406ACBu})omit(a,7);
  patch(0x479EE0,(void*)&global);patch(0x479D30,(void*)&positional);
  int opcode,type,skip,fast,x,y,z,a,b,c;
  while(std::cin>>opcode>>type>>skip>>fast>>x>>y>>z>>a>>b>>c){
   alignas(4)unsigned char actor[0x330]={};unsigned short operands[3]={(unsigned short)a,(unsigned short)b,(unsigned short)c};body=actor;words=operands;
   at<short>(body,0x38)=type;at<unsigned char>(body,0x234)=skip;mem<unsigned char>(0x60CFC4)=fast;
   at<int>(body,8)=x;at<int>(body,12)=y;at<int>(body,16)=z;at<unsigned short*>(body,0x24C)=words;calls.clear();
   int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(body,opcode);
   std::cout<<"{\"result\":"<<result<<",\"consumed\":"<<((unsigned char*)at<unsigned short*>(body,0x24C)-(unsigned char*)words)<<",\"calls\":";print(calls);std::cout<<"}\n";
  }
 }else{
  // Keep the real command, XAPlayPos, XAPlay, priority/availability gates and bookkeeping bodies.
  // Replace only the unavailable PC audio transport and diagnostic callback; the image's actual XA table is unchanged.
  patch(0x516050,(void*)&start);patch(0x516060,(void*)&stop);patch(0x516070,(void*)&volume);patch(0x4015B0,(void*)&diagnostic);
  mem<unsigned char>(0x68277C)=1;mem<int>(0x681D2C)=0;mem<int>(0x2E09BEC)=0;mem<short>(0x6B482E)=255;
  int sequence,kind,bank,slot,priority,io,movie,advance,previous=-1;unsigned clock;
  while(std::cin>>sequence>>kind>>bank>>slot>>priority>>clock>>io>>movie>>advance){
   if(sequence!=previous){reinterpret_cast<void(*)()>(0x479940)();mem<int>(0x682780)=mem<int>(0x682784)=mem<int>(0x682788)=0;plays=stops=0;lastTrack=-1;lastVolume=999;previous=sequence;}
   mem<unsigned>(0x6B4CA0)=clock;mem<int>(0x57C400)=io;mem<int>(0x6151F8)=movie;clockAdvance=advance;int result=-1;
   if(kind==0)result=reinterpret_cast<unsigned char(*)(int,int,int)>(0x479EE0)(bank,slot,priority);
   if(kind==1){int position[3]={123,-456,789};result=reinterpret_cast<unsigned char(*)(int,int,const int*,int)>(0x479D30)(bank,slot,position,priority);}
   if(kind==2)reinterpret_cast<void(*)()>(0x479940)();
   if(kind==3)reinterpret_cast<void(*)()>(0x479E30)();
   std::cout<<'['<<result<<','<<int(mem<unsigned char>(0x682770))<<','<<mem<int>(0x550D7C)<<','<<mem<unsigned>(0x68276C)<<','
    <<mem<int>(0x681D3C)<<','<<mem<int>(0x681D40)<<','<<mem<int>(0x681D44)<<','<<mem<int>(0x682780)<<','<<mem<int>(0x682784)<<','<<mem<unsigned>(0x682788)<<','
    <<plays<<','<<stops<<','<<lastTrack<<','<<lastVolume<<','<<mem<unsigned>(0x6B4CA0)<<"]\n";
  }
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x479D30)
    commands = []
    for opcode in [0x42A3, 0x450A]:
        for actor_type in [203, 304, 305, 312, 402]:
            for skip in [0, 1, 255]:
                for fast in [0, 1, 255]:
                    commands.append(dict(opcode=opcode, actorType=actor_type, skip=skip, fast=fast,
                                         position=[123456, -654321, 314159], words=[0x8000, 0x2000, 0xFFFF]))
        for _ in range(150):
            commands.append(dict(opcode=opcode, actorType=rng.choice([203, 304, 305, 312, 402]), skip=rng.randrange(256),
                                 fast=rng.randrange(256), position=[rng.randint(-2147483648, 2147483647) for _ in range(3)],
                                 words=[rng.randrange(65536) for _ in range(3)]))
    for words in [[8, 1, 666], [13, 0, 0], [13, 3, 1], [13, 2, 2], [13, 5, 255], [13, 4, 255]]:
        commands.append(dict(opcode=0x450A, actorType=203, skip=0, fast=0, position=[123, -456, 789], words=words))
    playback = []
    authored = [[1, 8, 1, 666, 123, 0, 0, 9], [0, 13, 3, 667, 200, 0, 0, 7],
                [1, 13, 0, 666, 0xFFFFFFFF, 0, 0, 1], [2, 0, 0, 0, 50, 0, 0, 0],
                [1, 13, 0, 0, 88, 0, 0, 12], [3, 0, 0, 0, 100, 0, 0, 0],
                [1, 13, 3, 1, 120, 0, 0, 0], [1, 13, 2, 2, 130, 0, 0, 0],
                [3, 0, 0, 0, 140, 0, 0, 0], [2, 0, 0, 0, 150, 0, 0, 0]]
    for _ in range(60):
        operations = [row[:] for row in authored]
        for _ in range(55):
            bank = rng.choice([0, 8, 13, 41, 67, 78, 79])
            slot = rng.randrange(16)
            operations.append([rng.choice([0, 1, 1, 1, 2, 3]), bank, slot, rng.choice([-1, 0, 1, 10, 255, 256, 666, 667]),
                               rng.randrange(2**32), rng.randrange(2), rng.randrange(2), rng.randrange(100)])
        playback.append(dict(operations=operations))
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-speech-') as directory:
        cpp, binary = Path(directory) / 'reference.cpp', Path(directory) / 'reference'
        cpp.write_text(HARNESS)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [[c['opcode'], c['actorType'], c['skip'], c['fast'], *c['position'], *c['words']] for c in commands]
        results = subprocess.check_output([str(binary), str(args.original_exe.resolve()), 'commands'],
                                          input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines()
        for case, line in zip(commands, results, strict=True):
            case['expected'] = json.loads(line)
        rows = [[i, *op] for i, case in enumerate(playback) for op in case['operations']]
        results = iter(subprocess.check_output([str(binary), str(args.original_exe.resolve()), 'playback'],
                                              input=('\n'.join(' '.join(map(str, row)) for row in rows) + '\n').encode()).decode().splitlines())
        for case in playback:
            case['expected'] = [json.loads(next(results)) for _ in case['operations']]
        assert next(results, None) is None
    fixture = root / 'modern/OpenSpidey.Tests/Fixtures/actor-speech.json'
    fixture.write_text(json.dumps(dict(commands=commands, playback=playback), separators=(',', ':')) + '\n')
    print(f'Captured {len(commands)} whole original actor commands and {sum(len(c["operations"]) for c in playback)} actual PC XAPlayPos/XAPlay operations.')


if __name__ == '__main__':
    main()
