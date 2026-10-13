"""Capture whole original CBaddy42B5 dispatch, absolute alignment and field24C callback/return ordering."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe',type=Path,required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parent.parent
    code=r'''
#include "original_executable.h"
#include <array>
#include <iostream>
template<class T>T&at(void*body,unsigned offset){return *reinterpret_cast<T*>((unsigned char*)body+offset);}
unsigned char*caller;unsigned char*origin;unsigned observed;int calls,entry;
void chunk(unsigned checksum){observed=checksum;calls++;entry=at<unsigned char*>(caller,0x24C)-origin;}
void patch(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset((void*)0x4050B0,0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 for(unsigned address:{0x4050BEu,0x406ACBu})std::memset((void*)address,0x90,7);
 patch(0x4273D0,(void*)&chunk);
 int sourceOffset,position;unsigned checksum;
 while(std::cin>>sourceOffset>>position>>checksum){
  alignas(4)unsigned char body[0x300]={};alignas(4)unsigned char bytes[128]={};caller=body;
  origin=bytes+(sourceOffset&3);unsigned char*start=origin+position;
  unsigned char*aligned=(unsigned char*)((reinterpret_cast<unsigned>(start)+3)&~3u);
  at<unsigned>(aligned,0)=checksum;at<unsigned char*>(body,0x24C)=start;calls=0;observed=0;entry=-1;
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(body,0x42B5);
  std::cout<<'['<<result<<','<<calls<<','<<observed<<','<<entry<<','<<(at<unsigned char*>(body,0x24C)-origin)<<"]\n";
 }
}
'''
    inputs=[[offset,position,checksum] for offset in [0,2,100,102] for position in [0,2,4,6]
        for checksum in [0,1,0xFFFFFFFF,0x80002000,0xA000212B,0x102A9]]
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-env-chunk-') as directory:
        cpp,binary=Path(directory)/'reference.cpp',Path(directory)/'reference';cpp.write_text(code)
        subprocess.run(['g++','-std=c++17','-m32','-fno-pie','-no-pie','-I',str(root/'src'),'-I',str(root/'tests/runtime'),str(cpp),'-o',str(binary)],check=True)
        output=subprocess.check_output([str(binary),str(args.original_exe.resolve())],input=('\n'.join(' '.join(map(str,row)) for row in inputs)+'\n').encode())
    fixture=[dict(input=row,expected=json.loads(line)) for row,line in zip(inputs,output.decode().splitlines(),strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-environment-chunk.json').write_text(json.dumps(fixture,separators=(',',':'))+'\n')
    print(f'Captured {len(fixture)} whole original CBaddy42B5 direct-checksum/alignment/cursor-commit boundaries.')


if __name__=='__main__':main()
