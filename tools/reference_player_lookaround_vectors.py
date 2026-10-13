"""Capture bounded original-PC lookaround input and scalar camera-angle slices.

The input slice starts immediately after ReadAnalogueInput and ends before the
movement-ramp stage. The SetupLookaroundCamera slice retains its real prologue,
counter/joint/heading/smoothing code and epilogue; matrix/path leaves are explicit
observers and execution stops before downstream camera/target selection.
"""
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
#include "original_executable.h"
#include <iostream>
#include <vector>
std::vector<long long> events; int effectiveHeading;
template<class T>T& at(void*p,unsigned o){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+o);}
template<class T>T& global(unsigned a){return *reinterpret_cast<T*>(a);}
void emit(std::initializer_list<long long>v){events.insert(events.end(),v);}
void patch(unsigned a,void*target){global<unsigned char>(a)=0xE9;global<unsigned>(a+1)=reinterpret_cast<unsigned>(target)-a-5;}
void __attribute__((thiscall)) enter(void*p){emit({1});if(at<int>(p,0xCE4))return;at<unsigned char>(p,0x8EA)=1;at<int>(p,0xCB4)=24;at<int>(p,0xCE4)=0;global<short>(0x6A8D44)=effectiveHeading;global<int>(0x6A818C)=170;}
void __attribute__((thiscall)) leave(void*p){emit({2});at<unsigned char>(p,0x8EA)=0;at<int>(p,0xCB4)=at<int>(p,0xCE4)=at<int>(p,0x56C)=0;}
short __attribute__((thiscall)) heading(void*){emit({3});return effectiveHeading;}
void __attribute__((thiscall)) torso(void*,short target,bool fast){emit({4,target,fast});}
void screen(bool){} void qtom(void*,void*){} void matrix(void*,void*){} void dispose(void*){}
extern "C" __attribute__((naked)) void input_done(){asm volatile("pop %edi;pop %esi;pop %ebp;pop %ebx;ret");}
extern "C" __attribute__((naked)) void input_step(void*){asm volatile("push %ebx;push %ebp;push %esi;push %edi;mov 20(%esp),%esi;xor %ebx,%ebx;mov $0x4B1CFE,%eax;jmp *%eax");}
void print(const std::vector<long long>& values){std::cout<<'[';for(unsigned i=0;i<values.size();i++){if(i)std::cout<<',';std::cout<<values[i];}std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4B1FCD,reinterpret_cast<void*>(&input_done));patch(0x4C3580,reinterpret_cast<void*>(&enter));patch(0x4C3810,reinterpret_cast<void*>(&leave));patch(0x4C6AA0,reinterpret_cast<void*>(&heading));patch(0x4C6970,reinterpret_cast<void*>(&torso));
patch(0x48AA40,reinterpret_cast<void*>(&screen));patch(0x47C7F0,reinterpret_cast<void*>(&qtom));patch(0x46E730,reinterpret_cast<void*>(&matrix));patch(0x458210,reinterpret_cast<void*>(&dispose));patch(0x4C3B8D,reinterpret_cast<void*>(0x4C46EF));
int kind;while(std::cin>>kind){events.clear();alignas(4)unsigned char player[0xEC0]={},pad[0x134]={},camera[0x2A4]={},path[0x180]={};short joints[24]={};long long v[32]={};int count=kind?17:30;for(int i=0;i<count;i++)std::cin>>v[i];
if(!kind){pad[0x40]=v[0];pad[0x70]=v[1];pad[0x71]=v[2];pad[0x100]=v[3];pad[0x101]=v[4];at<unsigned char>(player,0x8EA)=v[5];at<unsigned char>(player,0x8EB)=v[6];at<unsigned char>(player,0x550)=v[7];at<unsigned char>(player,0xE8D)=v[8];at<unsigned char>(player,0x54F)=v[9];at<int>(player,0x56C)=v[10];at<unsigned>(player,0x8E4)=v[11];at<unsigned>(player,0xE1C)=v[12];at<void*>(player,0xE48)=v[13]?path:nullptr;at<unsigned char>(player,0xEA4)=v[14];at<signed char>(player,0xE2D)=v[15];at<signed char>(player,0xE2E)=v[16];at<int>(player,0x8F0)=v[17];at<int>(player,0x80)=v[18];at<unsigned char>(player,0x8E0)=v[19];at<int>(player,0xCB4)=v[20];at<int>(player,0xCE4)=v[21];global<int>(0x6A818C)=v[22];global<int>(0x6A7FFC)=v[23];global<int>(0x6A82B4)=333;global<int>(0x6A8D54)=-444;at<unsigned char>(player,0x8E9)=v[24];effectiveHeading=v[25];at<int>(camera,0x2A0)=v[26];global<void*>(0x56F3B8)=camera;global<unsigned char>(0x60CFC7)=v[27];global<short>(0x6B4830)=v[28];at<void*>(player,0xE64)=v[29]?path:nullptr;at<void*>(player,0xE0C)=pad;at<unsigned char>(player,0x2C1)=77;at<unsigned char>(player,0x231)=88;
input_step(player);print({pad[0x40],pad[0x70],pad[0x71],pad[0x100],pad[0x101],at<unsigned char>(player,0x8EA),at<unsigned char>(player,0x550),at<unsigned char>(player,0xE8D),at<unsigned char>(player,0x54F),at<int>(player,0x56C),at<unsigned>(player,0x8E4),at<int>(player,0xCB4),at<int>(player,0xCE4),global<int>(0x6A818C),global<int>(0x6A7FFC),global<int>(0x6A82B4),global<int>(0x6A8D54),at<unsigned char>(player,0x2C1),at<unsigned char>(player,0x231)});
}else{at<unsigned char>(player,0x8EA)=v[0];at<int>(player,0xCB4)=v[1];at<int>(player,0xCE4)=v[2];at<int>(player,0x80)=v[3];global<short>(0x6A8D44)=v[4];effectiveHeading=v[5];at<unsigned char>(player,0x8E9)=v[6];global<int>(0x6A818C)=v[7];global<int>(0x6A7FFC)=v[8];global<int>(0x6A82B4)=v[9];global<int>(0x6A8D54)=v[10];joints[6]=v[11];joints[7]=v[12];joints[18]=v[13];joints[19]=v[14];at<void*>(player,0x188)=joints;at<int>(player,0xDF8)=v[15];global<int>(0x660F80)=v[16];global<int>(0x60F76C)=v[16];at<void*>(player,0xC90)=path;
reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x4C38A0)(player);print({at<unsigned char>(player,0x8EA),at<int>(player,0xCB4),at<int>(player,0xCE4),global<short>(0x6A8D44),global<int>(0x6A818C),global<int>(0x6A7FFC),global<int>(0x6A82B4),global<int>(0x6A8D54),joints[6],joints[7],joints[18],joints[19],at<int>(player,0xDF8),global<int>(0x660F80),global<int>(0x60F76C)});}}}
'''
    rng = random.Random(0x4C38A0)
    inputs = []
    for active in [0, 1]:
        for mode in range(8):
            for hold in [14, 15, 16, -1, 2147483647]:
                inputs.append([1, 0, 0, 0, 0, active, 1, 1, 0, 0, hold, 15, 1, 0, 4, -127, 127, 256, 2, 0, 0, 0, 170, 1024, 0, 1357, mode, 0, 0, 0])
    for _ in range(2400):
        inputs.append([rng.choice([0, 1, 255]) for _ in range(10)] + [rng.choice([0, 14, 15, 16, -1, 2147483647]), rng.choice([0, 15, 0xFFFF000F, 0x80000000]), rng.choice([1, 2, 4, 0x10000, 0x1C000, 0x20000, 0x2000000, 0x3EBE3FCE]), rng.randrange(2), rng.choice([0, 3, 4]), rng.choice([-128, -127, -1, 0, 1, 127]), rng.choice([-128, -127, -1, 0, 1, 127]), rng.choice([0, 16, 255, 256, 0x7FFFFFFF]), rng.choice([0, 1, 2, 6]), rng.choice([0, 1]), rng.choice([0, 1, 24]), rng.choice([0, 1, 24]), rng.choice([-2147483648, -1025, -1024, -170, 0, 170, 1024, 1025, 2147483647]), rng.choice([-2147483648, -2048, -1025, -1024, 0, 1024, 1025, 2048, 2147483647]), rng.randrange(2), rng.choice([0, 511, 512, 1357, 4095]), rng.randrange(8), rng.randrange(2), rng.randrange(2), rng.randrange(2)])
    angles = []
    for previous, current in [(4095, 0), (0, 4095), (0, 512), (0, 513), (513, 0), (512, 0), (1234, 1234)]:
        for entry, exit in [(0, 0), (24, 0), (2, 0), (0, 24), (0, 2), (2, 2)]:
            for ceiling in [0, 1]:
                angles.append([1, entry, exit, 2, previous, current, ceiling, 170, 1025, 0, -1025, -32768, -3, 32767, 3, 77, 13])
    for _ in range(2400):
        angles.append([rng.randrange(2), rng.choice([0, 1, 2, 6, 24]), rng.choice([0, 1, 2, 6, 24]), rng.choice([0, 1, 2, 6]), rng.choice([0, 512, 513, 2048, 4095, -32768, 32767]), rng.choice([0, 512, 513, 2048, 4095, -32768, 32767]), rng.randrange(2)] + [rng.choice([-2147483648, -1025, -192, -1, 0, 1, 192, 1025, 2147483647]) for _ in range(4)] + [rng.choice([-32768, -1025, -3, 0, 3, 1025, 32767]) for _ in range(4)] + [77, 13])
    cases = [(0, case) for case in inputs] + [(1, case) for case in angles]
    with tempfile.TemporaryDirectory(prefix='openspidey-player-lookaround-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(' '.join(map(str, [kind] + case)) for kind, case in cases) + '\n').encode()).decode().splitlines()
    output = root / 'modern/OpenSpidey.Tests/Fixtures/player-lookaround.json'
    output.write_text(json.dumps([dict(kind=kind, input=case, expected=json.loads(line)) for (kind, case), line in zip(cases, lines, strict=True)], separators=(',', ':')) + '\n')
    print(f'Captured {len(inputs)} original PC input slices and {len(angles)} scalar SetupLookaroundCamera slices.')


if __name__ == '__main__':
    main()
