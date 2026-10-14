"""Capture whole original489050 tail allocation/topology and489810 ring sweep, with genuine normalize/GTE/trigonometry."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
struct Vector{int x,y,z;};struct ShortVector{short x,y,z,pad;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
void patch(unsigned address,void*target){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)target-address-5;}
void __attribute__((naked))initTrig(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x40,%esp;xorl %ebx,%ebx;movl $0x46CCE6,%eax;call *%eax;addl $0x40,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
std::array<Vector,10>hooks;std::vector<long long>events;int hookCursor,submissions,allocationCursor;
alignas(4)unsigned char geometry[3972],palette[1024],texture[44];
void append(std::vector<long long>&out,Vector v){out.insert(out.end(),{v.x,v.y,v.z});}
void geometryState(std::vector<long long>&out){for(unsigned offset:{12u,16u,20u})out.push_back(at<unsigned>(geometry,offset));
 for(unsigned base:{28u,764u})for(unsigned vertex=0;vertex<92;vertex++){auto&v=at<ShortVector>(geometry,base+vertex*8);out.insert(out.end(),{v.x,v.y,v.z});assert(v.pad==0);}}
void dynamicHook(Vector*out,void*actor,const short*hook){assert(hookCursor<10);for(int i=0;i<4;i++)events.push_back(i==3?(unsigned short)hook[i]:hook[i]);
 append(events,at<Vector>(actor,0x400));geometryState(events);*out=hooks[hookCursor++];}
void render(void*item){assert(*(int*)0x660F98==1);assert(at<unsigned short>(item,4)==0x480&&at<unsigned short>(item,0x1A)==0);submissions++;}
void*allocate(unsigned size,int,int,int,int){allocationCursor++;assert(size==1024||size==3972);return size==1024?palette:geometry;}
void*findTexture(const char*name){assert(!strcmp(name,"new_scorp_tail"));return texture;}
void read(Vector&v){std::cin>>v.x>>v.y>>v.z;}
void vector(Vector v){std::cout<<'['<<v.x<<','<<v.y<<','<<v.z<<']';}
void values(const std::vector<long long>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46CD51=0xC3;initTrig();assert(*(short*)0x610C48==0&&*(short*)0x610C4A==4096);
 patch(0x453EE0,(void*)&dynamicHook);patch(0x4739A0,(void*)&render);patch(0x4581A0,(void*)&allocate);patch(0x4C94B0,(void*)&findTexture);
 int family;while(std::cin>>family){alignas(4)unsigned char actor[0xC28]={};events.clear();hookCursor=submissions=allocationCursor=0;
  if(family==0){int ready;std::cin>>ready;at<unsigned char>(actor,0x417)=ready?0:255;at<unsigned short>(actor,0x3FC)=0x480;at<void*>(actor,0x43C)=geometry;
   for(unsigned i=0;i<23;i++)read(at<Vector>(actor,0x4BC+i*12));for(auto&v:hooks)read(v);
   std::memset(geometry,0,sizeof(geometry));for(unsigned base:{28u,764u})for(unsigned i=0;i<92;i++){Vector v;read(v);at<ShortVector>(geometry,base+i*8)={short(v.x),short(v.y),short(v.z),0};}
   *(int*)0x660F98=0;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x489810)(actor);
   std::cout<<"{\"position\":";vector(at<Vector>(actor,0x400));std::cout<<",\"geometry\":";std::vector<long long>state;geometryState(state);values(state);
   std::cout<<",\"events\":";values(events);std::cout<<",\"submissions\":"<<submissions<<",\"renderFlag\":"<<*(int*)0x660F98<<"}\n";
  }else{assert(family==1);int occupied,width,height;std::cin>>occupied>>width>>height;std::memset((void*)0x6B2440,0,40*68);for(int i=0;i<occupied;i++)*(unsigned char*)(0x6B2440+i*68)='X';
   std::memset(texture,0,sizeof(texture));texture[10]=width-1;texture[11]=height-1;std::memset(geometry,0xCD,sizeof(geometry));std::memset(palette,0xCD,sizeof(palette));at<unsigned char>(actor,0x417)=255;
   reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x489050)(actor);
   std::cout<<"{\"region\":"<<unsigned(at<unsigned char>(actor,0x417))<<",\"allocations\":"<<allocationCursor<<",\"item\":["<<at<unsigned short>(actor,0x3FC)<<','<<at<unsigned short>(actor,0x410)<<','<<at<unsigned short>(actor,0x40E)<<','<<at<unsigned short>(actor,0x40C)<<','<<at<unsigned short>(actor,0x412)<<','<<at<unsigned>(actor,0x418)<<','<<(at<unsigned>(actor,0x434)==0x5523D0)<<"],\"header\":[";
   for(unsigned offset:{0u,2u,4u,6u,24u,26u}){if(offset)std::cout<<',';std::cout<<at<unsigned short>(geometry,offset);}std::cout<<','<<at<unsigned>(geometry,8)<<"],\"palette\":[";
   for(unsigned i=0;i<256;i++){if(i)std::cout<<',';std::cout<<at<unsigned>(palette,i*4);}std::cout<<"],\"faces\":[";
   for(unsigned face=0;face<88;face++){if(face)std::cout<<',';auto*p=geometry+1500+face*28;assert(at<void*>(p,16)==texture);std::cout<<'['<<at<unsigned short>(p,0)<<','<<at<unsigned short>(p,2);for(unsigned offset:{4u,5u,6u,7u})std::cout<<','<<unsigned(p[offset]);std::cout<<','<<at<unsigned>(p,8);for(unsigned offset=20;offset<28;offset++)std::cout<<','<<unsigned(p[offset]);std::cout<<']';}std::cout<<"]}\n";
  }
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x489810)
    renders = []
    for ready in [0, 1]:
        for origin in [[0, 0, 0], [-1735 * 4096, -31 * 4096, 443 * 4096], [2147483647, -2147483648, -1]]:
            for step in [[0, 0, 0], [8192, -4096, 4096], [-8192, 4096, -4096], [2147483647, -2147483648, -1]]:
                def signed(value):
                    return (value + 2 ** 31) % (2 ** 32) - 2 ** 31
                nodes = [[signed(origin[axis] + step[axis] * node) for axis in range(3)] for node in range(23)]
                points = [nodes[0], *[[signed(nodes[0][axis] + direction[axis] * 4096) for axis in range(3)]
                    for direction in [[0, 0, 15], [15, 0, 0], [0, 0, -15], [-15, 0, 0]]], nodes[-1],
                    *[[signed(nodes[-1][axis] + direction[axis] * 4096) for axis in range(3)]
                    for direction in [[7, 0, 0], [0, 0, -7], [-7, 0, 0], [0, 0, 7]]]]
                renders.append(dict(ready=ready, nodes=nodes, hooks=points))
    for _ in range(72):
        renders.append(dict(ready=1,
            nodes=[[rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(23)],
            hooks=[[rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(10)]))
    for row in renders:
        row['vertices'] = [[rng.randint(-32768, 32767) for _ in range(3)] for _ in range(92)]
        row['normals'] = [[rng.randint(-32768, 32767) for _ in range(3)] for _ in range(92)]
    allocations = [dict(occupied=occupied, width=width, height=height)
        for occupied in [0, 1, 17, 39] for width, height in [(128, 128), (64, 32), (255, 253), (256, 256), (2, 2)]]
    requests = [[0, row['ready'], *sum(row['nodes'], []), *sum(row['hooks'], []), *sum(row['vertices'], []), *sum(row['normals'], [])] for row in renders]
    requests += [[1, row['occupied'], row['width'], row['height']] for row in allocations]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-tail-render-') as folder:
        source, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        source.write_text(HARNESS)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(source), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in requests) + '\n').encode()).decode().splitlines()
    if len(lines) != len(requests):
        raise RuntimeError('Original tail-render reference returned an incomplete fixture set.')
    for row, line in zip(renders + allocations, lines, strict=True):
        row['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-tail-render.json').write_text(
        json.dumps(dict(renders=renders, allocations=allocations), separators=(',', ':')) + '\n')
    print(f'Captured {len(renders)} whole original489810 renders with all ten hook-visible stores, SHR/SAR and midpoint wrapping; {len(allocations)} whole489050 first-free/private allocation and88-face layouts.')


if __name__ == '__main__':
    main()
