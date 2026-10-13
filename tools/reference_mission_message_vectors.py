"""Capture actual PC message creation, ownership, clock, unsigned lifetime and display-font arguments."""
import argparse
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
#include <cstdlib>
#include <cstring>
struct Message {char*text;unsigned short time,total;unsigned char pad[8];void*program;Message*next,*previous;};
struct Simple {char*text;unsigned age,duration,x,y,scale,factor;Simple*next,*previous;};
std::vector<std::vector<long long>> draws;int live;
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
void*allocate(unsigned size,int,int,int,int){live++;return std::calloc(1,size);}void removeMemory(void*p){live--;std::free(p);}
int __attribute__((thiscall)) width(unsigned char*font,char*){assert(*reinterpret_cast<int*>(font+0x34)==1600);return 51;}
void __attribute__((thiscall)) draw(unsigned char*font,int x,int y,char*text,int flags,float depth){
 unsigned bits;std::memcpy(&bits,&depth,4);draws.push_back({(unsigned char)*text,x,y,*reinterpret_cast<int*>(font+0x34),
  *reinterpret_cast<int*>(font+4),*reinterpret_cast<int*>(font+0x14),*reinterpret_cast<int*>(font+0x18),*reinterpret_cast<int*>(font+0x1C),flags,bits});}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
void snapshot(){std::cout<<"{\"timer\":"<<word(0x60D5B0)<<",\"live\":"<<live<<",\"messages\":[";
 bool comma=false;for(Message*p=*reinterpret_cast<Message**>(0x60D59C);p;p=p->next){if(comma)std::cout<<',';comma=true;
  std::cout<<'['<<(unsigned)(unsigned char)*p->text<<','<<p->time<<','<<p->total<<','<<(p->program?1:0)<<']';}
 std::cout<<"],\"simple\":[";comma=false;for(Simple*p=*reinterpret_cast<Simple**>(0x60D5A0);p;p=p->next){if(comma)std::cout<<',';comma=true;
  std::cout<<'['<<(unsigned)(unsigned char)*p->text<<','<<p->age<<','<<p->duration<<','<<p->x<<','<<p->y<<','<<p->scale<<','<<p->factor<<']';}
 std::cout<<"],\"draws\":[";for(unsigned i=0;i<draws.size();i++){if(i)std::cout<<',';std::cout<<'[';
 for(unsigned j=0;j<draws[i].size();j++){if(j)std::cout<<',';std::cout<<draws[i][j];}std::cout<<']';}std::cout<<"]}";}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 jump(0x4581A0,reinterpret_cast<void*>(&allocate));jump(0x458210,reinterpret_cast<void*>(&removeMemory));
 jump(0x43EA60,reinterpret_cast<void*>(&width));jump(0x43E4C0,reinterpret_cast<void*>(&draw));
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;unsigned first,second,sort;int count;
 while(std::cin>>first>>second>>sort>>count){reinterpret_cast<void(*)()>(0x4587A0)();assert(live==0);word(0x60D5B0)=0;
  word(0x60D23C)=0;word(0x60D5A4)=0;word(0x60D5A6)=0;
  reinterpret_cast<void*(*)(char*,void*)>(0x458800)(const_cast<char*>("Checkpoint"),nullptr);
  reinterpret_cast<void*(*)(char*,unsigned,unsigned,unsigned)>(0x458850)(const_cast<char*>("A"),64,100,first);
  reinterpret_cast<void*(*)(char*,unsigned,unsigned,unsigned)>(0x458850)(const_cast<char*>("B"),128,120,second);
  *reinterpret_cast<unsigned short*>(0x60D5A6)=sort;
  std::cout<<'[';for(int step=0;step<count;step++){int timer;std::cin>>timer;word(0x6B4CA8)=timer;
   reinterpret_cast<void(*)()>(0x458940)();draws.clear();reinterpret_cast<void(*)()>(0x458A40)();if(step)std::cout<<',';snapshot();}
  reinterpret_cast<void(*)()>(0x4588E0)();draws.clear();reinterpret_cast<void(*)()>(0x458A40)();std::cout<<',';snapshot();
  reinterpret_cast<void(*)()>(0x4587A0)();draws.clear();reinterpret_cast<void(*)()>(0x458A40)();std::cout<<',';snapshot();std::cout<<"]\n";
 }}
'''
    clocks = [[100, 102, 130, 131, 160, 161, 200, 260, 300], [0, 0, 1, 2, 30, 31, 90, 91, 500],
              [2147483640, 2147483642, -2147483648, -2147483646, -2147483590, -2147483400]]
    durations = [0, 1, 30, 60, 125, 65535, 0xFFFFFFFF, 0xFFFFFFA6, 0xFFFFFFE2]
    cases = [dict(first=first, second=second, sort=sort, timers=timers) for first in durations
             for second in [0, 125, 0xFFFFFFFF] for timers in clocks for sort in [0, 4093, 4094, 4095]]
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-messages-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [case['first'], case['second'], case['sort'], len(case['timers']), *case['timers']])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-messages.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC message sequences; original creation/list ownership, timer math, display/style and explicit clearing remain untouched.')


if __name__ == '__main__':
    main()
