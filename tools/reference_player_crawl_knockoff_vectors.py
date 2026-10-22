"""Capture CPlayer::KnockSpideyFromCrawlPosition (0x4BB710) from SpideyPC.exe with patched callees."""
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
alignas(4) unsigned char player[0xF20];
int sounds[4];
std::vector<std::vector<int>>events;
void __attribute__((thiscall)) exitLookaround(void*self){events.push_back({1});player[0x8EA]=0;}
void __attribute__((thiscall)) runAnim(void*,int clip,int frame,int to){events.push_back({2,clip,frame,to});}
void __attribute__((thiscall)) orient(void*self,int useTarget,void*target){events.push_back({3,useTarget,(int)((unsigned char*)target-player)});}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 patch(0x4C3810,(void*)&exitLookaround);patch(0x460C10,(void*)&runAnim);patch(0x4C4BB0,(void*)&orient);
 int crawl,wall,ceiling,lookaround,action;
 while(std::cin>>crawl>>wall>>ceiling>>lookaround>>action){
  std::memset(player,0,sizeof player);events.clear();
  sounds[0]=0x10005;sounds[1]=0x2000C;sounds[2]=-1;*reinterpret_cast<int**>(0x6A8574)=sounds;
  player[0xAD4]=(unsigned char)crawl;player[0x8E8]=(unsigned char)wall;player[0x8E9]=(unsigned char)ceiling;
  player[0x8EA]=(unsigned char)lookaround;at<int>(player,0xE1C)=action;at<short>(player,0xAA)=77;player[0xAE5]=9;
  int result=(unsigned char)reinterpret_cast<char(__attribute__((thiscall))*)(void*)>(0x4BB710)(player);
  std::cout<<"{\"ret\":"<<result<<",\"fields\":["<<(int)player[0xAD4]<<','<<(int)player[0x8E8]<<','<<(int)player[0x8E9]<<','
   <<(int)player[0x8EA]<<','<<at<int>(player,0xE1C)<<','<<at<short>(player,0xA8)<<','<<at<short>(player,0xAA)<<','<<at<short>(player,0xAC)<<','
   <<(int)player[0xAE5]<<','<<(at<int*>(player,0x350)==sounds?1:0)<<','<<sounds[0]<<','<<sounds[1]<<"],\"events\":[";
  for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<'[';for(unsigned j=0;j<events[i].size();j++){if(j)std::cout<<',';std::cout<<events[i][j];}std::cout<<']';}
  std::cout<<"]}\n";
 }
}
'''


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = [(crawl, wall, ceiling, look, action) for crawl in (0, 1) for wall, ceiling in ((0, 0), (1, 0), (0, 1), (1, 1))
             for look in (0, 1) for action in (0x10, 0x800000)]
    with tempfile.TemporaryDirectory(prefix='openspidey-crawl-knockoff-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode())
    fixture = [dict(input=list(row), expected=json.loads(line)) for row, line in zip(cases, output.decode().splitlines(), strict=True)]
    out = root / 'modern/OpenSpidey.Tests/Fixtures/player-crawl-knockoff.json'
    out.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixture)} original CPlayer::KnockSpideyFromCrawlPosition cases.')


if __name__ == '__main__':
    main()
