"""Capture whole original4BB300 hand-trail dispatch and unpatched4BB1E0 midpoint math; omit only platform SEH and external particle/hook owners."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;unsigned char bodies[4][0x100];CVector hooks[2];int next;
int id(void*p){if(!p)return 0;for(int i=0;i<4;i++)if(p==bodies[i])return i+1;std::abort();}
void vec(CVector p){events.insert(events.end(),{p.vx,p.vy,p.vz});}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void hook(CVector*out,void*,int index){assert(index==0||index==1);*out=hooks[index];events.insert(events.end(),{1,index});vec(*out);}
void*allocate(unsigned size){events.insert(events.end(),{5,size});assert(next<4);return bodies[next++];}
void*__attribute__((thiscall))ctor(void*p,CVector*position,int count,int r,int g,int b){events.insert(events.end(),{2,id(p)});vec(*position);events.insert(events.end(),{count,r,g,b});return p;}
void __attribute__((thiscall))destroy(void*p,int flags){events.insert(events.end(),{4,id(p),flags});}
void __attribute__((thiscall))set(void*p,CVector*position){events.insert(events.end(),{3,id(p)});vec(*position);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);std::memset(reinterpret_cast<void*>(0x4BB307),0x90,6);*reinterpret_cast<unsigned short*>(0x4BB307)=0xC031;std::memset(reinterpret_cast<void*>(0x4BB30E),0x90,7);std::memset(reinterpret_cast<void*>(0x4BB701),0x90,7);patch(0x453D60,(void*)&hook);patch(0x4088A0,(void*)&allocate);patch(0x410F50,(void*)&ctor);patch(0x410EB0,(void*)&set);void*vt[]={reinterpret_cast<void*>(&destroy)};long long v[20];while(std::cin>>v[0]){for(int i=1;i<20;i++)std::cin>>v[i];alignas(4)unsigned char player[0xF20]={};for(auto&b:bodies)at<void*>(b,0)=vt;next=2;at<short>(player,0x12A)=v[0];at<short>(player,0x128)=v[1];at<void*>(player,0x58C)=v[2]?bodies[0]:nullptr;at<void*>(player,0x590)=v[3]?bodies[1]:nullptr;at<unsigned>(player,0x580)=v[4];at<CVector>(player,8)=CVector(v[5],v[6],v[7]);hooks[1]=CVector(v[8],v[9],v[10]);hooks[0]=CVector(v[11],v[12],v[13]);at<CVector>(player,0x594)=CVector(v[14],v[15],v[16]);at<CVector>(player,0x5A0)=CVector(v[17],v[18],v[19]);events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4BB300)(player);std::cout<<'['<<id(at<void*>(player,0x58C))<<','<<id(at<void*>(player,0x590));for(unsigned off:{0x594u,0x5A0u}){auto&p=at<CVector>(player,off);std::cout<<','<<p.vx<<','<<p.vy<<','<<p.vz;}std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    base = [104, 1, 0, 0, 0x202040, 12345, -23456, 34567, 23456, -567890, 44444,
            -456789, -345678, 777777, 13579, -55555, 24680, -12345, -54321, 123456]
    cases = []
    for clip in [0, 104, 106, 260, 262]:
        for frame in [-32768, -1, 0, 1, 2, 6, 7, 8, 32767]:
            for mask in range(4):
                case = base.copy(); case[:4] = [clip, frame, mask & 1, (mask >> 1) & 1]; cases.append(case)
    for distance in [0, 1, 31, 32, 33, 64, 127, 256]:
        for sign in [-1, 1]:
            case = base.copy(); case[:4] = [106, 4, 1, 1]
            case[5:8] = [0, 0, 0]; case[8:14] = [sign * distance << 12, 0, 0, 0, sign * distance << 12, 0]
            case[14:20] = [0] * 6; cases.append(case)
    rng = random.Random(0x4BB1E0)
    for _ in range(300):
        case = base.copy(); case[:4] = [rng.choice([0, 104, 106]), rng.randint(-2, 10), rng.randrange(2), rng.randrange(2)]
        case[4] = rng.randrange(1 << 32)
        case[5:] = [rng.randint(-2147483648, 2147483647) for _ in range(15)]
        cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-hand-trails-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=rows).decode().splitlines()
    result = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-hand-trails.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original hand-trail cases including unpatched source midpoint>>8, Q12 normalization, signed frame gates, construct/delete/set order and both hook offsets.')


if __name__ == '__main__':
    main()
