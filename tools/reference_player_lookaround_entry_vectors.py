"""Capture actual PC EnterLookaroundMode, retaining original MToQ/QToM/Slerp and source camera-to-body path ordering."""
import argparse
import json
import math
from pathlib import Path
import random
import subprocess
import tempfile
import shutil


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "core/fixed_vector.h"
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <cmath>
struct Quat{int x,y,z,w;}; struct Matrix{short m[3][3];int t[3];};
alignas(4) Quat path[24]; CVector hook,cameraPosition;int effectiveHeading;std::vector<long long>events;
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
void patch(unsigned a,void*target){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(target)-a-5;}
short __attribute__((thiscall)) heading(void*){emit({1});return effectiveHeading;}
void __attribute__((thiscall)) position(void*,CVector*out){emit({2});*out=cameraPosition;}
void hookPosition(CVector*out,void*,int index){emit({3,index});*out=hook;}
void __attribute__((thiscall)) push(void*){emit({4});}
int __attribute__((thiscall)) mode(void*,int value){emit({5,value});return 0;}
void*allocate(unsigned bytes,int second,int third,void*fourth,bool fifth){emit({6,bytes,second,third,fourth?1:0,fifth});return path;}
void vec(std::vector<long long>&v,const CVector&p){v.insert(v.end(),{p.vx,p.vy,p.vz});}
void quat(std::vector<long long>&v,const Quat&q){v.insert(v.end(),{q.x,q.y,q.z,q.w});}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096.0;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096.0;}patch(0x4C6AA0,reinterpret_cast<void*>(&heading));patch(0x416A00,reinterpret_cast<void*>(&position));patch(0x453D60,reinterpret_cast<void*>(&hookPosition));patch(0x416720,reinterpret_cast<void*>(&push));patch(0x4167F0,reinterpret_cast<void*>(&mode));patch(0x4581A0,reinterpret_cast<void*>(&allocate));
long long v[25];while(std::cin>>v[0]){for(int i=1;i<25;i++)std::cin>>v[i];alignas(4)unsigned char player[0xEC0]={},camera[0x2A4]={};Quat body{int(v[0]),int(v[1]),int(v[2]),int(v[3])};Quat cam{int(v[4]),int(v[5]),int(v[6]),int(v[7])};at<Quat>(camera,0x214)=cam;at<CVector>(player,0xC84)=CVector(v[8],v[9],v[10]);hook=CVector(v[11],v[12],v[13]);cameraPosition=CVector(v[14],v[15],v[16]);effectiveHeading=v[17];player[0x8E8]=v[18];player[0x8E9]=v[19];at<int>(player,0xCE4)=v[20];at<int>(player,0xDF8)=77;*reinterpret_cast<int*>(0x6A8260)=v[21];*reinterpret_cast<int*>(0x6A81FC)=v[22];*reinterpret_cast<int*>(0x6A8208)=v[23];*reinterpret_cast<int*>(0x6A818C)=v[24];*reinterpret_cast<void**>(0x56F3B8)=camera;*reinterpret_cast<short*>(0x6A8D44)=888;
reinterpret_cast<void(*)(Quat*,Matrix*)>(0x47C7F0)(&body,reinterpret_cast<Matrix*>(player+0x164));events.clear();std::memset(path,0xCD,sizeof(path));reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C3580)(player);
std::vector<long long>out{player[0x8EA],at<int>(player,0xCB4),at<int>(player,0xCE4),at<int>(player,0xDF8),*reinterpret_cast<short*>(0x6A8D44),*reinterpret_cast<int*>(0x6A818C)};vec(out,at<CVector>(player,0xD0C));vec(out,at<CVector>(player,0xD00));vec(out,at<CVector>(player,0xCB8));quat(out,at<Quat>(player,0xC94));quat(out,at<Quat>(player,0xCA4));
std::cout<<'[';for(unsigned i=0;i<out.size();i++){if(i)std::cout<<',';std::cout<<out[i];}std::cout<<",[";if(at<void*>(player,0xC90)){for(int i=0;i<24;i++){if(i)std::cout<<',';std::cout<<'['<<path[i].x<<','<<path[i].y<<','<<path[i].z<<','<<path[i].w<<']';}}std::cout<<"],[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    rng = random.Random(0x4C3580)
    def quaternion():
        parts = [rng.uniform(-1, 1) for _ in range(4)]; length = math.sqrt(sum(value * value for value in parts))
        return [int(value * 4096 / length) for value in parts]
    cases = []
    for body, camera in [([0, 0, 0, 4096], [0, 0, 0, 4096]), ([0, 4096, 0, 0], [0, 0, 0, 4096]), ([0, 0, 0, -4096], [0, -4096, 0, 0])]:
        for wall, ceiling in [(0, 0), (1, 0), (0, 1), (1, 1)]:
            for exit in [0, 1]:
                cases.append(body + camera + [0, -4096, 0, 100000, -200000, 300000, -123456, 234567, -345678, 1357, wall, ceiling, exit, 170, -170, 321, 777])
    for _ in range(120):
        cases.append(quaternion() + quaternion() + [rng.randint(-4096, 4096) for _ in range(3)] +
                     [rng.randint(-100000000, 100000000) for _ in range(6)] + [rng.randrange(4096), rng.randrange(2), rng.randrange(2), rng.choice([0, 0, 0, 1]), 170, -170, 321, 777])
    with tempfile.TemporaryDirectory(prefix='openspidey-lookaround-entry-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        requests = '\n'.join(' '.join(map(str, case)) for case in cases) + '\n'
        try:
            lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=requests.encode()).decode().splitlines()
        except subprocess.CalledProcessError as error:
            shutil.copy2(cpp, '/tmp/openspidey-lookaround-entry-reference.cpp')
            shutil.copy2(binary, '/tmp/openspidey-lookaround-entry-reference')
            Path('/tmp/openspidey-lookaround-entry-cases.txt').write_text(requests)
            print('Completed rows before failure:', error.output.count(b'\n'))
            raise
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-lookaround-entry.json').write_text(json.dumps([dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original PC entry cases, including all24 real quaternion path samples and original leaf/camera/allocation order.')


if __name__ == '__main__':
    main()
