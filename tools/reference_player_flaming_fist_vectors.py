"""Capture original4B826A..4B8346 flaming-fist gates and left/right simby5/RNG order without changing any selector instructions."""
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
std::vector<long long>events;unsigned seed;bool consumeEmberRandom;
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
int rnd(int maximum){assert(maximum>0);seed=seed*1664525u+1013904223u;int result=seed%maximum;events.insert(events.end(),{1,maximum,result});return result;}
void reloc(const char*module,unsigned function,const unsigned*args,unsigned*result){assert(function==5&&result==nullptr&&strcmp(module,"simby")==0);int side=reinterpret_cast<unsigned>(module)==0x5559C4?0:1;assert(side==0||reinterpret_cast<unsigned>(module)==0x5559CC);auto&position=*reinterpret_cast<const CVector*>(args[0]);events.insert(events.end(),{2,side,position.vx,position.vy,position.vz,args[1]});if(consumeEmberRandom){for(int maximum:{10,4096,4096,200,5,5})rnd(maximum);}}
// Source locals are ESP+7C(left) and+94(right); our CALL adds its four-byte return slot.
void __attribute__((naked))run(void*,const CVector*,const CVector*){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x500,%esp;movl 0x514(%esp),%esi;movl 0x518(%esp),%eax;movl (%eax),%edx;movl %edx,0x78(%esp);movl 4(%eax),%edx;movl %edx,0x7c(%esp);movl 8(%eax),%edx;movl %edx,0x80(%esp);movl 0x51c(%esp),%eax;movl (%eax),%edx;movl %edx,0x90(%esp);movl 4(%eax),%edx;movl %edx,0x94(%esp);movl 8(%eax),%edx;movl %edx,0x98(%esp);movl $0x4B826A,%eax;call *%eax;addl $0x500,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*reinterpret_cast<unsigned char*>(0x4B8346)=0xC3;patch(0x4E5DA0,(void*)&rnd);patch(0x47D470,(void*)&reloc);long long v[11];while(std::cin>>v[0]){for(int i=1;i<11;i++)std::cin>>v[i];alignas(4)unsigned char player[0xF20]={};at<unsigned char>(player,0x5E8)=v[0];at<int>(player,0x5AC)=v[1];at<unsigned short>(player,0x12A)=v[2];seed=v[3];consumeEmberRandom=v[4];CVector left(v[5],v[6],v[7]),right(v[8],v[9],v[10]);events.clear();run(player,&left,&right);std::cout<<'['<<unsigned(at<unsigned char>(player,0x5E8))<<','<<at<int>(player,0x5AC)<<','<<at<unsigned short>(player,0x12A)<<','<<seed<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = []
    for fire in [-1, 0, 1, 255]:
        for bonus in [-1, 0, 1]:
            for clip in [0, 100, 102, 104, 106, 213, 65535, 65536]:
                for seed in [0, 1, 12345, 0xffffffff]:
                    for consume in [0, 1]:
                        cases.append([fire, bonus, clip, seed, consume, 12345, -234567, 3456789, -9876543, 765432, -54321])
    rng = random.Random(0x4B826A)
    for _ in range(400):
        cases.append([rng.randrange(-256, 512), rng.randint(-2147483648, 2147483647),
                      rng.choice([100, 102, 104, 106, 213, rng.randrange(65536)]), rng.randrange(1 << 32), rng.randrange(2),
                      *[rng.randint(-2147483648, 2147483647) for _ in range(6)]])
    with tempfile.TemporaryDirectory(prefix='openspidey-flaming-fists-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = ('\n'.join(' '.join(map(str, case)) for case in cases) + '\n').encode()
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=rows).decode().splitlines()
    result = [dict(input=case, expected=json.loads(line)) for case, line in zip(cases, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/player-flaming-fists.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(result)} original flaming-fist cases: exact byte/bonus/clip gates, left-before-right simby5 emissions, borrowed posed endpoints, scale256 and interleaved real Ember RNG boundaries.')


if __name__ == '__main__':
    main()
