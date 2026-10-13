"""Capture genuine CSpark constructor/Move and whole CBaddy4292 with real spark setters, trigonometry and head insertion."""
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
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void log(const char*,...){ }
void __attribute__((naked))initTrig(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x40,%esp;xorl %ebx,%ebx;movl $0x46CCE6,%eax;call *%eax;addl $0x40,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
std::vector<int> randomEvents,allocations;std::vector<std::array<unsigned char,76>>pool;
int randomCursor,allocationCursor,failedIndex;unsigned char*caller,*origin;
int randomValue(int bound){int divisor=bound<0?-bound:bound;int value=divisor?(randomCursor*7+1)%divisor:0;randomCursor++;randomEvents.insert(randomEvents.end(),{bound,value});return value;}
void*allocate(unsigned size){assert(size==76);int index=allocationCursor++;allocations.insert(allocations.end(),{int(size),*(int*)0x547E40,*(int*)0x56EB48,int(at<unsigned char*>(caller,0x24C)-origin),index==failedIndex});return index==failedIndex?nullptr:pool.at(index).data();}
void values(const std::vector<int>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
void spark(void*p){std::cout<<'[';for(unsigned i=0;i<9;i++){if(i)std::cout<<',';std::cout<<at<int>(p,0x10+i*4);}std::cout<<','<<at<short>(p,0xC)<<','<<at<unsigned short>(p,0xE)<<','<<at<unsigned>(p,0x3C)<<','<<unsigned(at<unsigned char>(p,0x43))<<','<<at<unsigned>(p,0x44);
 for(unsigned offset:{0x40u,0x41u,0x42u,0x48u,0x49u,0x4Au,0x34u,0x35u,0x36u,0x37u})std::cout<<','<<unsigned(at<unsigned char>(p,offset));std::cout<<']';}
void settings(){std::cout<<'[';for(unsigned base:{0x56E9B8u,0x56EB38u})for(int i=0;i<3;i++){if(base!=0x56E9B8||i)std::cout<<',';std::cout<<*(short*)(base+2*i);}std::cout<<','<<*(unsigned*)0x547EB8<<','<<unsigned(*(unsigned char*)0x547EBC);for(unsigned base:{0x547EC0u,0x547EC4u})for(int i=0;i<3;i++)std::cout<<','<<unsigned(*(unsigned char*)(base+i));std::cout<<']';}
void setSettings(long long*v){for(unsigned base:{0x56E9B8u,0x56EB38u})for(int i=0;i<3;i++)*(short*)(base+2*i)=*v++;*(unsigned*)0x547EB8=*v++;*(unsigned char*)0x547EBC=*v++;for(unsigned base:{0x547EC0u,0x547EC4u})for(int i=0;i<3;i++)*(unsigned char*)(base+i)=*v++;}
int indexOf(void*p){for(unsigned i=0;i<pool.size();i++)if(p==pool[i].data())return i;assert(false);return -1;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46CD51=0xC3;initTrig();assert(*(short*)0x610C48==0&&*(short*)0x610C4A==4096);
 patch(0x4E5DA0,(void*)&randomValue);patch(0x4015B0,(void*)&log);patch(0x4088A0,(void*)&allocate);
 // Only Windows SEH reads/writes are omitted from the whole original constructors/dispatch.
 for(auto range:std::vector<std::pair<unsigned,int>>{{0x40F487,6},{0x40F48E,7},{0x40F667,7},{0x4050B0,6},{0x4050BE,7},{0x406ACB,7}})std::memset((void*)range.first,0x90,range.second);
 int family;while(std::cin>>family){randomEvents.clear();allocations.clear();*(void**)0x56E9E0=nullptr;
  if(family==0){long long v[24];for(auto&x:v)std::cin>>x;alignas(4)unsigned char raw[76]={};int position[]={int(v[0]),int(v[1]),int(v[2])};setSettings(v+3);randomCursor=v[20];*(int*)0x56EB48=v[22];*(int*)0x547E40=v[23];
   reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*,int,int,int)>(0x40F480)(raw,position,v[17],v[18],v[19]);assert(*(void**)0x56E9E0==raw&&*(int*)0x56EB48==int(v[22])+1);
   std::cout<<"{\"random\":";values(randomEvents);std::cout<<",\"states\":[";spark(raw);
   for(int i=0;i<v[21];i++){if(!at<unsigned char>(raw,0x37))reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x40F6D0)(raw);std::cout<<',';spark(raw);}std::cout<<"]}\n";
  }else if(family==1){long long v[18];for(auto&x:v)std::cin>>x;alignas(4)unsigned char raw[76]={};for(int i=0;i<9;i++)at<int>(raw,0x10+i*4)=v[i];at<short>(raw,0xC)=v[9];at<unsigned short>(raw,0xE)=v[10];for(int i=0;i<3;i++){at<unsigned char>(raw,0x40+i)=v[11+i];at<unsigned char>(raw,0x48+i)=v[14+i];at<unsigned char>(raw,0x34+i)=3;}
   std::cout<<'[';for(int i=0;i<v[17];i++){if(i)std::cout<<',';reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x40F6D0)(raw);spark(raw);}std::cout<<"]\n";
  }else{long long v[25];for(auto&x:v)std::cin>>x;alignas(4)unsigned char raw[0x300]={},bytes[32]={};caller=raw;origin=bytes;at<unsigned short>(bytes,0)=v[0];at<unsigned char*>(raw,0x24C)=bytes;
   *(int*)0x56EB48=v[1];*(int*)0x547E40=v[2];for(int i=0;i<3;i++){at<int>(raw,8+i*4)=v[3+i];at<short>(raw,0x14+i*2)=v[6+i];}setSettings(v+9);randomCursor=v[23];failedIndex=v[24];allocationCursor=0;pool.clear();pool.resize(v[0]);
   int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(raw,0x4292);
   std::cout<<"{\"return\":["<<result<<','<<int(at<unsigned char*>(raw,0x24C)-origin)<<','<<*(int*)0x56EB48<<','<<*(int*)0x547E40<<"],\"settings\":";settings();std::cout<<",\"allocations\":";values(allocations);std::cout<<",\"random\":";values(randomEvents);
   std::cout<<",\"sparks\":[";bool first=true;for(int i=0;i<allocationCursor;i++)if(i!=failedIndex){if(!first)std::cout<<',';first=false;spark(pool[i].data());}
   std::cout<<"],\"headOrder\":[";first=true;for(void*p=*(void**)0x56E9E0;p;p=at<void*>(p,8)){if(!first)std::cout<<',';first=false;std::cout<<indexOf(p);}std::cout<<"]}\n";
  }
 }}
'''
    rng = random.Random(0x40F480)
    constructors = []
    for cone in [(0, 0, 0), (512, 4096, 0), (1, 3, 7), (32767, 32767, 32767), (-2, -3, -32768)]:
        for trajectory in [(0, 0, 0), (-32768, 32767, -1), (1024, 2048, 3072)]:
            for seed in [0, 1, 9]:
                constructors.append([12345, -23456, 34567, *trajectory, *cone, 0x10001, 1,
                                     128, 128, 128, 4, 4, 4, 32, 8192, 32, seed, 35, 0, 1])
    for _ in range(160):
        constructors.append([*[rng.randint(-2147483648, 2147483647) for _ in range(3)],
            *[rng.randint(-32768, 32767) for _ in range(3)], *[rng.choice([0, 1, 512, 4096, 32767]) for _ in range(3)],
            rng.choice([0, 1, 2, 0x10001, 0x20003, 0xFFFFFFFF]), rng.randrange(2), *[rng.randrange(256) for _ in range(6)],
            rng.choice([-2147483648, -32, 0, 1, 32, 2147483647]), rng.randint(-2147483648, 2147483647),
            rng.choice([0, 1, 2, 32, 32768, 65536]), rng.randrange(16), 6, rng.randrange(200), rng.randrange(2)])
    moves = [[12345, -23456, 34567, 32767, -32768, -1, 0, 8192, 0, age, lifetime, 1, 2, 3, 4, 5, 255, 3]
             for age in [-32768, -1, 0, 32766, 32767] for lifetime in [0, 1, 32, 32767, 32768, 65535]]
    for _ in range(120):
        moves.append([*[rng.randint(-2147483648, 2147483647) for _ in range(9)], rng.randint(-32768, 32767),
                      rng.randrange(65536), *[rng.randrange(256) for _ in range(6)], 3])
    initial_settings = [-11, 123, -345, 2, 4, 6, 0x20003, 0, 17, 29, 43, 3, 5, 7]
    commands = [[count, live, 77, 12345, -23456, 34567, 123, -456, 789, *initial_settings, seed, failed]
                for count in [0, 1, 2, 8, 201] for live in [-1, 0, 199, 200, 201]
                for seed in [0, 1] for failed in [-1, 0]]
    commands += [[count, 200, 88, 1, 2, 3, 1024, 2048, 3072, *initial_settings, 9, -1]
                 for count in [0x2000, 0x8000, 0xFFFF]]
    assert all(len(v) == 24 for v in constructors) and all(len(v) == 18 for v in moves) and all(len(v) == 25 for v in commands)
    groups = [(0, constructors), (1, moves), (2, commands)]
    rows = [(family, row) for family, values in groups for row in values]
    with tempfile.TemporaryDirectory(prefix='openspidey-source-spark-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, [family, *row])) for family, row in rows) + '\n').encode()).decode().splitlines()
    fixture = [dict(family=family, input=row, expected=json.loads(line)) for (family, row), line in zip(rows, lines, strict=True)]
    destination = root / 'modern/OpenSpidey.Tests/Fixtures/source-sparks.json'
    destination.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(constructors)} whole original CSpark constructors, {len(moves)} exact signed16 age/unsigned16 life Move boundaries and {len(commands)} whole4292 batches, with genuine trigonometry, half-cone RNG, once-only signed BitCount gate and source usage/head order.')


if __name__ == '__main__':
    main()
