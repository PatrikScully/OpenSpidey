"""Capture whole original489660 cubic sampling and488370 Scorpion pose-hook/tail-history update."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


HARNESS = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
struct Vector { int x,y,z; };
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>((unsigned char*)p+n);}
void patch(unsigned address,void*target){auto*p=(unsigned char*)address;p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=(unsigned)target-address-5;}
std::array<Vector,7> hooks;
std::vector<long long>events;
void append(std::vector<long long>&values,const Vector*v,unsigned count){for(unsigned i=0;i<count;i++)values.insert(values.end(),{v[i].x,v[i].y,v[i].z});}
void dynamicHook(Vector*out,void*actor,const short*hook){assert(hook[0]==0&&hook[1]==0&&hook[2]==0);unsigned part=(unsigned short)hook[3];assert(part>=17&&part<=23);events.push_back(part);
 append(events,(Vector*)((unsigned char*)actor+0x48C),4);append(events,(Vector*)((unsigned char*)actor+0x4BC),23);append(events,(Vector*)((unsigned char*)actor+0x5D0),128);*out=hooks[part-17];}
void read(Vector&v){std::cin>>v.x>>v.y>>v.z;}
void vectors(const Vector*v,unsigned count){std::cout<<'[';for(unsigned i=0;i<count;i++){if(i)std::cout<<',';std::cout<<'['<<v[i].x<<','<<v[i].y<<','<<v[i].z<<']';}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x453EE0,(void*)&dynamicHook);int family;
 while(std::cin>>family){
  if(family==0){unsigned count;std::cin>>count;assert(count>=2&&count<=64);std::array<Vector,4>controls;for(auto&v:controls)read(v);std::vector<Vector>samples(count);
   reinterpret_cast<unsigned(__attribute__((stdcall)) *)(Vector*,unsigned,Vector*)>(0x489660)(controls.data(),count,samples.data());vectors(samples.data(),samples.size());std::cout<<'\n';}
  else{assert(family==1);alignas(4)unsigned char actor[0xC28]={};for(auto&v:hooks)read(v);for(unsigned o=0;o<4;o++)read(at<Vector>(actor,0x48C+12*o));
   for(unsigned o=0;o<23;o++)read(at<Vector>(actor,0x4BC+12*o));for(unsigned o=0;o<128;o++)read(at<Vector>(actor,0x5D0+12*o));events.clear();
   reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x488370)(actor);
   std::cout<<"{\"controls\":";vectors((Vector*)(actor+0x48C),4);std::cout<<",\"nodes\":";vectors((Vector*)(actor+0x4BC),23);
   std::cout<<",\"history\":";vectors((Vector*)(actor+0x5D0),128);std::cout<<",\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";}
 }
}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x489660)
    curves = []
    for count in [2, 3, 4, 5, 12, 23, 32, 64]:
        for controls in [
            [[0, 0, 0]] * 4,
            [[-1735, -31, 443], [-1720, -100, 520], [-1600, -150, 570], [-1500, -200, 600]],
            [[-524288, 524287, -1], [524287, -524288, 1], [-32768, 32767, 4096], [32767, -32768, -4096]],
            [[-2147483648, 2147483647, -1], [2147483647, -2147483648, 1], [-65536, 65535, 4095], [65535, -65536, -4095]]
        ]:
            curves.append(dict(count=count, controls=controls))
    for _ in range(160):
        curves.append(dict(count=rng.choice([2, 3, 12, 23, 64]), controls=[
            [rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(4)]))
    updates = []
    for case in range(80):
        hooks = [[rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(7)]
        if case == 0:
            hooks = [[(-1735 + 15 * part) * 4096 + part, (-31 - 13 * part) * 4096 - part,
                      (443 + 20 * part) * 4096 + part * 17] for part in range(7)]
        updates.append(dict(hooks=hooks,
            controls=[[rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(4)],
            nodes=[[rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(23)],
            history=[[rng.randint(-2147483648, 2147483647) for _ in range(3)] for _ in range(128)]))
    requests = [[0, row['count'], *sum(row['controls'], [])] for row in curves]
    requests += [[1, *sum(row['hooks'], []), *sum(row['controls'], []), *sum(row['nodes'], []), *sum(row['history'], [])] for row in updates]
    with tempfile.TemporaryDirectory(prefix='openspidey-scorpion-tail-') as folder:
        source, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        source.write_text(HARNESS)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(source), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, row)) for row in requests) + '\n').encode()).decode().splitlines()
    if len(lines) != len(requests):
        raise RuntimeError('Original tail reference returned an incomplete fixture set.')
    for row, line in zip(curves, lines[:len(curves)], strict=True):
        row['expected'] = json.loads(line)
    for row, line in zip(updates, lines[len(curves):], strict=True):
        row['expected'] = json.loads(line)
    (root / 'modern/OpenSpidey.Tests/Fixtures/scorpion-tail-path.json').write_text(
        json.dumps(dict(curves=curves, updates=updates), separators=(',', ':')) + '\n')
    print(f'Captured {len(curves)} whole original tail curves and {len(updates)} pose-hook/history updates, including all seven callback-visible states and node23/history0 alias.')


if __name__ == '__main__':
    main()
