"""Capture actual PC module load/unload/function dispatch with original module table/initializers and CRC."""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP=r'''
#include "original_executable.h"
#include <iostream>
#include <cstdlib>
#include <vector>
struct Module {void(*clear)();unsigned unused,crc;void(*functions[8])(unsigned*,unsigned*);Module*next,*previous;};
std::vector<unsigned> events;Module*&head=*reinterpret_cast<Module**>(0x682b3c);
void*alloc(unsigned bytes,int,int,int,int){return std::calloc(1,bytes);}void release(void*p){std::free(p);}
void clear(unsigned family){events.push_back(1);events.push_back(family);int count=0;for(auto*m=head;m;m=m->next)count++;events.push_back(count);for(auto*m=head;m;m=m->next)events.push_back(m->crc);}
void catClear(){clear(319);}void thugClear(){clear(304);}
void create(unsigned family,unsigned*args,unsigned*result){events.push_back(2);events.push_back(family);events.push_back(args[0]);events.push_back(args[1]);*result=family+args[0]+args[1];}
void catCreate(unsigned*a,unsigned*r){create(319,a,r);}void thugCreate(unsigned*a,unsigned*r){create(304,a,r);}
void diagnostic(int condition,const char*,...){if(!condition)events.push_back(99);}
void patch(unsigned a,void*t){auto*p=(unsigned char*)a;p[0]=0xe9;*(unsigned*)(p+1)=(unsigned)t-a-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4581a0,(void*)&alloc);patch(0x458210,(void*)&release);patch(0x4138d0,(void*)&catClear);patch(0x4d2460,(void*)&thugClear);patch(0x413900,(void*)&catCreate);patch(0x4d2490,(void*)&thugCreate);patch(0x4015b0,(void*)&diagnostic);
const char*names[]={"thug","blackcat"};int count;
while(std::cin>>count){assert(!head);std::cout<<'[';for(int i=0;i<count;i++){int opcode,name,slot,a,b;std::cin>>opcode>>name>>slot>>a>>b;events.clear();unsigned arguments[]={unsigned(a),unsigned(b)},result=0xffffffff;
if(opcode==0)reinterpret_cast<void(*)(const char*,int)>(0x47cee0)(names[name],1);
else if(opcode==1)reinterpret_cast<void(*)(const char*)>(0x47d3a0)(names[name]);
else reinterpret_cast<void(*)(const char*,unsigned,unsigned*,unsigned*)>(0x47d470)(names[name],slot,arguments,&result);
if(i)std::cout<<',';std::cout<<'['<<result<<",[";int j=0;for(auto*m=head;m;m=m->next){if(j++)std::cout<<',';std::cout<<'['<<m->crc<<','<<(m->functions[0]!=nullptr)<<']';}std::cout<<"],[";for(unsigned k=0;k<events.size();k++){if(k)std::cout<<',';std::cout<<events[k];}std::cout<<"]]";}
std::cout<<"]\n";while(head)reinterpret_cast<void(*)(unsigned)>(0x47d410)(head->crc);}
}
'''

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent;rng=random.Random(0x47CEE0)
    cases=[[[0,0,0,0,0],[0,1,0,0,0],[0,0,0,0,0],[2,0,0,12,34],[2,1,0,56,78],[2,1,7,0,0],[1,0,0,0,0],[1,1,0,0,0],[1,1,0,0,0]]]
    for _ in range(100):cases.append([[rng.randrange(3),rng.randrange(2),rng.choice([0,1,7]),rng.randrange(1000),rng.randrange(1000)]for _ in range(16)])
    with tempfile.TemporaryDirectory(prefix='openspidey-modules-') as folder:
        cpp,binary=Path(folder)/'reference.cpp',Path(folder)/'reference';cpp.write_text(CPP)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        result=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(str(len(case))+' '+' '.join(str(v)for op in case for v in op)for case in cases)+'\n').encode())
    fixtures=[dict(input=case,expected=json.loads(line))for case,line in zip(cases,result.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/source-modules.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Captured {len(cases)} actual PC module ownership sequences, original table/initializers/CRC/dispatch retained; only factory/clear/allocator leaves traced.')

if __name__=='__main__':main()
