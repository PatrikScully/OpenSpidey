"""Capture CRhino::ChargePlayer (0x47F800) leaves from SpideyPC.exe with patched callees."""
import argparse
import json
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cstring>
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
void omitWindowsUnwind(unsigned read,unsigned write){
 auto p=reinterpret_cast<unsigned char*>(read);assert(p[0]==0x64&&p[1]==0xA1);
 p[0]=0x31;p[1]=0xC0;std::memset(p+2,0x90,4);
 p=reinterpret_cast<unsigned char*>(write);assert(p[0]==0x64&&p[1]==0x89);std::memset(p,0x90,7);
}
alignas(4) unsigned char actor[0x500],player[0xF20],proc[0x80];
std::vector<std::vector<int>>events;
int seed;
int rnd(int bound){events.push_back({1,bound,seed});int v=bound<=0?0:seed%bound;seed++;return v;}
void*alloc(unsigned size){events.push_back({2,(int)size});std::memset(proc,0,sizeof proc);return proc;}
void __attribute__((thiscall)) neutralize(void*self){events.push_back({3});assert(self==actor);}
void __attribute__((thiscall)) lookAt(void*self,void*owner,void*body,void*point,int mask,int rate,int tol){
 events.push_back({4,mask,rate,tol,body==player?1:0,point?1:0});assert(owner==actor);at<void*>(actor,0x28C)=self;}
void __attribute__((thiscall)) runAnim(void*self,int clip,int frame,int dir){
 events.push_back({5,clip,frame,dir});assert(self==actor);at<unsigned short>(actor,0x12A)=(unsigned short)clip;actor[0x142]=0;}
int sfx(unsigned id,void*pos,int pitch){events.push_back({6,(int)id,pitch});return 0;}
void __attribute__((thiscall)) physics(void*self,int mode){events.push_back({7,mode});assert(self==actor);}
int __attribute__((thiscall)) runTimer(void*self,int*timer){
 events.push_back({8,*timer});assert(self==actor);*timer-=at<int>(actor,0x80);if(*timer<0)*timer=0;return *timer;}
void diagnostic(int,const char*){}
void numbers(const std::vector<int>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 omitWindowsUnwind(0x47F800,0x47F80E);
 // Epilogue / nested new cleanup sites inside ChargePlayer that write FS:[0].
 for(unsigned off:{0xAAu,0x10Au,0x17Au,0x1B9u,0x22Cu,0x27Au,0x341u,0x435u,0x51Au,0x5D4u,0x61Fu,0x678u,0x702u,0x7CAu,0x7F1u,0x820u,0x842u}){
  auto p=reinterpret_cast<unsigned char*>(0x47F800+off);assert(p[0]==0x64&&p[1]==0x89);std::memset(p,0x90,7);
 }
 patch(0x4E5DA0,(void*)&rnd);patch(0x455390,(void*)&alloc);patch(0x4040E0,(void*)&neutralize);
 patch(0x401180,(void*)&lookAt);patch(0x460C10,(void*)&runAnim);patch(0x471EA0,(void*)&sfx);
 patch(0x404C50,(void*)&physics);patch(0x402BE0,(void*)&runTimer);patch(0x4015B0,(void*)&diagnostic);
 int state,sub,ticks,finished,anim,crawling,flags288,flags218,field330,field334,px,py,pz,plX,plY,plZ,rndSeed;
 while(std::cin>>state>>sub>>ticks>>finished>>anim>>crawling>>flags288>>flags218>>field330>>field334>>px>>py>>pz>>plX>>plY>>plZ>>rndSeed){
  std::memset(actor,0,sizeof actor);std::memset(player,0,sizeof player);events.clear();seed=rndSeed;
  *reinterpret_cast<void**>(0x6A9038)=player;
  at<int>(actor,0x31C)=state;at<int>(actor,0x320)=sub;at<int>(actor,0x80)=ticks;actor[0x142]=(unsigned char)finished;
  at<unsigned short>(actor,0x12A)=(unsigned short)anim;at<unsigned>(actor,0x288)=flags288;at<unsigned>(actor,0x218)=flags218;
  at<int>(actor,0x330)=field330;at<int>(actor,0x334)=field334;
  at<int>(actor,8)=px;at<int>(actor,12)=py;at<int>(actor,16)=pz;
  at<int>(player,8)=plX;at<int>(player,12)=plY;at<int>(player,16)=plZ;player[0xAD4]=(unsigned char)crawling;
  reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x47F800)(actor);
  std::cout<<"{\"snapshot\":["<<at<int>(actor,0x31C)<<','<<at<int>(actor,0x320)<<','<<at<int>(actor,0x310)<<','
   <<at<int>(actor,0x330)<<','<<at<int>(actor,0x334)<<','<<at<unsigned>(actor,0x288)<<','<<at<unsigned>(actor,0x218)<<','
   <<(int)at<unsigned short>(actor,0x12A)<<','<<(int)actor[0x142]<<"],\"events\":[";
  for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';numbers(events[i]);}
  std::cout<<"]}\n";
 }
}
'''


def case(**kwargs):
    values = dict(state=8, sub=0, ticks=2, finished=0, anim=0, crawling=0, flags288=0, flags218=0,
                  field330=0, field334=0, px=0, py=0, pz=0, plX=100 << 12, plY=0, plZ=0, rndSeed=0)
    values.update(kwargs)
    return [values[k] for k in ('state', 'sub', 'ticks', 'finished', 'anim', 'crawling', 'flags288', 'flags218',
                                'field330', 'field334', 'px', 'py', 'pz', 'plX', 'plY', 'plZ', 'rndSeed')]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = []
    for seed in range(8):
        cases.append(case(state=8, sub=0, rndSeed=seed))
    for seed in [0, 7, 29]:
        cases.append(case(state=7, sub=0, rndSeed=seed))
    for field330, flags288, crawling in [(90, 0, 0), (2, 0, 0), (2, 2, 0), (10, 0, 1), (0, 0, 0), (0, 0, 1), (1, 2, 1)]:
        cases.append(case(state=8, sub=2, field330=field330, flags288=flags288, crawling=crawling, ticks=2))
    cases.append(case(state=8, sub=3, finished=0, anim=4))
    with tempfile.TemporaryDirectory(prefix='openspidey-rhino-charge-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output(
            [str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode())
    fixture = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, result.decode().splitlines(), strict=True)]
    out = root / 'modern/OpenSpidey.Tests/Fixtures/rhino-charge.json'
    out.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixture)} original CRhino::ChargePlayer cases.')


if __name__ == '__main__':
    main()
