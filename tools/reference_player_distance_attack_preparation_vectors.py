"""Capture whole original4B9420 at typed resource/orientation/camera boundaries, leaving all source control flow and vector math intact."""
import argparse
import itertools
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
std::vector<long long>events;unsigned char*player;unsigned char bodies[12][0x100];int valid[12];
int id(void*p){if(!p)return 0;for(int i=0;i<12;i++)if(p==bodies[i])return i+1;std::abort();}
void vec(CVector p){events.insert(events.end(),{p.vx,p.vy,p.vz});}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void __attribute__((thiscall)) unlink(void*p,void**head){events.insert(events.end(),{1,id(p),head==reinterpret_cast<void**>(0x60DAB0)?1:2});}
void __attribute__((thiscall)) destroy(void*p,int flags){events.insert(events.end(),{2,id(p),flags});}
void stop(unsigned handle){events.insert(events.end(),{3,handle,player[0xEF4],at<int>(player,0xECC),*reinterpret_cast<int*>(0x60FA9C)});}
void*recover(unsigned*h){void*p=reinterpret_cast<void*>(h[0]);int identity=id(p);events.insert(events.end(),{4,identity,h[1]});if(identity&&!valid[identity-1]){h[0]=0;return nullptr;}return p;}
// Null source handles have an unspecified validation word in the original heap ABI.
// This boundary returns the canonical modern empty handle; its pointer is the only recoverable identity.
unsigned long long make(void*p){assert(p==nullptr);events.push_back(9);return 0;}
void __attribute__((thiscall)) drop(void*p,CVector*v){events.insert(events.end(),{5,id(p)});vec(*v);events.insert(events.end(),{at<int>(player,0xE84),at<int>(player,0xE88)});}
void __attribute__((thiscall)) swing(void*p){events.insert(events.end(),{6,id(p)});}
void __attribute__((thiscall)) burst(void*p){events.insert(events.end(),{7,id(p),at<int>(player,0x5B0),at<int>(player,0x5AC)});}
void __attribute__((thiscall)) orient(void*p,bool target,CVector*v){events.insert(events.end(),{8,target});vec(*v);events.insert(events.end(),{player[0x54C],player[0xAD4],player[0x8E8],player[0x8E9],at<short>(p,0xA8),at<short>(p,0xAA),at<short>(p,0xAC),at<int>(p,0x548),at<int>(p,0xDF8)});vec(at<CVector>(p,0x60));}
void __attribute__((thiscall)) exitLook(void*p){events.push_back(10);vec(at<CVector>(p,0x60));events.insert(events.end(),{at<int>(p,0x548),at<int>(p,0xDF8)});}
void __attribute__((thiscall)) camX(void*,short value,unsigned short ticks){events.insert(events.end(),{11,0,value,ticks});}
void __attribute__((thiscall)) camY(void*,short value,unsigned short ticks){events.insert(events.end(),{11,1,value,ticks});}
void __attribute__((thiscall)) camZ(void*,short value,unsigned short ticks){events.insert(events.end(),{11,2,value,ticks});}
void __attribute__((thiscall)) camXZ(void*,unsigned short value,unsigned short ticks){events.insert(events.end(),{11,3,value,ticks});}
void __attribute__((thiscall)) camYD(void*,short value,unsigned short ticks){events.insert(events.end(),{11,4,value,ticks});}
void __attribute__((thiscall)) behind(void*p,int ticks){events.insert(events.end(),{12,ticks,at<int>(p,0x540)});}
void printVec(void*p,unsigned offset){auto&v=at<CVector>(p,offset);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x460280,(void*)&unlink);patch(0x4723D0,(void*)&stop);patch(0x4583F0,(void*)&recover);patch(0x458360,(void*)&make);patch(0x456E40,(void*)&drop);patch(0x4F7550,(void*)&swing);patch(0x4FAD50,(void*)&burst);patch(0x4C4BB0,(void*)&orient);patch(0x4C3810,(void*)&exitLook);patch(0x417AE0,(void*)&camX);patch(0x417B60,(void*)&camY);patch(0x417BE0,(void*)&camZ);patch(0x4179F0,(void*)&camXZ);patch(0x417A70,(void*)&camYD);patch(0x4C64A0,(void*)&behind);void*vt[]={reinterpret_cast<void*>(&destroy)};long long v[52];while(std::cin>>v[0]){for(int i=1;i<52;i++)std::cin>>v[i];alignas(4)unsigned char raw[0xF20]={},camera[0x300]={};player=raw;std::memset(bodies,0,sizeof(bodies));for(int i=0;i<12;i++){at<void*>(bodies[i],0)=vt;at<int>(bodies[i],0x54)=73+i;valid[i]=1;}auto ptr=[&](unsigned off,int identity,bool present){at<void*>(raw,off)=present?bodies[identity-1]:nullptr;};auto handle=[&](unsigned off,int identity,int kind,unsigned validator){ptr(off,identity,kind!=0);at<unsigned>(raw,off+4)=validator;valid[identity-1]=kind==1;};ptr(0x878,1,v[0]);at<unsigned>(raw,0xED0)=v[1];handle(0xED4,2,v[2],v[48]);ptr(0xE48,3,v[3]);ptr(0xE64,4,v[4]);ptr(0xE6C,5,v[5]);handle(0x5B8,6,v[6],v[49]);handle(0x5C0,7,v[7],v[50]);handle(0xAB8,8,v[8],v[51]);for(int i=0;i<4;i++)ptr(0x584+i*4,9+i,v[9]&(1<<i));raw[0x8EA]=v[10];at<int>(camera,0x2A0)=v[11];raw[0xEF4]=v[12];at<int>(raw,0xECC)=v[13];*reinterpret_cast<int*>(0x60FA9C)=v[14];for(int i=0;i<3;i++)at<int>(raw,0xE80+i*4)=v[15+i];at<int>(raw,0x5B0)=v[18];at<int>(raw,0x5AC)=v[19];raw[0x54C]=v[20];raw[0xAD4]=v[21];raw[0x8E8]=v[22];raw[0x8E9]=v[23];at<int>(raw,0x548)=v[24];at<int>(raw,0xDF8)=v[25];at<int>(raw,0x540)=v[26];at<int>(camera,0x12C)=v[27];for(int i=0;i<3;i++)at<short>(raw,0xA8+i*2)=v[28+i];at<CVector>(raw,0x60)=CVector(v[31],v[32],v[33]);at<CVector>(raw,0xC6C)=CVector(v[34],v[35],v[36]);at<CVector>(raw,0xC84)=CVector(v[37],v[38],v[39]);CVector direction(v[40],v[41],v[42]);*reinterpret_cast<void**>(0x56F3B8)=camera;unsigned floorOffsets[]={0x6A81DC,0x6A81DE,0x6A81E0,0x6A81FA,0x6A8274};for(int i=0;i<5;i++)*reinterpret_cast<unsigned short*>(floorOffsets[i])=v[43+i];events.clear();reinterpret_cast<void(__attribute__((thiscall)) *)(void*,CVector)>(0x4B9420)(raw,direction);std::cout<<'['<<unsigned(raw[0xEF4])<<','<<id(at<void*>(raw,0x878))<<','<<at<int>(raw,0xECC)<<','<<*reinterpret_cast<int*>(0x60FA9C)<<','<<at<unsigned>(raw,0xED0);for(unsigned off:{0xED4u,0x5B8u,0x5C0u,0xAB8u})std::cout<<','<<id(at<void*>(raw,off))<<','<<at<unsigned>(raw,off+4);for(unsigned off:{0xE48u,0xE64u,0xE6Cu})std::cout<<','<<id(at<void*>(raw,off));for(int i=0;i<3;i++)std::cout<<','<<at<int>(raw,0xE80+i*4);std::cout<<','<<at<int>(raw,0x5B0)<<','<<at<int>(raw,0x5AC);for(int i=0;i<4;i++)std::cout<<','<<id(at<void*>(raw,0x584+i*4))<<','<<at<int>(bodies[8+i],0x54);for(unsigned off:{0x54Cu,0xAD4u,0x8E8u,0x8E9u})std::cout<<','<<unsigned(raw[off]);for(int i=0;i<3;i++)std::cout<<','<<at<short>(raw,0xA8+i*2);printVec(raw,0x60);std::cout<<','<<at<int>(raw,0x548)<<','<<at<int>(raw,0xDF8)<<','<<unsigned(raw[0x8EA])<<','<<at<int>(camera,0x12C)<<','<<at<int>(raw,0x540)<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    base = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 173, 123, 1, -77777, 111, 222,
            345, 456, 173, 1, 174, 175, 123456, 77, 88, 99, 111, -222, 333,
            12345, -23456, 34567, 123, -456, 4096, 777, -4096, 999,
            4096, 0, 1234, -23, 45, -67, 65535, -89, 1002, 1006, 1007, 1008]
    cases = []
    for mask in range(256):
        case = base.copy()
        case[:9] = [mask & 1, 0x80000001 if mask & 2 else 0, 1 if mask & 4 else 0,
                    (mask >> 3) & 1, (mask >> 4) & 1, (mask >> 5) & 1,
                    1 if mask & 64 else 0, 1 if mask & 128 else 0, 1]
        case[9] = mask & 15
        case[10] = (mask >> 7) & 1
        cases.append(case)
    for kinds in itertools.product(range(3), repeat=4):
        for mode in [0, 3, 6, -1]:
            case = base.copy(); case[2], case[6], case[7], case[8] = kinds
            case[0:2] = [1, 77]; case[3:6] = [1, 1, 1]
            case[9:12] = [15, 173, mode]
            cases.append(case)
    rng = random.Random(0x4B9420)
    for _ in range(200):
        case = base.copy()
        for i in [0, 3, 4, 5]: case[i] = rng.randrange(2)
        for i in [2, 6, 7, 8]: case[i] = rng.randrange(3)
        case[1] = rng.randrange(1 << 32); case[9] = rng.randrange(16)
        case[10] = rng.randrange(256); case[11] = rng.choice([0, 3, 6, -1])
        for i in [12, 20, 21, 22, 23]: case[i] = rng.randrange(256)
        for i in [13, 14, *range(15, 20), *range(24, 28), *range(31, 43)]:
            case[i] = rng.randint(-2147483648, 2147483647)
        for i in [*range(28, 31), *range(43, 48)]: case[i] = rng.randint(-32768, 32767)
        for i in range(48, 52): case[i] = rng.randrange(1 << 32)
        cases.append(case)
    with tempfile.TemporaryDirectory(prefix='openspidey-distance-prepare-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        requests = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=requests).decode().splitlines()
    result = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-distance-attack-preparation.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original4B9420 cleanup cases: actual typed body destructor/recovery order, stale validators, wrapped held drop, four trail fades, orientation and camera mode3 gates.')


if __name__ == '__main__':
    main()
