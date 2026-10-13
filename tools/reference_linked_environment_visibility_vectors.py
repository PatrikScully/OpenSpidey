"""Capture untouched PC SendVisible13 link order, absolute checksum alignment and ENV flag writes."""
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
#include <array>
#include <iostream>
#include <map>
struct Item {unsigned next;unsigned short flags;unsigned char rest[58];};
std::map<unsigned,Item> items;
std::vector<unsigned> calls;
unsigned initial, mode;
Item* find(unsigned hash){calls.push_back(hash);if(mode==0 || (mode==2 && hash==0x22222222))return nullptr;
 auto result=items.emplace(hash,Item{});if(result.second)result.first->second.flags=initial;return &result.first->second;}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Keep original command dispatch, links, checksum alignment and bit mutation; omit only Windows SEH/logging.
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7);std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;jump(0x4C9230,reinterpret_cast<void*>(&find));
 unsigned kind,align,count,visible;
 while(std::cin>>kind>>align>>count>>visible>>initial>>mode){
  calls.clear();items.clear();
  unsigned short context[]={3,4,1,2,1,3};
  alignas(4) std::array<unsigned short,16> first{},second{},third{};
  unsigned short* refs[]={first.data()+align/2,second.data()+align/2,third.data()+align/2};
  for(unsigned i=0;i<3;i++){unsigned short*n=refs[i];n[0]=kind;n[1]=count;
   for(unsigned j=0;j<count;j++)n[2+j]=3;
   unsigned address=reinterpret_cast<unsigned>(n+2+count);address=(address+3)&~3u;
   *reinterpret_cast<unsigned*>(address)=i==0?0x11111111:i==1?0x22222222:0x11111111;}
  unsigned short* nodes[]={context,refs[0],refs[1],refs[2]};
  *reinterpret_cast<unsigned short***>(0x6B466C)=nodes;*reinterpret_cast<int*>(0x6B4670)=4;
  unsigned short program[]={13,static_cast<unsigned short>(visible),65535};
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program,0,0);
  std::cout<<"[ [";for(unsigned i=0;i<calls.size();i++){if(i)std::cout<<',';std::cout<<calls[i];}
  std::cout<<"], [";bool comma=false;for(auto& row:items){if(comma)std::cout<<',';comma=true;
   std::cout<<'['<<row.first<<','<<row.second.flags<<']';}std::cout<<"] ]\n";
 }}
'''
    cases = [dict(kind=kind, alignment=alignment, links=count, visible=visible, flags=flags, mode=mode)
             for kind in [2, 9] for alignment in [0, 2] for count in [0, 1, 2, 3]
             for visible in [0, 1, 2, 65535] for flags in [0, 1, 0x1020, 0xFFFF] for mode in [0, 1, 2]]
    with tempfile.TemporaryDirectory(prefix='openspidey-linked-env-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(str(case[key]) for key in ['kind', 'alignment', 'links', 'visible', 'flags', 'mode']) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/linked-environment-visibility.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} original SendVisible13 executions with linked lookup, shared item identity and aligned source checksums.')


if __name__ == '__main__':
    main()
