"""Capture original PC 416B10 mode3/6/8/15 terrain pull-in; only the line query transport and player heading are scripted."""
import argparse
import json
import math
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
#include <cmath>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
template<class T>T&mem(unsigned n){return *reinterpret_cast<T*>(n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
alignas(4)unsigned char item[0x40];
struct Hit{int present,distance,x,y,z,nx,ny,nz;};std::vector<Hit>script;unsigned cursor;std::vector<int>events;int diagnostics,headingCalls,heading;
void diagnostic(int condition,const char*message){if((condition&255)==0){assert(!strcmp(message,"No valid tripod"));diagnostics++;}}
void init(int*line){}
int effectiveHeading(){headingCalls++;return heading;}
void query(int*line,int flag){assert(flag==1&&mem<int>(0x5FBEDC)==1);events.insert(events.end(),{line[0],line[1],line[2],line[3],line[4],line[5]});
 Hit hit=cursor<script.size()?script[cursor]:Hit{};cursor++;
 at<void*>(line,0x68)=hit.present?item:nullptr;at<int>(line,0x40)=hit.distance;at<int>(line,0x6C)=hit.x;at<int>(line,0x70)=hit.y;at<int>(line,0x74)=hit.z;
 at<short>(line,0x78)=hit.nx;at<short>(line,0x7A)=hit.ny;at<short>(line,0x7C)=hit.nz;}
void print(void*p,unsigned offset,int count){for(int i=0;i<count;i++)std::cout<<','<<at<int>(p,offset+4*i);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
patch(0x4015B0,(void*)&diagnostic);patch(0x4524C0,(void*)&init);patch(0x4549A0,(void*)&query);patch(0x4C6AA0,(void*)&effectiveHeading);
for(int i=0;i<4096;i++){reinterpret_cast<short*>(0x610C48)[2*i]=sin(i*0.001536096911877394)*4096;reinterpret_cast<short*>(0x610C48)[2*i+1]=cos(i*0.001536096911877394)*4096;}
long long v[37];while(std::cin>>v[0]){for(int i=1;i<37;i++)std::cin>>v[i];
 int hits;std::cin>>hits;script.assign(hits,{});for(Hit&h:script)std::cin>>h.present>>h.distance>>h.x>>h.y>>h.z>>h.nx>>h.ny>>h.nz;
 cursor=0;events.clear();diagnostics=0;headingCalls=0;
 alignas(4)unsigned char camera[0x300]={},tripod[0x100]={},player[0x1000]={};
 at<unsigned>(camera,0x2A0)=v[0];at<void*>(camera,0xFC)=v[1]?tripod:nullptr;at<unsigned char>(camera,0x100)=v[2];mem<int>(0x5FAE98)=0;
 at<int>(camera,0x104)=v[3];at<int>(camera,0x108)=v[4];at<int>(camera,0x10C)=v[5];
 mem<int>(0x56F260)=v[6];mem<int>(0x56F264)=v[7];mem<int>(0x56F268)=v[8];
 at<int>(camera,8)=v[9];at<int>(camera,12)=v[10];at<int>(camera,16)=v[11];
 for(int i=0;i<4;i++){at<int>(camera,0x1E4+4*i)=v[12+i];at<int>(camera,0x1F4+4*i)=v[16+i];}
 at<int>(camera,0x1D0)=v[20];mem<int>(0x54885C)=v[21];mem<int>(0x548858)=v[22];
 at<short>(camera,0x234)=v[23];at<short>(camera,0x236)=v[24];at<short>(camera,0x238)=v[25];
 at<unsigned short>(player,4)=v[26];at<unsigned char>(player,0x8E8)=v[27];at<int>(player,0x60)=v[28];at<int>(player,0x68)=v[29];
 at<unsigned char>(player,0x57C)=v[30];heading=v[31];mem<int>(0x56F1AC)=v[32];mem<int>(0x56F254)=v[33];
 at<unsigned char>(camera,0x180)=255;at<unsigned char>(camera,0x1A4)=255;at<int>(camera,0x1D4)=v[34];at<unsigned char>(camera,0xF9)=0;
 for(int i=0;i<3;i++){at<int>(camera,0x60+4*i)=v[35]+i;at<int>(camera,0x1B8+4*i)=v[36]+i;}
 mem<void*>(0x6A9038)=player;mem<void*>(0x56F3B8)=camera;mem<int>(0x5FBEDC)=0;
 reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x416B10)(camera);
 assert(mem<int>(0x5FBEDC)==0);
 std::cout<<'['<<diagnostics;print(camera,8,3);print(camera,0x60,3);std::cout<<','<<at<int>(camera,0x1D0)<<','<<at<int>(camera,0x1D4);
 print(camera,0x1E4,4);print(camera,0x204,4);print(camera,0x214,4);print(camera,0x1B8,3);
 std::cout<<','<<unsigned(at<unsigned char>(camera,0x180))<<','<<unsigned(at<unsigned char>(camera,0x1A4))<<','<<at<unsigned short>(player,4);
 std::cout<<','<<at<short>(camera,0x236)<<','<<mem<int>(0x56F1AC)<<','<<mem<int>(0x56F254)<<','<<headingCalls<<",[";
 for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";
}}
'''
    rng = random.Random(0x416DAF)

    def unit(scale=4096):
        values = [rng.gauss(0, 1) for _ in range(3)]
        length = math.sqrt(sum(value * value for value in values))
        return [round(value / length * scale) for value in values]

    def quaternion():
        values = [rng.gauss(0, 1) for _ in range(4)]
        length = math.sqrt(sum(value * value for value in values))
        return [round(value / length * 4096) for value in values]

    def normal():
        x, y, z = unit()
        if rng.random() < 0.25: y = rng.choice([-2601, -2600, 3400, 3401, 4096, -4096, 0])
        return [x, y, z]

    def hit(distance, end):
        if rng.random() < 0.25: return [0, 0, 0, 0, 0, 0, 0, 0]
        choices = [0, distance, distance + 32, distance + 256, distance + 257, rng.randint(-100, 3000), 0x7FFFFFFF]
        return [1, rng.choice(choices), *[value + rng.randint(-2 ** 18, 2 ** 18) for value in end], *normal()]

    cases = []
    for _ in range(1500):
        current = quaternion()
        desired = rng.choice([current, quaternion(), [-value for value in current]])
        distance = rng.choice([1, 150, 200, 400, rng.randint(1, 2000)])
        focus = [rng.randint(-2 ** 26, 2 ** 26) for _ in range(3)]
        offset = [rng.randint(-2 ** 18, 2 ** 18) for _ in range(3)]
        end = [f + o for f, o in zip(focus, offset)]
        speed = [rng.choice([0, 0, rng.randint(-2 ** 14, 2 ** 14)]) for _ in range(2)]
        row = [rng.choice([3, 3, 6, 8, 15]), rng.randrange(2), rng.choice([0, 1, 1, 255]), *focus, *offset,
               *[rng.randint(-2 ** 26, 2 ** 26) for _ in range(3)], *current, *desired, rng.randint(-3000, 6000), distance,
               rng.randrange(-8192, 8192), *[rng.randint(-32768, 32767) for _ in range(3)], rng.randrange(65536),
               rng.choice([0, 0, 0, 1]), *speed, rng.choice([0, 0, 1]), rng.randint(-32768, 32767),
               rng.randint(-5000, 5000), rng.randint(-5, 5), rng.randint(-2000, 8000), rng.randint(-2 ** 26, 2 ** 26),
               rng.randint(-2 ** 26, 2 ** 26)]
        script = [hit(distance, end) for _ in range(3)]
        if rng.random() < 0.5:
            script[0][0] = 1
            script[0][1] = rng.choice([distance, distance + 32, rng.randint(0, distance + 200)])
        cases.append(dict(input=row, hits=script))
    rows = [case['input'] + [len(case['hits'])] + [value for hit in case['hits'] for value in hit] for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-original-camera-collision-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(' '.join(map(str, row)) for row in rows) + '\n')
        if completed.returncode:
            done = len(completed.stdout.splitlines())
            raise RuntimeError(f'Original camera collision fixture failed{completed.returncode} at case{done}: {completed.stderr}')
        output = completed.stdout.splitlines()
        if len(output) != len(cases): raise RuntimeError(f'Expected{len(cases)} cases, got{len(output)}')
    for case, line in zip(cases, output, strict=True):
        case['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/camera-collision-placement.json').write_text(json.dumps(cases, separators=(',', ':')) + '\n')
    calls = [len(case['expected'][-1]) // 6 for case in cases]
    print(f'Captured {len(cases)} original PC mode3/6/8/15 placements: rays per case {[calls.count(n) for n in range(4)]}, '
          f'collisions {sum(case["expected"][24] == 1 for case in cases)}, heading retries {sum(case["expected"][-2] for case in cases)}.')


if __name__ == '__main__': main()
