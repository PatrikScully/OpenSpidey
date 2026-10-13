"""Capture original Push/Set7/Set160mode/Pop sequences over the actual saved mode/pose and retained fixed-camera fields."""
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
#include <iostream>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void snapshot(unsigned char*raw){std::cout<<at<unsigned>(raw,0x2A0)<<','<<at<unsigned>(raw,0x280)<<','<<*reinterpret_cast<int*>(0x56F254);for(unsigned o:{8u,0x1E4u,0x284u,0x290u})for(int i=0;i<(o==8||o==0x284?3:4);i++)std::cout<<','<<at<int>(raw,o+4*i);std::cout<<','<<at<int>(raw,0x2BC)<<','<<at<int>(raw,0x2C0);for(int i=0;i<3;i++)std::cout<<','<<at<int>(raw,0x24C+4*i);for(int i=0;i<4;i++)std::cout<<','<<at<int>(raw,0x2D4+4*i);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);unsigned initial,command,duration;while(std::cin>>initial>>command>>duration){alignas(4)unsigned char raw[0x300]={};at<unsigned>(raw,0x2A0)=initial;at<int>(raw,0x2BC)=duration;at<int>(raw,0x2C0)=duration;for(int i=0;i<3;i++){at<int>(raw,8+4*i)=1000+111*i;at<int>(raw,0x24C+4*i)=-4000+222*i;}for(int i=0;i<4;i++){at<int>(raw,0x1E4+4*i)=10+11*i;at<int>(raw,0x2D4+4*i)=-20-13*i;}*reinterpret_cast<int*>(0x56F254)=77;reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416720)(raw);reinterpret_cast<unsigned(__attribute__((thiscall)) *)(void*,unsigned)>(0x4167F0)(raw,7);for(int i=0;i<3;i++)at<int>(raw,8+4*i)=-1000-111*i;for(int i=0;i<4;i++)at<int>(raw,0x1E4+4*i)=-10-11*i;unsigned previous=reinterpret_cast<unsigned(__attribute__((thiscall)) *)(void*,unsigned)>(0x4167F0)(raw,command);std::cout<<'['<<previous<<",[";snapshot(raw);std::cout<<"],[";reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416780)(raw);snapshot(raw);std::cout<<"]]\n";}}
'''
    cases = [[initial, command, duration] for initial in [3, 4, 5] for command in [3, 4, 5] for duration in [0, 17, 65535]]
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-command-mode-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()).decode().splitlines()
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-command-mode.json').write_text(json.dumps(
        [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original camera mode ownership sequences: actual Set160 modes3/4/5 from7, retained saved mode/position/quaternion, both fixed timers/targets and later Pop restoration.')


if __name__ == '__main__':
    main()
