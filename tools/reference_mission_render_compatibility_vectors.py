"""Capture whole originalPC151 and13 mixed-link continuation, retaining dispatch and following147/302 commands."""
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
#include <cstdarg>
#include <iostream>
#include <map>
struct Item{unsigned next;unsigned short flags;unsigned char rest[58];};
std::map<unsigned,Item> items;std::vector<unsigned> lookups;unsigned flags,lookupMode;int invalidLinks,dualCalls;
int debug(unsigned first,...){if(first==0){va_list a;va_start(a,first);unsigned format=va_arg(a,unsigned);va_end(a);if(format==0x558EF0)invalidLinks++;}else if(first==0x5595AC)dualCalls++;return 0;}
Item*find(unsigned checksum){lookups.push_back(checksum);if(lookupMode==0||(lookupMode==2&&checksum==0x22222222))return nullptr;
 auto added=items.emplace(checksum,Item{});if(added.second)added.first->second.flags=flags;return &added.first->second;}
void patch(unsigned address,void*function){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)function-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Omit Windows exception registration only. All actual command bodies, links, alignment, state writes and cursor increments remain original.
 std::memset((void*)0x4E021D,0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 for(unsigned address:{0x4E0224u,0x4E2BA0u})std::memset((void*)address,0x90,7);
 // Original debug entry is an empty sharedRET stub. Observation records its diagnostic arguments without adding state effects.
 patch(0x4015B0,(void*)&debug);patch(0x4C9230,(void*)&find);
 const unsigned watched[]={0x660F80,0x60F76C,0x5FAA64,0x5FAA5C,0x5FAA60,0x5FAA58,0x6B4670};
 int mode,alignment,operand,count,pattern;
 const unsigned short kinds[][6]={{2,1,9,7,5,20},{1,2,7,9,20,5},{2,9,2,9,2,9},{1,7,5,20,3,8}};
 while(std::cin>>mode>>alignment>>operand>>flags>>lookupMode>>count>>pattern){
  lookups.clear();items.clear();invalidLinks=dualCalls=0;
  alignas(4)std::array<unsigned short,24> context{};context[0]=3;context[1]=7;
  unsigned short links[]={1,2,3,4,5,6,1};for(unsigned i=0;i<7;i++)context[2+i]=links[i];
  alignas(4)std::array<unsigned short,32> references[6]{};unsigned short*nodes[7]={context.data()};
  for(unsigned i=0;i<6;i++){unsigned short*ref=references[i].data()+alignment/2;ref[0]=kinds[pattern][i];ref[1]=count;
   for(int j=0;j<count;j++)ref[2+j]=1;
   unsigned address=(reinterpret_cast<unsigned>(ref+2+count)+3)&~3u;*reinterpret_cast<unsigned*>(address)=i%2?0x22222222:0x11111111;nodes[i+1]=ref;}
  *(void**)0x6B466C=nodes;*(int*)0x6B4670=7;*(void**)0x6A9038=nullptr;
  for(unsigned i=0;i<sizeof watched/4;i++)*(int*)watched[i]=int(0x13570000+i*37);
  *(int*)0x6B4670=7;*(int*)0x5FCD14=-123;*(int*)0x558050=-234;
  unsigned short program[]={static_cast<unsigned short>(mode?13:151),static_cast<unsigned short>(operand),147,0x5678,302,1234,65535};
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program,mode?0:65535,0);
  std::cout<<"{\"gameLevel\":"<<*(int*)0x5FCD14<<",\"endNode\":"<<*(int*)0x558050<<",\"invalidLinks\":"<<invalidLinks<<",\"dualCalls\":"<<dualCalls<<",\"watched\":[";
  for(unsigned i=0;i<sizeof watched/4;i++){if(i)std::cout<<',';std::cout<<*(int*)watched[i];}
  std::cout<<"],\"lookups\":[";for(unsigned i=0;i<lookups.size();i++){if(i)std::cout<<',';std::cout<<lookups[i];}
  std::cout<<"],\"items\":[";bool comma=false;for(auto&entry:items){if(comma)std::cout<<',';comma=true;std::cout<<'['<<entry.first<<','<<entry.second.flags<<']';}std::cout<<"]}\n";
 }}
'''
    cases = [[0, alignment, operand, 0xA55A, 0, 0, 0]
             for alignment in [0, 2] for operand in [0, 1, 2, 320, 640, 1280, 32767, 32768, 65534, 65535]]
    cases += [[1, alignment, visible, flags, lookup, count, pattern]
              for pattern in range(4) for alignment in [0, 2] for count in [0, 1, 3]
              for visible in [0, 1, 256, 65535] for flags in [0, 1, 0xA55A, 65535] for lookup in range(3)]
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-render-compatibility-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    fixtures = [dict(input=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-render-compatibility.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} wholePC151/13 sequences: unchanged graphics globals, one-word consumption, mixed-kind links/diagnostics, original ENV flags and subsequent147/302 dispatch.')


if __name__ == '__main__':
    main()
