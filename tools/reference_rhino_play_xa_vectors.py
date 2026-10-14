"""Capture CRhino::PlayXAPlease (0x47E0A0) from SpideyPC.exe with patched speech callees."""
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
alignas(4) unsigned char actor[0x500],player[0xF20];
std::vector<std::vector<int>>events;
int seed;
int rnd(int bound){events.push_back({1,bound,seed});int v=bound<=0?0:seed%bound;seed++;return v;}
unsigned char __attribute__((thiscall)) canTalk(void*self){events.push_back({2});return 1;}
int __attribute__((thiscall)) xaPos(int bank,int slot,void*pos,int priority){
 events.push_back({3,bank,slot,at<int>(pos,0),at<int>(pos,4),at<int>(pos,8),priority});return 1;}
void __attribute__((thiscall)) attachBody(void*self,int bank,int slot){
 if(self==actor)events.push_back({4,bank,slot});else if(self==player)events.push_back({5,bank,slot});else events.push_back({6,bank,slot});}
void dumpEvents(){std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned j=0;j<events[i].size();j++){if(j)std::cout<<',';std::cout<<events[i][j];}std::cout<<']';}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4E5DA0,(void*)&rnd);patch(0x479D30,(void*)&xaPos);patch(0x4603D0,(void*)&attachBody);
 patch(0x4B9730,(void*)&canTalk);
 *reinterpret_cast<void**>(0x6A9038)=player;
 int bankOffset,slotCount,rndAddend,field3dc,rndSeed;
 while(std::cin>>bankOffset>>slotCount>>rndAddend>>field3dc>>rndSeed){
  std::memset(actor,0,sizeof actor);events.clear();seed=rndSeed;at<int>(actor,0x3DC)=field3dc;
  reinterpret_cast<void(__attribute__((thiscall))*)(void*,int,int,int)>(0x47E0A0)(actor,bankOffset,slotCount,rndAddend);
  std::cout<<at<int>(actor,0x3DC)<<';';dumpEvents();std::cout<<'\n';
}}
'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = [
        (0, 3, 1, 0, 0),
        (0, 3, 1, 0, 3),
        (0, 3, 1, 1, 1),
        (15, 3, 1, 0, 2),
    ]
    with tempfile.TemporaryDirectory(prefix='openspidey-rhino-xa-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output(
            [str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(str(part) for part in row) for row in cases) + '\n').encode())
    fixture = []
    for row, line in zip(cases, result.decode().splitlines(), strict=True):
        field3dc, events = line.split(';', 1)
        fixture.append(dict(input=list(row), field3dc=int(field3dc), events=json.loads(events)))
    out = root / 'modern/OpenSpidey.Tests/Fixtures/rhino-play-xa.json'
    out.write_text(json.dumps(fixture, indent=2) + '\n')
    print(f'Captured {len(fixture)} original CRhino::PlayXAPlease cases -> {out}')


if __name__ == '__main__':
    main()
