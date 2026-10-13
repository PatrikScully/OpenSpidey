"""Run real PC TestItemFaces46F1F0 on a genuine coarse triangle: zone tokens stay separate from nearest blockers."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
unsigned packed,record,nearest;while(std::cin>>packed>>record>>nearest){
 alignas(4)unsigned char model[0x60]={},info[0x90]={},item[0x40]={};short scratch[12]={-100,-100,50,0,100,-100,50,0,-100,100,50,0};
 at<unsigned short>(model,2)=3;at<unsigned short>(model,4)=1;at<unsigned short>(model,6)=1;
 auto*normal=model+0x1C+3*8;at<short>(normal,4)=-4096;
 auto*face=normal+8;at<unsigned short>(face,0)=0x10;at<unsigned short>(face,2)=16;
 face[4]=0;face[5]=1;face[6]=2;face[7]=0;at<unsigned>(face,12)=packed;
 at<int>(info,0x40)=nearest;at<int>(info,0x44)=100;at<unsigned char>(info,0x88)=record;
 at<unsigned short>(item,0x1A)=7;*(int*)0x5FBDBC=-1;*(unsigned*)0x5FBDA8=0x200000;*(unsigned*)0x5FBDDC=0xFFFFFFFF;
 std::memset((void*)0x610B60,0,18);*(short*)0x610B60=*(short*)0x610B68=*(short*)0x610B70=4096;
 *(short*)0x610B4C=*(short*)0x610B4E=0;*(short*)0x610B50=102;
 reinterpret_cast<void(*)(void*,void*,void*,void*)>(0x46F1F0)(model,scratch,info,item);
 std::cout<<'['<<*(int*)0x5FBDBC<<','<<at<int>(info,0x40)<<','<<(at<void*>(info,0x68)==item)<<','<<(at<void*>(info,0x80)==face)<<"]\n";
 }}
'''
    cases = [[category | extra, record, nearest]
             for category in [0, 0x10000, 0x20000, 0x30000]
             for extra in [0, 0x200000, 0x400000, 0x100000, 0xC00000, 0x300000]
             for record in [0, 1, 255] for nearest in [5, 1000, 2147483647]]
    with tempfile.TemporaryDirectory(prefix='openspidey-trigger-face-') as folder:
        cpp = Path(folder)/'reference.cpp'; binary = Path(folder)/'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-m32', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in cases)+'\n').encode()).decode().splitlines()
    (root/'modern/OpenSpidey.Tests/Fixtures/trigger-face-policy.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(row)) for case, row in zip(cases, rows, strict=True)], separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} complete PC coarse triangle face/token/blocker decisions.')


if __name__ == '__main__': main()
