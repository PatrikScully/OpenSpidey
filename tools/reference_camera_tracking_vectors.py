"""Capture real PC SetCamAngle/timed yaw, posed tripod easing, and complete PutCameraBehind callback order."""
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
#include <cmath>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
std::vector<long long>events;CVector hookPosition;short effectiveHeading;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
extern "C" __attribute__((naked)) void sliceEnd(){asm volatile("pop %edi;pop %ebp;pop %esi;pop %ebx;add $0x60,%esp;ret");}
extern "C" __attribute__((naked)) void yawStep(void*){asm volatile("sub $0x60,%esp;push %ebx;push %esi;push %ebp;push %edi;mov 0x74(%esp),%esi;mov 0x80(%esi),%ecx;mov $0x4182ED,%eax;jmp *%eax");}
extern "C" __attribute__((naked)) void tripodStep(void*){asm volatile("sub $0x60,%esp;push %ebx;push %esi;push %ebp;push %edi;mov 0x74(%esp),%esi;lea 8(%esi),%eax;xor %edx,%edx;mov $0x417ED0,%ecx;jmp *%ecx");}
void hook(void*out,void*,int index){events.push_back(index);at<CVector>(out,0)=hookPosition;}
short __attribute__((thiscall)) heading(void*){events.push_back(1);return effectiveHeading;}
void __attribute__((thiscall)) setAngle(void*,short value,unsigned short duration){events.insert(events.end(),{2,value,duration});}
void __attribute__((thiscall)) setVertical(void*,short value,unsigned short duration){events.insert(events.end(),{3,value,duration});}
void printVector(void*p,unsigned offset){auto&v=at<CVector>(p,offset);std::cout<<','<<v.vx<<','<<v.vy<<','<<v.vz;}
void printEvents(){std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);int family=atoi(argv[2]);
if(family==0)patch(0x4183BC,(void*)&sliceEnd);
if(family==1){patch(0x41805D,(void*)&sliceEnd);patch(0x453D60,(void*)&hook);}
if(family==2){patch(0x4C6AA0,(void*)&heading);patch(0x4178E0,(void*)&setAngle);patch(0x417A70,(void*)&setVertical);for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}}
long long v[24];while(std::cin>>v[0]){int count=family==0?10:family==1?24:13;for(int i=1;i<count;i++)std::cin>>v[i];events.clear();
alignas(4)unsigned char camera[0x2A4]={};
if(family==0){at<unsigned>(camera,0x2A0)=v[0];at<short>(camera,0x236)=v[1];*reinterpret_cast<int*>(0x56F254)=v[4];*reinterpret_cast<int*>(0x56F38C)=v[5];*reinterpret_cast<int*>(0x56F28C)=v[6];*reinterpret_cast<int*>(0x56F1AC)=v[7];at<int>(camera,0x80)=v[8];reinterpret_cast<void(__attribute__((thiscall)) *)(void*,short,unsigned short)>(0x4178E0)(camera,v[2],v[3]);std::cout<<'[';for(int i=0;i<=v[9];i++){if(i)std::cout<<',';std::cout<<'['<<at<short>(camera,0x236)<<','<<*reinterpret_cast<int*>(0x56F1AC)<<','<<*reinterpret_cast<int*>(0x56F254)<<','<<*reinterpret_cast<int*>(0x56F38C)<<','<<*reinterpret_cast<int*>(0x56F28C)<<']';yawStep(camera);}std::cout<<"]\n";}
if(family==1){alignas(4)unsigned char body[0x100]={};at<CVector>(camera,0x104)=CVector(v[0],v[1],v[2]);at<CVector>(body,8)=CVector(v[3],v[4],v[5]);hookPosition=CVector(v[6],v[7],v[8]);at<void*>(camera,0xFC)=v[9]?body:nullptr;at<unsigned char>(camera,0x100)=v[10];at<unsigned short>(body,0x46)=v[11]?0x40:0;at<int>(camera,0x12C)=v[12];at<int>(camera,0x128)=v[13];at<CVector>(camera,0x110)=CVector(v[14],v[15],v[16]);at<CVector>(camera,0x11C)=CVector(v[17],v[18],v[19]);at<int>(camera,0x130)=v[20];at<int>(camera,0x134)=v[21];at<int>(camera,0x138)=v[22];at<int>(camera,0x80)=v[23];tripodStep(camera);std::cout<<'['<<at<int>(camera,0x128);printVector(camera,0x104);printEvents();std::cout<<"]\n";}
if(family==2){alignas(4)unsigned char player[0xEC0]={};player[0x8EC]=v[0];player[0x8E8]=v[1];effectiveHeading=v[2];at<CVector>(player,0xC84)=CVector(v[3],v[4],v[5]);at<unsigned>(camera,0x2A0)=v[6];player[0xE2D]=v[7];player[0xE2E]=v[8];at<unsigned>(player,0xE1C)=v[9];at<short>(player,0xE32)=v[10];*reinterpret_cast<short*>(0x6A8C66)=v[11];*reinterpret_cast<void**>(0x56F3B8)=camera;reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int)>(0x4C64A0)(player,v[12]);std::cout<<'[';for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]\n";}
}}
'''
    rng = random.Random(0x4178E0)
    heading = [[mode, initial, wanted, duration, 7, 1357, -23, 999, ticks, 18]
               for mode in [3, 4, 5, 7, 15, 16, 17] for initial, wanted in [(0, 2048), (2048, 0), (4095, 1), (1, 4095), (-32768, 32767), (13, 13)]
               for duration in [0, 1, 3, 16, 65535] for ticks in [1, 2, 6]]
    heading += [[rng.randrange(24), rng.randint(-32768, 32767), rng.randint(-32768, 32767), rng.randrange(65536), rng.randint(-100, 100),
                 rng.randint(-32768, 32767), rng.randint(-2147483648, 2147483647), rng.randint(-2147483648, 2147483647), rng.choice([0, 1, 2, 6, 32768, 65535]), 18] for _ in range(160)]
    tripod = [[*[rng.randint(-2147483648, 2147483647) for _ in range(9)], rng.randrange(2), rng.randrange(2), rng.randrange(2), rng.choice([-1, 0, 2, 8]),
               rng.choice([0, 1, 2, 16, -1]), *[rng.randint(-1000000, 1000000) for _ in range(6)], rng.choice([0, 4, 16, -1, 255]),
               rng.choice([0, 8, 16, -1, 255]), rng.choice([0, 4, 16, -1, 255]), rng.choice([0, 1, 2, 6])] for _ in range(384)]
    behind = [[locked, wall, 1357, 2048, -3072, 1024, mode, axis, 0, action, angle, 123, 16]
              for locked in [0, 1] for wall in [0, 1] for mode in [3, 4, 7] for axis in [0, -127]
              for action in [1, 16] for angle in [0, 1024, 2048, 4095]]
    behind += [[rng.randrange(2), rng.randrange(2), rng.randint(-32768, 32767), *[rng.randint(-1000000, 1000000) for _ in range(3)], rng.randrange(24),
                rng.randint(-128, 127), rng.randint(-128, 127), rng.choice([1, 16]), rng.randint(-32768, 32767), rng.randint(-32768, 32767),
                rng.randint(-2147483648, 2147483647)] for _ in range(160)]
    output = []
    with tempfile.TemporaryDirectory(prefix='openspidey-camera-tracking-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), str(root / 'src/core/fixed_vector.cpp'), '-o', str(binary)], check=True)
        for family, name, cases in [(0, 'heading', heading), (1, 'tripod', tripod), (2, 'behind', behind)]:
            requests = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
            lines = subprocess.check_output([str(binary), str(args.original_exe.resolve()), str(family)], input=requests).decode().splitlines()
            output += [dict(kind=name, input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-tracking.json').write_text(json.dumps(output, separators=(',', ':')) + '\n')
    print(f'Captured {len(heading)} original PC timed-heading sequences, {len(tripod)} posed-tripod slices and {len(behind)} complete PutCameraBehind calls.')


if __name__ == '__main__': main()
