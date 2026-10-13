"""Capture untouched PC optimum-viewing-node selection and Euler fixed-camera quaternion targets."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


CPP = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
#include <vector>
struct Vec { int x,y,z; };
struct Ang { short x,y,z,pad; };
struct Node { short kind; Vec position; int occlusion; };
struct Item { unsigned char bytes[64]; };
std::vector<Node> nodes;
std::vector<int> events;
int currentNode,ray;
int& word(unsigned address) { return *reinterpret_cast<int*>(address); }
void __attribute__((naked)) initHeading(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x28,%esp;xorl %ebx,%ebx;movl $0x46cce6,%eax;call *%eax;addl $0x28,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
void patch(unsigned address,void*target){auto*code=(unsigned char*)address;code[0]=0xE9;*(unsigned*)(code+1)=(unsigned)target-address-5;}
void event(std::initializer_list<int> values){events.insert(events.end(),values);}
void position(Vec*result,int id){*result=nodes[id].position;currentNode=id;ray=0;}
void collide(unsigned char*line,int mode){
 Vec start=*(Vec*)line,end=*(Vec*)(line+12);
 event({2,currentNode,ray,start.x,start.y,start.z,end.x,end.y,end.z,mode,word(0x5FBEDC)});
 // SLineInfo.pItem is at0x68 in the original PC; actual InitLineInfo remains untouched.
 *(unsigned*)(line+0x68)=(nodes[currentNode].occlusion&(1<<ray))?1:0;ray++;
}
void __attribute__((thiscall)) fixed(void*,Vec*position,Ang*angles,unsigned short frames){
 event({3,currentNode,position->x,position->y,position->z,angles->x,angles->y,angles->z,frames});
}
void printEvents(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){
 assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46CD51=0xC3;initHeading();
 *(unsigned char*)0x4015B0=0xC3;
 std::array<unsigned char,5> originalFixed{};std::memcpy(originalFixed.data(),(void*)0x4164F0,5);
 patch(0x4E3940,(void*)&position);patch(0x4549A0,(void*)&collide);
 char mode;while(std::cin>>mode){
  std::array<unsigned char,0x400> camera{};
  if(mode=='E'){
   std::memcpy((void*)0x4164F0,originalFixed.data(),5);
   Ang angles{};int x;for(int axis=0;axis<3;axis++){std::cin>>x;((short*)&angles)[axis]=x;}
   Vec target{12345,-67890,76543};
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,Vec*,Ang*,unsigned short)>(0x4164F0)(camera.data(),&target,&angles,0);
   std::cout<<'[';for(int axis=0;axis<4;axis++){if(axis)std::cout<<',';std::cout<<*(int*)(camera.data()+0x2D4+axis*4);}std::cout<<"]\n";
  }else{
   patch(0x4164F0,(void*)&fixed);Vec points[2];for(Vec&point:points)std::cin>>point.x>>point.y>>point.z;
   int count,initialCollision;std::cin>>count>>initialCollision;nodes.assign(count,{});std::vector<short*> offsets(count);
   for(int i=0;i<count;i++){std::cin>>nodes[i].kind>>nodes[i].position.x>>nodes[i].position.y>>nodes[i].position.z>>nodes[i].occlusion;offsets[i]=&nodes[i].kind;}
   int itemCount;std::cin>>itemCount;std::array<std::vector<Item>,2> lists;
   for(auto&list:lists){list.resize(itemCount);for(int i=0;i<itemCount;i++){
    int x,y,z,flags,usable;std::cin>>x>>y>>z>>flags>>usable;auto*b=list[i].bytes;
    std::memset(b,0,64);*(Vec*)(b+8)={x,y,z};*(unsigned short*)(b+4)=flags;b[31]=usable?0:1;
    *(unsigned*)(b+32)=i+1<itemCount?(unsigned)&list[i+1]:0;
   }}
   word(0x6B2EFC)=itemCount?(unsigned)lists[0].data():0;word(0x56E990)=itemCount?(unsigned)lists[1].data():0;
   *(unsigned char*)0x6B244A=1;*(unsigned char*)(0x6B244A+68)=0;
   word(0x6B4670)=count;word(0x6B466C)=(unsigned)offsets.data();word(0x56F3B8)=(unsigned)camera.data();word(0x5FBEDC)=initialCollision;events.clear();
   int selected=reinterpret_cast<int(*)(unsigned,Vec*)>(0x419430)(2,points);
   std::cout<<'['<<selected<<','<<word(0x5FBEDC)<<',';printEvents();std::cout<<",[";
   for(unsigned list=0;list<lists.size();list++){if(list)std::cout<<',';std::cout<<'[';
    for(int i=0;i<itemCount;i++){if(i)std::cout<<',';std::cout<<*(unsigned short*)(lists[list][i].bytes+4);}std::cout<<']';}
   std::cout<<"]]\n";
  }
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x419430)
    cases = []
    for angles in [[0, 0, 0], [0, 1024, 0], [1024, 0, 0], [0, 0, 1024],
                   [4095, 4095, 4095], [-1, -1, -1], [2048, 2048, 2048]]:
        cases.append(dict(mode='E', angles=angles))
    for _ in range(150):
        cases.append(dict(mode='E', angles=[rng.randint(-32768, 32767) for _ in range(3)]))
    for distance in [511, 512, 513, 4095, 4096, 4097]:
        for occlusion in range(4):
            cases.append(dict(mode='S', points=[[0, 0, 0], [0, 0, 0]], initialCollision=0,
                              nodes=[[13, 0, 0, 512*4096, 0], [13, 0, 0, distance*4096, occlusion]]))
    cases.extend([
        dict(mode='S', points=[[0, 0, 0], [0, 0, 0]], initialCollision=1, nodes=[]),
        dict(mode='S', points=[[0, 0, 0], [0, 0, 0]], initialCollision=0,
             nodes=[[13, 0, 0, 512*4096, 0], [13, 0, 0, 1024*4096, 0], [13, 0, 0, 2048*4096, 0]]),
        dict(mode='S', points=[[0, -512*4096, 0], [0, 512*4096, 0]], initialCollision=1,
             nodes=[[0, 0, 0, 0, 0], [13, 0, 0, 512*4096, 1], [13, 0, 0, 1024*4096, 0], [13, 0, 0, 2048*4096, 0]]),
    ])
    for _ in range(240):
        points = [[rng.randint(-4000, 4000)*4096+rng.randrange(4096) for _ in range(3)] for _ in range(2)]
        midpoint = [(points[0][axis]+points[1][axis])//2 for axis in range(3)]
        nodes = [[rng.choice([0, 12, 13, 13]),
                  *[midpoint[axis]+rng.randint(-4500, 4500)*4096+rng.randrange(4096) for axis in range(3)],
                  rng.randrange(4)] for _ in range(rng.randrange(1, 18))]
        cases.append(dict(mode='S', points=points, initialCollision=rng.randrange(2), nodes=nodes))
    rows = []
    items = [[0, 0, 0, 0xA5, 1], [2147483647]*3+[0x101, 1], [-2147483647]*3+[0x1001, 1],
             [-2147483648, 0, 0, 0x2001, 1], [0, 0, 0, 0x4001, 0], [-1, 1, 0, 65535, 1]]
    for case in cases:
        values = case.get('angles')
        if values is None:
            values = [*[value for point in case['points'] for value in point], len(case['nodes']),
                      case['initialCollision'], *[value for node in case['nodes'] for value in node], len(items),
                      *[value for _ in range(2) for item in items for value in item]]
            case['items'] = items
        rows.append(case['mode']+' '+' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-optimum-camera-') as folder:
        cpp, binary = Path(folder)/'reference.cpp', Path(folder)/'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/optimum-camera.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} original PC cases: untouched419430 scoring/aim/midpoint, real4E6BC0 visibility/4524C0 line initialization and4164F0 Euler quaternion conversion.')


if __name__ == '__main__':
    main()
