"""Execute complete original4C0B80 ceiling/wall smash entry with its genuine typed orientation/torso/animation boundaries."""
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
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) orient(void*,bool use,CVector*p){events.insert(events.end(),{1,use,p->vx,p->vy,p->vz});}
void __attribute__((thiscall)) torso(void*,CVector*p){events.insert(events.end(),{2,p->vx,p->vy,p->vz});}
void __attribute__((thiscall)) run(void*p,int clip,int first,int last){events.insert(events.end(),{3,clip,first,last});at<unsigned short>(p,0x12A)=clip;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C4BB0,(void*)&orient);patch(0x4C68A0,(void*)&torso);patch(0x460C10,(void*)&run);std::memset((void*)0x6A82B8,0,300*4);
long long v[13];while(std::cin>>v[0]){for(int i=1;i<13;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},controls[0x200]={},target[0x100]={};at<void*>(player,0xE0C)=controls;at<CVector>(player,8)=CVector(12345,-23456,34567);at<CVector>(target,8)=CVector(v[0],v[1],v[2]);at<void*>(player,0xDCC)=v[3]?target:nullptr;player[0x8EA]=v[4];player[0x8E8]=v[5];player[0x8E9]=v[6];player[0xAD4]=v[7];controls[0x121]=v[8];controls[0x131]=v[9];at<unsigned>(player,0xE1C)=v[10];at<unsigned short>(player,0x12A)=v[11];*(unsigned*)0x6B4CA8=v[12];at<short>(player,0xA8)=123;at<short>(player,0xAA)=-456;at<short>(player,0xAC)=789;player[0xAE4]=173;at<int>(player,0x8DC)=99;at<int>(player,0x8C4)=1000;at<int>(player,0x8C8)=900;at<CVector>(player,0x8CC)=CVector(31,-32,33);player[0x8D8]=77;at<void*>(player,0x350)=(void*)1;events.clear();
int result=reinterpret_cast<unsigned char(__attribute__((thiscall))*)(void*)>(0x4C0B80)(player);
auto&p=at<CVector>(player,0x8CC);std::cout<<'['<<result<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0x12A)<<','<<unsigned(player[0xAE4])<<','<<at<short>(player,0xA8)<<','<<at<short>(player,0xAA)<<','<<at<short>(player,0xAC)<<','<<unsigned(player[0xAD4])<<','<<at<int>(player,0x8DC)<<','<<at<int>(player,0x8C4)<<','<<at<int>(player,0x8C8)<<','<<p.vx<<','<<p.vy<<','<<p.vz<<','<<unsigned(player[0x8D8])<<','<<unsigned(controls[0x121])<<','<<unsigned(controls[0x131])<<','<<(at<void*>(player,0x350)!=nullptr)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    cases = [[x, y, z, present, active, wall, ceiling, crawl, kick, punch, 16, 50, clock]
             for x, y, z in [(12345, -23456, 34567), (12345, 3456789, 34567), (12345, -3456789, 34567),
                             (-2147483648, 2147483647, -2147483648)]
             for present in [0, 1] for active in [0, 255] for wall, ceiling in [(0, 0), (1, 0), (0, 255), (255, 255)]
             for crawl in [0, 1] for kick, punch in [(0, 0), (255, 0), (0, 255), (255, 255)]
             for clock in [0, 0xFFFF1234]]
    with tempfile.TemporaryDirectory(prefix='openspidey-ceiling-smash-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp),
                        str(root/'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, case)) for case in cases)+'\n').encode()).decode().splitlines()
    fixture = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/player-ceiling-smash.json').write_text(json.dumps(fixture, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} complete original ceiling/wall smash selectors, untouched attack edges, exact orientation/torso/reset-animation order and shared source1000000 fields.')


if __name__ == '__main__':
    main()
