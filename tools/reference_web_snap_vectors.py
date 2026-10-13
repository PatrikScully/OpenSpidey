"""Capture actual PC CWeb SwitchToSnap and mode2 AI with explicit target/particle boundary leaves."""
import json
from pathlib import Path
import subprocess
import tempfile
import argparse


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cstring>
std::vector<void*> allocations;std::vector<int> events;int accepted,type;bool expired;
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void*allocate(unsigned size){void*p=calloc(1,size);allocations.push_back(p);return p;}
void*dcAllocate(unsigned size,int,int,void*,bool){return allocate(size);}
void release(void*){} // Each bounded fixture owns and frees all allocator leaves after the original calls finish.
void*recover(unsigned*handle){if(expired)handle[0]=0;return reinterpret_cast<void*>(handle[0]);}
unsigned char __attribute__((thiscall)) tug(void*,CVector*contact,CVector*direction,void*path){events.insert(events.end(),{1,type,contact->vx,contact->vy,contact->vz,direction->vx,direction->vy,direction->vz,path?1:0});return accepted;}
unsigned char __attribute__((thiscall)) manipTug(void*target,CVector*contact,CVector*direction){return tug(target,contact,direction,nullptr);}
void __attribute__((thiscall)) firstContact(void*){events.push_back(2);}
void __attribute__((thiscall)) broken(unsigned char*web){events.push_back(3);*reinterpret_cast<unsigned short*>(web+0x46)|=0x40;}
void __attribute__((thiscall)) attachPoint(unsigned char*target,CVector*out){*out=*reinterpret_cast<CVector*>(target+8)+*reinterpret_cast<CVector*>(target+0x110);}
void __attribute__((thiscall)) deleteBoundary(void*,int){}
void output(unsigned char*web){auto*line=*reinterpret_cast<unsigned char**>(web+0x12C);auto*chain=*reinterpret_cast<unsigned char**>(web+0x130);
 std::cout<<'['<<*reinterpret_cast<int*>(web+0x104)<<','<<*reinterpret_cast<unsigned short*>(web+0x100)<<','<<*reinterpret_cast<unsigned short*>(web+0x46)<<','<<unsigned(*reinterpret_cast<int*>(web+0x7C))<<','<<*reinterpret_cast<int*>(web+0x80)<<','<<*reinterpret_cast<int*>(web+0x84)<<",[";
 if(line){auto*start=reinterpret_cast<CVector*>(line+0x44);std::cout<<start->vx<<','<<start->vy<<','<<start->vz<<','<<int(line[0x6E])<<','<<int(line[0x6F]);auto*ends=*reinterpret_cast<unsigned char**>(line+0x40);for(int i=0;i<*reinterpret_cast<int*>(line+0x3C);i++){auto*p=reinterpret_cast<CVector*>(ends+16*i);std::cout<<','<<p->vx<<','<<p->vy<<','<<p->vz;}}
 std::cout<<"],[";if(chain){for(int offset=8;offset<0x28;offset+=4){if(offset>8)std::cout<<',';std::cout<<*reinterpret_cast<int*>(chain+offset);}auto*points=*reinterpret_cast<unsigned char**>(chain+4);for(int i=0;i<*reinterpret_cast<int*>(chain+8);i++){auto*p=reinterpret_cast<int*>(points+40*i);for(int j=0;j<9;j++)std::cout<<','<<p[j];std::cout<<','<<int(points[40*i+36]);}}
 std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]";events.clear();}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4581A0,reinterpret_cast<void*>(&dcAllocate));patch(0x455390,reinterpret_cast<void*>(&allocate));patch(0x458210,reinterpret_cast<void*>(&release));
 patch(0x4583F0,reinterpret_cast<void*>(&recover));patch(0x457A00,reinterpret_cast<void*>(&manipTug));patch(0x457AA0,reinterpret_cast<void*>(&attachPoint));
 patch(0x4C70F0,reinterpret_cast<void*>(&firstContact));patch(0x4F66A0,reinterpret_cast<void*>(&broken));
 for(auto entry:std::vector<std::pair<unsigned,int>>{{0x420407,6},{0x42040E,7},{0x4204DD,7},{0x4204F3,7},
 {0x4F69F7,6},{0x4F69FE,7},{0x4F6A8A,7},{0x4F6BF7,7}})std::memset(reinterpret_cast<void*>(entry.first),0x90,entry.second);
 int distance,path,vertical,ticks,expiry;while(std::cin>>type>>accepted>>distance>>path>>vertical>>ticks>>expiry){
 expired=false;events.clear();alignas(4)unsigned char web[0x13C]={},line[0x78]={},target[0x140]={},player[0xF8]={};unsigned table[8]={};table[7]=reinterpret_cast<unsigned>(&tug);
 *reinterpret_cast<unsigned*>(target)=reinterpret_cast<unsigned>(table);*reinterpret_cast<unsigned short*>(target+0x38)=type;
 *reinterpret_cast<CVector*>(target+8)=CVector(123456,-54321,76543);*reinterpret_cast<CVector*>(target+0x110)=CVector(1024,8192,-2048);
 *reinterpret_cast<CVector*>(player+8)=*reinterpret_cast<CVector*>(target+8)+CVector(0,vertical,0);*reinterpret_cast<void**>(0x6A9038)=player;
 *reinterpret_cast<unsigned short*>(web+0x46)=0x16;*reinterpret_cast<int*>(web+0x104)=1;*reinterpret_cast<void**>(web+0x12C)=line;
 *reinterpret_cast<unsigned*>(web+0x134)=type<0?0:reinterpret_cast<unsigned>(target);*reinterpret_cast<unsigned*>(web+0x138)=999;
 CVector end(123456+(distance<<12),-54321+distance*123-321,76543+distance*321+987),start(123456,-54321,76543);*reinterpret_cast<CVector*>(web+0x114)=start;
 int segments=std::max(1,std::min(40,distance/80));auto*ends=static_cast<unsigned char*>(allocate(16*segments));
 *reinterpret_cast<int*>(line+0x3C)=segments;*reinterpret_cast<void**>(line+0x40)=ends;*reinterpret_cast<CVector*>(line+0x44)=start;
 CVector step=(end-start)/segments,current=start;for(int i=0;i<segments;i++){current+=step;*reinterpret_cast<CVector*>(ends+16*i)=current;}line[0x6E]=1;line[0x6F]=20;
 CVector direction(12345,-23456,34567),pathPoint(98765,87654,76543);
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector*,void*)>(0x4F69F0)(web,&direction,path?&pathPoint:nullptr);
 std::cout<<'[';output(web);
 unsigned destructorTable[1]={reinterpret_cast<unsigned>(&deleteBoundary)};*reinterpret_cast<void**>(line)=destructorTable;
 if(auto*chain=*reinterpret_cast<unsigned char**>(web+0x130))*reinterpret_cast<void**>(chain)=destructorTable;
 bool deleted=false;for(int frame=1;frame<=38;frame++){std::cout<<',';*reinterpret_cast<unsigned*>(0x6B4CA8)=unsigned(500+frame*ticks);if(expiry&&frame>=expiry)expired=true;
 if(!deleted){if(*reinterpret_cast<unsigned short*>(web+0x46)&0x40){deleted=true;*reinterpret_cast<void**>(web+0x12C)=nullptr;*reinterpret_cast<void**>(web+0x130)=nullptr;}else reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4F6C10)(web);}output(web);}
 std::cout<<"]\n";for(void*p:allocations)free(p);allocations.clear();
 }}
'''
    cases = [[kind, accepted, distance, path, vertical, ticks, expiry]
             for kind in [-1, 50, 304, 401] for accepted in [0, 1]
             for distance, path, vertical, ticks, expiry in [(79, 0, 0, 2, 0), (320, 1, 122879, 2, 6), (160, 0, 122880, 7, 0)]]
    with tempfile.TemporaryDirectory(prefix='openspidey-web-snap-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp),
                        str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/web-snap.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} actual PC SwitchToSnap/mode2 owner sequences; rejected particle constructors are an explicit boundary.')


if __name__ == '__main__':
    main()
