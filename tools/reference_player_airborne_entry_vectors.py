"""Capture complete original CheckJump4C2090 and CheckGroundGone4C23A0 at real animation/orientation/drop boundaries."""
import argparse
import json
from pathlib import Path
import random
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
void vec(CVector p){events.insert(events.end(),{p.vx,p.vy,p.vz});}
void __attribute__((thiscall)) animation(void*p,int clip,int first,int last){events.insert(events.end(),{1,clip,first,last});at<short>(p,0x12A)=clip;}
void __attribute__((thiscall)) orient(void*,bool targeted,CVector*p){events.insert(events.end(),{2,targeted});vec(*p);}
int sound(unsigned id,CVector*p,int pitch){events.insert(events.end(),{3,id});vec(*p);events.push_back(pitch);return 17;}
void __attribute__((thiscall)) drop(void*,CVector*p){events.push_back(4);vec(*p);}
void printVector(void*p,unsigned off){auto&v=at<CVector>(p,off);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);patch(0x460C10,(void*)&animation);patch(0x4C4BB0,(void*)&orient);patch(0x471EA0,(void*)&sound);patch(0x456E40,(void*)&drop);unsigned family=atoi(argv[2]);long long v[38];while(std::cin>>v[0]){for(int i=1;i<38;i++)std::cin>>v[i];alignas(4)unsigned char player[0xF20]={},controls[0x200]={},support[0x180]={};at<void*>(player,0xE0C)=controls;controls[0x101]=v[0];player[0x2C1]=v[1];player[0x8EA]=v[2];at<void*>(player,0xE48)=v[3]?reinterpret_cast<void*>(1):nullptr;player[0xAD4]=v[4];player[0x8E8]=v[5];player[0x8E9]=v[6];at<unsigned>(player,0xE1C)=v[7];at<unsigned short>(player,0xE0)=v[8];at<short>(player,0x12A)=v[9];player[0xE2D]=v[10];player[0xE2E]=v[11];at<short>(player,0xE32)=v[12];at<void*>(player,0xDBC)=v[13]?support:nullptr;at<int>(support,0x64)=v[14];for(unsigned i=0;i<3;i++)at<int>(player,0xE80+i*4)=v[15+i];at<int>(player,0xEBC)=v[18];for(unsigned i=0;i<3;i++)at<short>(player,0xA8+i*2)=v[19+i];at<CVector>(player,8)=CVector(v[22],v[23],v[24]);at<CVector>(player,0xC84)=CVector(v[25],v[26],v[27]);at<CVector>(player,0xC6C)=CVector(v[28],v[29],v[30]);player[0xEA4]=v[31];player[0xE8C]=v[32];player[0xAE5]=v[33];player[0xAE6]=v[34];player[0xE8D]=v[35];at<int>(player,0xE38)=v[36];at<int>(player,0xE3C)=v[37];events.clear();int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*)>(family?0x4C23A0:0x4C2090)(player);std::cout<<'['<<result<<','<<unsigned(controls[0x101])<<','<<unsigned(player[0x2C1])<<','<<(at<void*>(player,0xE48)?1:0)<<','<<unsigned(player[0xAD4])<<','<<unsigned(player[0x8E8])<<','<<unsigned(player[0x8E9])<<','<<at<unsigned>(player,0xE1C)<<','<<at<unsigned short>(player,0xE0)<<','<<at<unsigned short>(player,0x12A);for(unsigned i=0;i<3;i++)std::cout<<','<<at<int>(player,0xE80+i*4);std::cout<<','<<at<int>(player,0xEBC);for(unsigned i=0;i<3;i++)std::cout<<','<<at<short>(player,0xA8+i*2);printVector(player,8);for(unsigned off:{0xEA4u,0xE8Cu,0xAE5u,0xAE6u,0xE8Du})std::cout<<','<<unsigned(player[off]);std::cout<<','<<at<int>(player,0xE38)<<','<<at<int>(player,0xE3C)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    base = [1, 173, 0, 0, 0, 0, 0, 1, 2, 0, 0, 0, 0, 0, -123456,
            12345, 77, 99, 0x10000, 111, -222, 333, 12345, -23456, 34567,
            321, -4096, 123, 4096, 234, -456, 0, 173, 174, 175, 176, -555555, 99999]
    cases = []
    for crawling in [0, 1]:
        for wall in [0, 1]:
            for ceiling in [0, 1]:
                for heading in [-32768, -1, 0, 512, 513, 1535, 1536, 2048, 2560, 2561, 3583, 3584, 4095, 32767]:
                    for axes in [(0, 0), (-127, 0), (0, 127)]:
                        case = base.copy()
                        case[4:7] = [crawling, wall, ceiling]
                        case[10:13] = [*axes, heading]
                        cases.append(case)
    for action in [1, 8, 16, 17, 0x10010, 0x40000]:
        for support in [None, -2147483648, -262145, -262144, -262143, 0, 2147483647]:
            for collision in [0, 1, 2, 0x103]:
                case = base.copy()
                case[7:9] = [action, collision]
                case[13:15] = [int(support is not None), support or 0]
                cases.append(case)
    for edge in [0, 1, 255]:
        for blocked in [0, 1, 173]:
            for held in [0, 1]:
                for grace in [0, 1, 2, 255]:
                    for collision in [0, 1, 2, 3]:
                        case = base.copy()
                        case[0] = edge; case[2:4] = [blocked, held]; case[31] = grace; case[8] = collision
                        cases.append(case)
    rng = random.Random(0x4C2090)
    for _ in range(250):
        case = base.copy()
        case[:7] = [rng.randrange(2) for _ in range(7)]
        case[7:10] = [rng.randrange(1 << 32), rng.randrange(65536), rng.randrange(512)]
        case[10:13] = [rng.randint(-128, 127), rng.randint(-128, 127), rng.randint(-32768, 32767)]
        case[13] = rng.randrange(2)
        case[14:19] = [rng.randint(-2147483648, 2147483647) for _ in range(5)]
        case[19:22] = [rng.randint(-32768, 32767) for _ in range(3)]
        case[22:31] = [rng.randint(-2147483648, 2147483647) for _ in range(9)]
        case[31:36] = [rng.randrange(256) for _ in range(5)]
        case[36:38] = [rng.randint(-2147483648, 2147483647) for _ in range(2)]
        cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-air-entry-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        requests = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        output = []
        for family, name in [(0, 'jump'), (1, 'ground_gone')]:
            lines = subprocess.check_output([str(binary), str(args.original_exe.resolve()), str(family)], input=requests).decode().splitlines()
            output += [dict(kind=name, input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-airborne-entry.json').write_text(json.dumps(output, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original CheckJump and {len(cases)} GroundGone cases: consume/held/lookaround gates, signed support, wall/ceiling orientation, bytegrace, source drop and animation/sound order.')


if __name__ == '__main__':
    main()
