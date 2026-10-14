"""Capture whole PC Jonah DoMGSShadow/GTE/quad stores, including actual first-time CQuadBit construction."""
import argparse
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <iostream>
struct Vec{int x,y,z;};struct Hook{short x,y,z;unsigned short part;};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
template<class T>T&mem(unsigned a){return *reinterpret_cast<T*>(a);}
Vec hooks[4];int count,allocations,textureLookups;alignas(4)unsigned char quad[132],texture[24];unsigned quick[3];
void hook(Vec*out,void*,Hook*h){int index=h->part==3?0:h->part==6?1:h->part==10?2:h->part==15?3:-1;assert(index>=0);*out=hooks[index];count++;}
void diagnostic(int ok,const char*){assert(ok);}
void patch(unsigned a,void*t){auto*p=(unsigned char*)a;p[0]=0xe9;*(unsigned*)(p+1)=(unsigned)t-a-5;}
void*allocate(unsigned size,int a,int b,int c,int d){assert(size==132&&a==0&&b==1&&c==0&&d==1);allocations++;return quad;}
void*lookup(const char*name){assert(!strcmp(name,"Shadow2"));textureLookups++;return texture;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset((void*)0x446980,0x90,6);mem<unsigned short>(0x446980)=0xC031;std::memset((void*)0x44698E,0x90,7);std::memset((void*)0x446CBC,0x90,7);
 patch(0x453EE0,(void*)&hook);patch(0x4015B0,(void*)&diagnostic);patch(0x4581A0,(void*)&allocate);patch(0x4C94B0,(void*)&lookup);
 quick[0]=1;quick[1]=0;quick[2]=(unsigned)texture;mem<unsigned>(0x56EA64)=(unsigned)(quick+1);
 int ground,fresh,steps;while(std::cin>>fresh>>steps>>ground){alignas(4)unsigned char actor[0x380]={};int x;std::memset(quad,0,sizeof quad);std::memset(texture,0,sizeof texture);
  at<short>(actor,0x222)=ground;at<short>(actor,0x21E)=100;at<void*>(actor,0x374)=fresh?nullptr:quad;at<short>(quad,0x38)=99;at<unsigned>(quad,0x64)=0x1C0001;
  for(int i=0;i<3;i++){std::cin>>x;at<int>(actor,8+i*4)=x;}
  for(int i=0;i<9;i++){std::cin>>x;at<short>(actor,0x164+i*2)=x;}
  for(auto&h:hooks)std::cin>>h.x>>h.y>>h.z;
  count=allocations=textureLookups=0;mem<int>(0x56EB48)=0;mem<void*>(0x56EB2C)=nullptr;
  for(int step=0;step<steps;step++)reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x446980)(actor);
  std::cout<<'['<<count<<','<<at<short>(quad,0x38)<<','<<at<unsigned>(quad,0x64)<<','<<at<unsigned>(quad,0x6C)<<','<<allocations<<','<<textureLookups<<','<<mem<int>(0x56EB48)<<','<<(mem<void*>(0x56EB2C)==quad?1:0)<<",[";
  int n=0;for(unsigned offset:{0x10u,0x3Cu,0x48u,0x54u}){if(n++)std::cout<<',';auto v=at<Vec>(quad,offset);std::cout<<'['<<v.x<<','<<v.y<<','<<v.z<<']';}std::cout<<"]]\n";
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x446980)
    cases = []
    for fresh in [0,1]:
        for angle in [0,512,1024,2048,4095]:
            a=angle*.001536096911877394;s=int(math.sin(a)*4096);c=int(math.cos(a)*4096)
            for limit in [0,32,1024,524288]:
                position=[12345,-67890,34567];matrix=[c,0,s,0,4096,0,-s,0,c]
                hooks=[[position[0]+rng.randint(-limit,limit)*4096,position[1]+rng.randint(-limit,limit)*4096,position[2]+rng.randint(-limit,limit)*4096] for _ in range(4)]
                cases.append([fresh, rng.choice([1,2,32]),rng.choice([-32768,-1,0,160,32767]),*position,*matrix,*[v for h in hooks for v in h]])
        for _ in range(180):
            position=[rng.randint(-2147483647,2147483647) for _ in range(3)];matrix=[rng.randint(-4096,4096) for _ in range(9)];hooks=[rng.randint(-2147483647,2147483647) for _ in range(12)]
            cases.append([fresh,rng.choice([1,2,32]),rng.randint(-32768,32767),*position,*matrix,*hooks])
    with tempfile.TemporaryDirectory(prefix='openspidey-jonah-shadow-') as folder:
        cpp,binary=Path(folder)/'reference.cpp',Path(folder)/'reference';cpp.write_text(HARNESS)
        subprocess.run(['g++','-m32','-std=c++17','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        output=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,c)) for c in cases)+'\n').encode()).decode().splitlines()
    fixtures=[dict(input=c,expected=json.loads(line)) for c,line in zip(cases,output,strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/jonah-shadows.json').write_text(json.dumps(fixtures,separators=(',',':'))+'\n')
    print(f'Captured {len(fixtures)} whole PC Jonah footprint cases with actual GTE math, quad construction/reuse, Shadow2 override and source stores.')


if __name__=='__main__':
    main()
