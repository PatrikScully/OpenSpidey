"""Capture PC player4B7BF0 trigger sweep and entire4549A0 grid walk at genuine geometry-call boundaries."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <cmath>
#include <iostream>
#include <vector>
struct VECTOR{int vx,vy,vz,pad;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
std::vector<int>events;int width,height;unsigned cellTokens[400];
void init(void*p){
 for(unsigned off:{0x18u,0x1Cu,0x20u,0x24u,0x28u,0x2Cu,0x6Cu,0x70u,0x74u})assert(at<int>(p,off)==0);
 for(unsigned off:{0x78u,0x7Au,0x7Cu})assert(at<short>(p,off)==0);
 at<int>(p,0x40)=at<int>(p,0x8C)=0x7FFFFFFF;at<void*>(p,0x68)=nullptr;at<void*>(p,0x80)=nullptr;
 at<int>(p,0x84)=-1;at<unsigned char>(p,0x88)=at<unsigned char>(p,0x89)=0;
 events.push_back(1);
}
void sweep(void*p,int mode){assert(mode==1&&at<unsigned char>(p,0x88)==1&&*(int*)0x5FBEE8==0);
 events.insert(events.end(),{2,mode});for(unsigned off:{0u,4u,8u,12u,16u,20u})events.push_back(at<int>(p,off));
 // A genuine callee may change the global; the player must restore it after the call.
 *(int*)0x5FBEE8=99;
}
void env(void*,void*){events.push_back(-1);}
void cell(unsigned*p,void*){int i=int(p-cellTokens);assert(i>=0&&i<width*height);events.insert(events.end(),{i%width,i/width});}
void getLine(void*){}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;add $0x4B0,%esp;ret");}
extern "C" __attribute__((naked)) void leafStep(void*){asm volatile("sub $0x4B0,%esp;push %ebx;push %ebp;push %esi;push %edi;mov 0x4C4(%esp),%esi;mov $0x4B7BF0,%eax;jmp *%eax");}
void output(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
'''
    code += controller_method((root/'src/engine/rendering/ps2funcs.cpp').read_text(), 'void VectorNormal(')
    code += r'''
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x470430,(void*)&VectorNormal);patch(0x4524C0,(void*)&init);patch(0x4B7D5A,(void*)&leafEnd);
*(unsigned char*)0x4015B0=0xC3;patch(0x4527C0,(void*)&env);patch(0x452820,(void*)&cell);patch(0x452890,(void*)&getLine);
unsigned char originalGrid[5];std::memcpy(originalGrid,(void*)0x4549A0,5);
*(unsigned char*)0x6A7F24=1;int family;
while(std::cin>>family){events.clear();if(family==0){patch(0x4549A0,(void*)&sweep);
 alignas(4)unsigned char player[0xE40]={};unsigned action;int previous[3],position[3],last[3];
 std::cin>>action;for(auto&v:previous)std::cin>>v;for(auto&v:position)std::cin>>v;for(auto&v:last)std::cin>>v;
 std::memcpy(player+8,position,12);std::memcpy(player+0xE8,previous,12);std::memcpy((void*)0x6A7EF8,last,12);
 at<unsigned>(player,0xE1C)=action;*(int*)0x5FBEE8=7;leafStep(player);
 std::cout<<"{\"direction\":["<<*(int*)0x6A7EF8<<','<<*(int*)0x6A7EFC<<','<<*(int*)0x6A7F00<<"],\"triggerGlobal\":"<<*(int*)0x5FBEE8<<",\"events\":";output();std::cout<<"}\n";
 }else{std::memcpy((void*)0x4549A0,originalGrid,5);alignas(4)unsigned char info[0x90]={};
 int minimumX,minimumZ,maximumX,maximumZ,startX,startZ,endX,endZ;std::cin>>minimumX>>minimumZ>>maximumX>>maximumZ>>width>>height>>startX>>startZ>>endX>>endZ;
 auto*zone=(unsigned char*)0x5FC640;std::memset(zone,0,0x660);at<int>(zone,0)=1;at<int>(zone,4)=minimumX;at<int>(zone,8)=minimumZ;at<int>(zone,12)=maximumX;at<int>(zone,16)=maximumZ;at<int>(zone,20)=(maximumX-minimumX)/width;at<short>(zone,28)=width;at<short>(zone,30)=height;
 for(int z=0;z<height;z++)for(int x=0;x<width;x++)at<void*>(zone,0x20+4*(x*20+z))=&cellTokens[z*width+x];
 at<int>(info,0)=startX;at<int>(info,8)=startZ;at<int>(info,12)=endX;at<int>(info,20)=endZ;
 *(int*)0x5FBEE0=*(int*)0x5FBEE4=*(int*)0x5FBEDC=*(int*)0x5FBEE8=0;
 reinterpret_cast<void(*)(void*,int)>(0x4549A0)(info,1);output();std::cout<<'\n';
 }}
}
'''
    sweeps = []
    for action in [1, 16, 0x80, 0x1000000, 0x40000]:
        for movement in [(0, 0, 0), (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, 0, -1), (17, -31, 63),
                         (4095, 4095, 4095), (-4095, -4095, -4095), (4096, -8192, 12288), (32768, 4096, -24576)]:
            for previous in [(0, 0, 0), (-1234567, 2345678, -3456789), (2147483000, -2147483000, 0)]:
                for last in [(0, 0, 0), (0, -17, 0), (23, -41, 67)]:
                    wrap = lambda v: (v + 2**31) % 2**32 - 2**31
                    sweeps.append([action, *previous, *[wrap(a+b) for a, b in zip(previous, movement)], *last])
    grids = []
    for width, height in [(1, 1), (2, 3), (4, 4), (20, 20)]:
        minimumX, minimumZ, cellWidth = -12345, -34567, 12000
        maximumX, maximumZ = minimumX+width*cellWidth, minimumZ+height*cellWidth
        points = [(minimumX, minimumZ), (maximumX, maximumZ), (minimumX+1, maximumZ-1), (maximumX-1, minimumZ+1),
                  ((minimumX+maximumX)//2, (minimumZ+maximumZ)//2), (minimumX-5000, minimumZ-3000),
                  (maximumX+4000, maximumZ+6000), (minimumX-5000, maximumZ+4000), (maximumX+3000, minimumZ-6000)]
        grids.extend([[minimumX, minimumZ, maximumX, maximumZ, width, height, *start, *end]
                      for start in points for end in points])
    rng = random.Random(0x4549A0)
    for _ in range(600):
        width, height = rng.randrange(1, 21), rng.randrange(1, 21)
        step = rng.randrange(2000, 40000); minimumX, minimumZ = rng.randrange(-1000000, 0), rng.randrange(-1000000, 0)
        maximumX, maximumZ = minimumX+width*step, minimumZ+height*step
        grids.append([minimumX, minimumZ, maximumX, maximumZ, width, height,
                      *[rng.randrange(minimumX-step, maximumX+step), rng.randrange(minimumZ-step, maximumZ+step)],
                      *[rng.randrange(minimumX-step, maximumX+step), rng.randrange(minimumZ-step, maximumZ+step)]])
    rows = [(0, 'sweep', row) for row in sweeps] + [(1, 'grid', row) for row in grids]
    with tempfile.TemporaryDirectory(prefix='openspidey-trigger-zones-') as folder:
        cpp = Path(folder)/'reference.cpp'; binary = Path(folder)/'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-mfpmath=387', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(str(family)+' '+' '.join(map(str, row)) for family, _, row in rows)+'\n').encode()).decode().splitlines()
    fixtures = [dict(kind=kind, input=row, expected=json.loads(line)) for (_, kind, row), line in zip(rows, lines, strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-trigger-zone.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(sweeps)} actual PC trigger sweeps and {len(grids)} complete original grid walks.')


if __name__ == '__main__':
    main()
