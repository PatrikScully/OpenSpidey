"""Capture whole PC4273D0 with real ENV lookup, command-point queue, constructor, and chunk initialization."""
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
#include <cmath>
#include <cstdlib>
#include <iostream>
#include <vector>
template<class T>T& at(void* raw,unsigned offset){return *reinterpret_cast<T*>(reinterpret_cast<unsigned char*>(raw)+offset);}
unsigned& ptr(unsigned address){return *reinterpret_cast<unsigned*>(address);}
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
struct Item { unsigned char bytes[64]; };
struct Entry { Item* item; int velocity[3]; int reserved; short angular[3],padding; unsigned lifetime; };
static_assert(sizeof(Entry)==32);
std::vector<void*> allocated;
std::vector<std::array<unsigned,8>> allocationBoundaries;
void* allocate(unsigned bytes){void*p=std::calloc(1,bytes);allocated.push_back(p);return p;}
void* allocateEntries(unsigned bytes,int,int,int,int){
 unsigned head=ptr(0x56E994);auto*c=reinterpret_cast<void*>(head);
 allocationBoundaries.push_back({bytes,at<unsigned short>(c,0x46),at<unsigned short>(c,0xF8),at<unsigned short>(c,0xFA),
   static_cast<unsigned>(at<int>(c,8)),static_cast<unsigned>(at<int>(c,12)),static_cast<unsigned>(at<int>(c,16)),ptr(0x6B468C)==0x5580C0});
 return allocate(bytes);
}
void diagnostic(int,const char*){}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only platform/heap boundaries change. ENV hash lookup, source trigger queue, CBody construction/AttachTo and AddChunk remain original machine code.
 patch(0x455390,reinterpret_cast<void*>(&allocate));patch(0x4581A0,reinterpret_cast<void*>(&allocateEntries));
 patch(0x4015B0,reinterpret_cast<void*>(&diagnostic));
 std::memset(reinterpret_cast<void*>(0x4273D7),0x90,6);std::memset(reinterpret_cast<void*>(0x4273DE),0x90,7);
 std::memset(reinterpret_cast<void*>(0x427754),0x90,7);
 for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;reinterpret_cast<short*>(0x610C48)[i*2]=std::sin(angle)*4096.;reinterpret_cast<short*>(0x610C48)[i*2+1]=std::cos(angle)*4096.;}
 int seed,stationary,moving,missing,repeats;
 while(std::cin>>seed>>stationary>>moving>>missing>>repeats){
  std::array<Item,5>items{};std::array<std::array<unsigned char,156>,5>models{};
  std::array<std::array<unsigned char,224>,5>faces{};std::array<std::array<unsigned char,36>,5>dc{};
  std::array<unsigned,5>checksums{};std::array<void*,5>modelPointers{};
  std::array<int,5>faceCounts{};std::array<std::array<unsigned,4>,5>faceOffsets{};
  for(int i=0;i<5;i++){
   unsigned flags,modelFlags,rendererFlags;int x,y,z,ax,ay,az,count;
   std::cin>>flags>>x>>y>>z>>ax>>ay>>az>>modelFlags>>rendererFlags>>count;
   at<unsigned short>(&items[i],4)=flags;at<int>(&items[i],8)=x;at<int>(&items[i],12)=y;at<int>(&items[i],16)=z;
   at<short>(&items[i],20)=ax;at<short>(&items[i],22)=ay;at<short>(&items[i],24)=az;at<unsigned short>(&items[i],26)=i;
   checksums[i]=0x102A9+i*0x100;modelPointers[i]=models[i].data();faceCounts[i]=count;
   at<unsigned short>(models[i].data(),0)=modelFlags;at<unsigned short>(models[i].data(),6)=count;
   at<void*>(dc[i].data(),4)=faces[i].data();at<unsigned>(dc[i].data(),12)=rendererFlags;at<unsigned>(dc[i].data(),20)=count;
   unsigned offset=28;
   for(int f=0;f<count;f++){unsigned source,collision,renderer;std::cin>>source>>collision>>renderer;
    faceOffsets[i][f]=offset;at<unsigned>(models[i].data(),offset)=source;at<unsigned>(models[i].data(),offset+12)=collision;
    at<unsigned short>(faces[i].data(),f*56)=renderer;offset+=(source>>18)*4;}
  }
  std::array<unsigned,10>chunk{};chunk[0]=1;chunk[1]=missing?0x102AA:checksums[0];chunk[2]=3; // The PC discards the offset's low two bits.
  chunk[3]=stationary|(moving<<16);int cursor=4;
  for(int i=0;i<stationary;i++)chunk[cursor++]=checksums[i+1];
  for(int i=0;i<moving;i++)chunk[cursor++]=checksums[i+3];
  ptr(0x6B2EFC)=reinterpret_cast<unsigned>(items.data());
  std::memset(reinterpret_cast<void*>(0x6AC214),0xFF,256*10);
  for(int i=0;i<5;i++)reinterpret_cast<short*>(0x6AC214+0xA9*10)[i]=i;
  std::memset(reinterpret_cast<void*>(0x6B2440),0,68);
  ptr(0x6B244C)=reinterpret_cast<unsigned>(checksums.data());ptr(0x6B2454)=reinterpret_cast<unsigned>(modelPointers.data());
  ptr(0x6B2474)=reinterpret_cast<unsigned>(chunk.data());ptr(0x5F6764)=reinterpret_cast<unsigned>(dc.data());
  std::array<std::array<unsigned char,24>,3>points{};
  std::memset(reinterpret_cast<void*>(0x6B4214),0,256*4);std::memset(reinterpret_cast<void*>(0x6B4688),0,16*8);ptr(0x56E994)=0;
  for(int i=0;i<3;i++){at<unsigned>(points[i].data(),0)=0x5580C0;at<unsigned char>(points[i].data(),5)=i==0;
   at<unsigned short>(points[i].data(),10)=172-i;at<unsigned>(points[i].data(),12)=checksums[0];
   if(i<2)at<void*>(points[i].data(),16)=points[i+1].data();}
  ptr(0x6B4214+0xA9*4)=reinterpret_cast<unsigned>(points.data());
  reinterpret_cast<void(*)(int)>(0x4E5D80)(seed);
  for(int repeat=0;repeat<repeats;repeat++)reinterpret_cast<void(*)(unsigned)>(0x4273D0)(checksums[0]);
  std::cout<<"{\"items\":[";
  for(int i=0;i<5;i++){if(i)std::cout<<',';std::cout<<"{\"flags\":"<<at<unsigned short>(&items[i],4)<<",\"modelFlags\":"<<at<unsigned short>(models[i].data(),0)
   <<",\"rendererFlags\":"<<at<unsigned>(dc[i].data(),12)<<",\"faces\":[";
   for(int f=0;f<faceCounts[i];f++){if(f)std::cout<<',';std::cout<<'['<<at<unsigned>(models[i].data(),faceOffsets[i][f])<<','
    <<at<unsigned>(models[i].data(),faceOffsets[i][f]+12)<<','<<at<unsigned short>(faces[i].data(),f*56)<<']';}std::cout<<"]}";}
  std::cout<<"],\"controls\":[";bool comma=false;
  for(auto*c=reinterpret_cast<void*>(ptr(0x56E994));c;c=at<void*>(c,0x20)){
   if(comma)std::cout<<',';comma=true;std::cout<<"{\"bodyFlags\":"<<at<unsigned short>(c,0x46)<<",\"capacity\":"<<at<unsigned short>(c,0xF8)
    <<",\"added\":"<<at<unsigned short>(c,0xFA)<<",\"node\":"<<at<unsigned short>(c,0xDE)<<",\"entries\":[";
   auto*entries=at<Entry*>(c,0xFC);for(int i=0;i<at<unsigned short>(c,0xFA);i++){if(i)std::cout<<',';auto&e=entries[i];
    std::cout<<'['<<(e.item-items.data())<<','<<e.velocity[0]<<','<<e.velocity[1]<<','<<e.velocity[2]<<','<<e.angular[0]<<','<<e.angular[1]<<','<<e.angular[2]<<','<<e.lifetime<<']';}std::cout<<"]}";}
  std::cout<<"],\"allocationBoundaries\":[";
  for(unsigned i=0;i<allocationBoundaries.size();i++){if(i)std::cout<<',';std::cout<<'[';for(int j=0;j<8;j++){if(j)std::cout<<',';std::cout<<allocationBoundaries[i][j];}std::cout<<']';}
  std::cout<<"],\"points\":[";for(int i=0;i<3;i++){if(i)std::cout<<',';std::cout<<'['<<(int)at<unsigned char>(points[i].data(),4)<<','<<(int)at<unsigned char>(points[i].data(),5)<<']';}
  std::cout<<"],\"pending\":[";comma=false;for(int i=0;i<16;i++)if(ptr(0x6B468C+i*8)){if(comma)std::cout<<',';comma=true;std::cout<<*reinterpret_cast<unsigned short*>(0x6B4688+i*8);}
  std::cout<<"],\"random\":["<<static_cast<int>(ptr(0x6B4C7C))<<','<<static_cast<int>(ptr(0x6B4B90))<<','<<static_cast<int>(ptr(0x6B4B94))<<"]}\n";
  for(void*p:allocated)std::free(p);allocated.clear();allocationBoundaries.clear();
 }
}
'''
    rng = random.Random(0x4273D0)
    cases = []
    for seed in [0, 1, -1, 0x12A6CC58]:
        for stationary, moving, missing in [(0, 0, 0), (2, 0, 0), (0, 2, 0), (2, 2, 0), (1, 1, 0), (2, 2, 1)]:
            for repeats in [1, 2]:
                items = []
                for index in range(5):
                    count = rng.randrange(5)
                    items.append(dict(flags=rng.choice([0, 0x1021, 0xFFFF, 0x8011]),
                        position=[rng.randint(-40000000, 40000000) for _ in range(3)],
                        angles=[rng.randint(-32768, 32767) for _ in range(3)],
                        modelFlags=rng.choice([0, 0x30, 0xFFFF, 0x18]),
                        rendererFlags=rng.choice([0, 0x100, 0x12345678, 0xFFFFFFFF]),
                        faces=[[rng.choice([4, 5, 8]) << 18 | rng.choice([0, 0x40, 0x80, 0xC0, 0x811]),
                            rng.choice([0, 0x10000, 0xFFFFFFFF, 0x180000]),
                            rng.choice([0, 0x40, 0x80, 0xC0, 0x810, 0xFFFF])] for _ in range(count)]))
                cases.append(dict(seed=seed, stationary=stationary, moving=moving, missing=bool(missing), repeats=repeats, items=items))
    rows = []
    for case in cases:
        values = [case['seed'], case['stationary'], case['moving'], int(case['missing']), case['repeats']]
        for item in case['items']:
            values.extend([item['flags'], *item['position'], *item['angles'], item['modelFlags'], item['rendererFlags'], len(item['faces'])])
            for face in item['faces']: values.extend(face)
        rows.append(' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-environment-chunk-producer-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
            '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = dict(originalPcProducer='4273D0', originalPcLookup='4C9230', originalPcTrigger='4DE8D0',
        boundaries=[dict(**case, expected=json.loads(row)) for case, row in zip(cases, output.decode().splitlines(), strict=True)])
    (root / 'modern/OpenSpidey.Tests/Fixtures/environment-chunk-producer.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original chunk producer boundaries with actual hash collisions, source queue/constructors, static/moving masks and random state.')


if __name__ == '__main__': main()
