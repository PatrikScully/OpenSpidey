"""Capture whole original489370 InitTailSweepPSX palette,132 face descriptors and callback-visible private-region stores."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
void patch(unsigned address,void*target){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)target-address-5;}
alignas(4)unsigned char geometry[2884],palette[1024];unsigned char*actor;std::vector<unsigned>events;
void*allocate(unsigned size,int second,int third,int fourth,int fifth){unsigned slot=at<unsigned char>(actor,0x45F);assert(slot<40);auto*r=(unsigned char*)(0x6B2440+slot*68);
 events.insert(events.end(),{size,unsigned(second),unsigned(third),unsigned(fourth),unsigned(fifth),slot,unsigned(r[0]),unsigned(r[1]),unsigned(r[2]),unsigned(r[9]),unsigned(r[10]),unsigned(r[11]),at<unsigned short>(r,56),at<unsigned short>(actor,0x444),at<unsigned short>(actor,0x454),at<unsigned short>(actor,0x456),at<unsigned short>(actor,0x458),at<unsigned short>(actor,0x45A),at<unsigned>(actor,0x460),at<unsigned>(actor,0x47C)});
 assert(size==1024||size==2884);return size==1024?palette:geometry;}
void values(const std::vector<unsigned>&v){std::cout<<'[';for(unsigned i=0;i<v.size();i++){if(i)std::cout<<',';std::cout<<v[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4581A0,(void*)&allocate);unsigned occupancy;
 while(std::cin>>occupancy){alignas(4)unsigned char body[0xC28]={};actor=body;events.clear();std::memset((void*)0x6B2440,0,40*68);
  // A mask represents the lower32 busy slots; upper8 are filled independently so late first-free slots are covered.
  unsigned late;std::cin>>late;for(unsigned i=0;i<40;i++)if(i<32?((occupancy>>i)&1):((late>>(i-32))&1))*(unsigned char*)(0x6B2440+i*68)='X';
  at<unsigned char>(body,0x45F)=255;std::memset(geometry,0xCD,sizeof(geometry));std::memset(palette,0xCD,sizeof(palette));
  reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x489370)(body);unsigned slot=at<unsigned char>(body,0x45F);auto*r=(unsigned char*)(0x6B2440+slot*68);
  std::cout<<"{\"region\":"<<slot<<",\"events\":";values(events);std::cout<<",\"regionStores\":[";
  for(unsigned offset:{0u,1u,2u,9u,10u,11u,58u}){if(offset)std::cout<<',';std::cout<<unsigned(r[offset]);}
  std::cout<<','<<at<unsigned short>(r,56)<<','<<(at<void*>(r,20)==body+0x488)<<','<<(at<void*>(r,40)==palette)<<"],\"header\":[";
  for(unsigned offset:{0u,2u,4u,6u,24u,26u}){if(offset)std::cout<<',';std::cout<<at<unsigned short>(geometry,offset);}std::cout<<','<<at<unsigned>(geometry,8)<<"],\"palette\":[";
  for(unsigned i=0;i<256;i++){if(i)std::cout<<',';std::cout<<at<unsigned>(palette,i*4);}std::cout<<"],\"faces\":[";
  for(unsigned face=0;face<132;face++){if(face)std::cout<<',';auto*p=geometry+764+face*16;std::cout<<'['<<at<unsigned short>(p,0)<<','<<at<unsigned short>(p,2);for(unsigned offset:{4u,5u,6u,7u})std::cout<<','<<unsigned(p[offset]);std::cout<<','<<at<unsigned>(p,8)<<']';}
  bool verticesUntouched=true;for(unsigned i=28;i<764;i++)verticesUntouched&=geometry[i]==0xCD;
  std::cout<<"],\"verticesUntouched\":"<<(verticesUntouched?"true":"false")<<"}\n";
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    cases = [[(1 << occupied) - 1, 0] for occupied in range(32)]
    cases += [[0xFFFFFFFF, (1 << occupied) - 1] for occupied in range(8)]
    cases += [[0xFFFFFFFF ^ (1 << index), 0xFF] for index in [0, 7, 17, 31]]
    cases += [[0xAAAAAAAA, 0xAA], [0x55555555, 0x55], [0xFFFFFFFE, 0xFF]]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-tail-sweep-') as folder:
        source, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        source.write_text(HARNESS)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(source), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases) + '\n').encode()).decode().splitlines()
    fixture = [dict(occupancy=row, expected=json.loads(line)) for row, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-tail-sweep.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original489370 sweep initializations: first-free slots0..39,256-colour palette,132 double-sided additive faces, unchanged vertex storage and both allocation-visible region/item states.')


if __name__ == '__main__':
    main()
