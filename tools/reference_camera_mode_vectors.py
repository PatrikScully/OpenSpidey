"""Capture actual PC Push/Set/Pop mode and front7 MoveToDesiredPos, preserving its exact pose snapshot and rotation writes."""
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
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);int mode,water;
while(std::cin>>mode>>water){alignas(4)unsigned char raw[0x2A4]={};at<unsigned>(raw,0x2A0)=mode;for(int i=0;i<3;i++)at<int>(raw,8+4*i)=1000+111*i;for(int i=0;i<4;i++)at<int>(raw,0x1E4+4*i)=10+11*i;at<unsigned char>(raw,0x180)=255;*reinterpret_cast<int*>(0x56F254)=77;*reinterpret_cast<int*>(0x5FAE98)=water;
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416720)(raw);unsigned previous=reinterpret_cast<unsigned(__attribute__((thiscall)) *)(void*,unsigned)>(0x4167F0)(raw,7);for(int i=0;i<3;i++)at<int>(raw,8+4*i)=-1000-111*i;for(int i=0;i<4;i++)at<int>(raw,0x1F4+4*i)=-10-11*i;
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416B10)(raw);std::cout<<'['<<previous<<','<<at<unsigned>(raw,0x280)<<','<<unsigned(at<unsigned char>(raw,0x180))<<','<<*reinterpret_cast<int*>(0x56F254);for(unsigned o: {8u,0x1E4u,0x204u,0x214u,0x284u,0x290u})for(int i=0;i<(o==8||o==0x284?3:4);i++)std::cout<<','<<at<int>(raw,o+4*i);
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416780)(raw);std::cout<<','<<at<unsigned>(raw,0x2A0);for(unsigned o:{8u,0x1E4u})for(int i=0;i<(o==8?3:4);i++)std::cout<<','<<at<int>(raw,o+4*i);
for(unsigned next:{16u,17u,3u}){*reinterpret_cast<int*>(0x56F254)=77;unsigned old=reinterpret_cast<unsigned(__attribute__((thiscall)) *)(void*,unsigned)>(0x4167F0)(raw,next);std::cout<<','<<old<<','<<at<unsigned>(raw,0x2A0)<<','<<*reinterpret_cast<int*>(0x56F254);}std::cout<<"]\n";}}
'''
    cases = [[mode, water] for mode in range(18) for water in [0, 1]]
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-mode-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-mode.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} actual PC camera-mode sequences: Push/Set/front7 Move/Pop plus mode16/17 counter resets.')


if __name__ == '__main__': main()
