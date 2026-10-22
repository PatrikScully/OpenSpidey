"""Capture CSimbyBase::AI (0x4ACF00) against SpideyPC.exe over several frames, plus the CBaddy constructor
(0x402C00) defaults the inlined CSimbyBase constructor starts from.

Mem_RecoverPointer (0x4583F0) answers from a scripted per-frame owner liveness and the CItem transform rebuild (0x475F50)
is a recorded boundary. Between frames the case may rewrite field_328/field_324 the way the owning CSimby does.
"""
import argparse
import json
import random
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=(unsigned char*)a;p[0]=0xE9;*reinterpret_cast<int*>(p+1)=(unsigned)f-a-5;}
alignas(4)unsigned char base[0x334];int owner;std::vector<int>recover;unsigned recoverI;int recoverCalls,transformCalls;
void*recoverPointer(int*handle){assert(handle==&at<int>(base,0x32C));recoverCalls++;return recover.at(recoverI)?&owner:nullptr;}
void transform(void*item){assert(item==base);transformCalls++;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 {alignas(4)unsigned char baddy[0x334]={};reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x402C00)(baddy);
  at<unsigned short>(baddy,0x46)&=~0x10;
  std::cout<<at<short>(baddy,0x28)<<' '<<at<short>(baddy,0x2A)<<' '<<at<short>(baddy,0x2C)<<' '<<at<unsigned short>(baddy,4)<<' '<<at<unsigned short>(baddy,0x46)<<"\n";}
 patch(0x4583F0,(void*)&recoverPointer);patch(0x475F50,(void*)&transform);
 int frames;while(std::cin>>frames){
  std::memset(base,0,sizeof base);int s[7];for(int&v:s)std::cin>>v;
  at<short>(base,0x28)=s[0];at<short>(base,0x2A)=s[1];at<short>(base,0x2C)=s[2];at<unsigned short>(base,4)=s[3];at<unsigned short>(base,0x46)=s[4];
  at<int>(base,0x324)=s[5];at<int>(base,0x328)=s[6];at<int>(base,0x32C)=0x1234;
  recover.assign(frames,0);recoverI=0;std::vector<int>sets(2*frames);for(int i=0;i<frames;i++)std::cin>>recover[i]>>sets[2*i]>>sets[2*i+1];
  for(int i=0;i<frames;i++){
   if(sets[2*i]!=-1)at<int>(base,0x324)=sets[2*i];if(sets[2*i+1]!=-99999)at<int>(base,0x328)=sets[2*i+1];
   recoverI=i;recoverCalls=transformCalls=0;reinterpret_cast<void(__attribute__((thiscall))*)(void*)>(0x4ACF00)(base);
   std::cout<<(i?" ":"")<<at<short>(base,0x28)<<' '<<at<short>(base,0x2A)<<' '<<at<short>(base,0x2C)<<' '<<at<unsigned short>(base,4)<<' '
    <<at<unsigned short>(base,0x46)<<' '<<at<int>(base,0x324)<<' '<<at<int>(base,0x328)<<' '<<recoverCalls<<' '<<transformCalls;}
  std::cout<<"\n";}}
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    rng = random.Random(0x4ACF00)
    targets = [0, 100, 300, 1024, 4096, 6000, 39, 40, 41, -50, 32767, -32768, 70000]
    cases = []
    for _ in range(1200):
        scale = rng.choice([4096, 100, 0, 99, 101, 199, 200, 300, 6000, -32768, 32767, rng.randint(-32768, 32767)])
        uniform = rng.random() < 0.8
        state = [scale, scale if uniform else rng.randint(-32768, 32767), scale if uniform else rng.randint(-32768, 32767),
                 rng.choice([0x2, 0x202, rng.randrange(65536)]), rng.choice([0x206, 0x246, rng.randrange(65536)]),
                 rng.choice([0, 0, 1, 1, rng.randint(-5, 5)]), rng.choice(targets + [rng.randint(-40000, 40000)])]
        frames = rng.randint(1, 24)
        script = []
        for _ in range(frames):
            alive = 0 if rng.random() < 0.08 else 1
            follow = rng.choice([1, 0]) if rng.random() < 0.08 else -1
            target = rng.choice(targets) if rng.random() < 0.15 else -99999
            script.append([alive, follow, target])
        cases.append(dict(state=state, frames=script))
    lines = [' '.join(map(str, [len(c['frames']), *c['state'], *[v for f in c['frames'] for v in f]])) for c in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-original-simby-base-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], text=True, capture_output=True,
                                   input='\n'.join(lines) + '\n')
        if completed.returncode:
            raise RuntimeError(f'Original CSimbyBase fixture failed{completed.returncode}: {completed.stderr}')
        output = completed.stdout.splitlines()
    if len(output) != len(cases) + 1: raise RuntimeError(f'Expected {len(cases) + 1} lines, got {len(output)}')
    for case, line in zip(cases, output[1:], strict=True):
        values = list(map(int, line.split()))
        case['expected'] = [values[i:i + 9] for i in range(0, len(values), 9)]
    fixture = dict(constructor=list(map(int, output[0].split())), cases=cases)
    (root / 'modern/OpenSpidey.Tests/Fixtures/simby-base.json').write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    deaths = sum(any(frame[4] & 0x40 for frame in case['expected']) for case in cases)
    print(f'Captured {len(cases)} original CSimbyBase::AI cases ({sum(len(c["frames"]) for c in cases)} frames, '
          f'{deaths} reaching flag 0x40); constructor defaults {fixture["constructor"]}.')


if __name__ == '__main__': main()
