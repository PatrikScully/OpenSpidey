"""Capture original PC 416B10 item hiding over two frames; only the line query transport is scripted."""
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
#include <iostream>
#include <cmath>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
alignas(4)unsigned char items[10][0x40];
struct Hit{int item,length,x,y,z;};std::vector<Hit>script;unsigned cursor;std::vector<long long>events;int overflows;
int identity(void*p){for(int i=0;i<10;i++)if(p==items[i])return i+1;return p?-1:0;}
void diagnostic(int condition,const char*message){if((condition&255)==0){if(!strcmp(message,"Overflow"))overflows++;else assert(!strcmp(message,"No valid tripod"));}}
void init(int*line){}
void query(int*line,int flag){assert(flag==1);events.insert(events.end(),{line[0],line[1],line[2],line[3],line[4],line[5]});
 Hit hit=cursor<script.size()?script[cursor]:Hit{0,0,0,0,0};cursor++;
 at<void*>(line,0x68)=hit.item?items[hit.item-1]:nullptr;at<int>(line,0x44)=hit.length;at<int>(line,0x6C)=hit.x;at<int>(line,0x70)=hit.y;at<int>(line,0x74)=hit.z;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4015B0,(void*)&diagnostic);patch(0x4524C0,(void*)&init);patch(0x4549A0,(void*)&query);
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
int count;while(std::cin>>count){std::memset(items,0,sizeof items);std::memset((void*)0x56F2D0,0,32);std::memset((void*)0x56F350,0,32);events.clear();overflows=0;
 for(int i=0;i<10;i++){int flags,model,region;std::cin>>flags>>model>>region;at<unsigned short>(items[i],4)=flags;at<unsigned short>(items[i],26)=model;at<unsigned char>(items[i],31)=region;}
 int rotation[4];for(int&r:rotation)std::cin>>r;
 alignas(4)unsigned char camera[0x300]={},tripod[0x100]={},player[0x1000]={};
 at<unsigned>(camera,0x2A0)=16;at<void*>(camera,0xFC)=tripod;at<unsigned char>(camera,0x100)=1;mem<int>(0x5FAE98)=0;at<unsigned char>(camera,0xF9)=1;
 at<int>(camera,0x104)=-19268<<12;at<int>(camera,0x108)=4851<<12;at<int>(camera,0x10C)=-40748<<12;
 for(int i=0;i<4;i++){at<int>(camera,0x1E4+4*i)=rotation[i];at<int>(camera,0x1F4+4*i)=rotation[i];}
 at<int>(camera,0x1D0)=400;mem<int>(0x54885C)=400;mem<int>(0x56F260)=0;mem<int>(0x56F264)=0;mem<int>(0x56F268)=0;mem<void*>(0x6A9038)=player;
 std::cout<<'[';
 for(int frame=0;frame<2;frame++){
  int hits;std::cin>>hits;script.assign(hits,{});for(Hit&h:script)std::cin>>h.item>>h.length>>h.x>>h.y>>h.z;cursor=0;
  int held;std::cin>>held;at<void*>(player,3656)=held?items[held-1]:nullptr;
  if(frame==1)for(int i=0;i<10;i++){int model,region;std::cin>>model>>region;at<unsigned short>(items[i],26)=model;at<unsigned char>(items[i],31)=region;}
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416B10)(camera);
  if(frame)std::cout<<',';std::cout<<'['<<overflows;
  for(int i=0;i<8;i++)std::cout<<','<<identity(at<void*>(camera,388+4*i));
  for(int i=0;i<8;i++)std::cout<<','<<mem<int>(0x56F2D0+4*i)<<','<<mem<int>(0x56F350+4*i);
  for(int i=0;i<10;i++)std::cout<<','<<at<unsigned short>(items[i],4);
  std::cout<<",["; for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";events.clear();overflows=0;
 }
 std::cout<<"]\n";
}}
'''
    rng = random.Random(0x4174E9)
    cases = []

    def script(length_choices):
        hits = []
        for _ in range(rng.choice([0, 1, 2, 3, 5, 8, 9, 11])):
            hits.append([rng.randint(1, 10), rng.choice(length_choices), *[rng.randint(-2 ** 28, 2 ** 28) for _ in range(3)]])
        return hits

    for _ in range(600):
        items = [[rng.choice([0, 0x800, 0x8FF, 0xF7FF, rng.randrange(65536)]), rng.randrange(65536), rng.randrange(256)] for _ in range(10)]
        rotation = rng.choice([[0, 0, 0, 4096], [0, 2896, 0, 2896], [1402, -216, 1327, -3784]])
        first, second = script([0, 4, 5, 100]), script([0, 4, 5, 100])
        changes = [[model if rng.random() < 0.7 else rng.randrange(65536), region if rng.random() < 0.8 else rng.randrange(256)]
                   for _, model, region in items]
        cases.append(dict(items=items, rotation=rotation, frames=[
            dict(hits=first, held=rng.choice([0, 0, rng.randint(1, 10)])),
            dict(hits=second, held=rng.choice([0, 0, rng.randint(1, 10)]), changes=changes)]))
    rows = []
    for case in cases:
        row = [len(case['items'])] + [value for item in case['items'] for value in item] + case['rotation']
        for index, frame in enumerate(case['frames']):
            row += [len(frame['hits'])] + [value for hit in frame['hits'] for value in hit] + [frame['held']]
            if index == 1:
                row += [value for change in frame['changes'] for value in change]
        rows.append(row)
    with tempfile.TemporaryDirectory(prefix='openspidey-original-camera-items-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in rows) + '\n')
        if completed.returncode: raise RuntimeError(f'Original camera item fixture failed{completed.returncode}: {completed.stderr}')
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    for case, line in zip(cases, output, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-hidden-items.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} two-frame original PC camera item-hiding cases: slot release, held-item retention, generation checks, chained rays and overflow.')


if __name__ == '__main__': main()
