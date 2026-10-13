"""Capture whole original46ACD0 with its genuine initialization/trig loop and unpatched source control flow; compile the actual pre-player sky reset separately."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
from reference_ground_response_vectors import controller_method


def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
std::vector<unsigned>events;
unsigned bits(float v){unsigned r;std::memcpy(&r,&v,4);return r;}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void texture(int first,int blend){events.insert(events.end(),{1,unsigned(first),unsigned(blend)});}
void quad(float x,float y,float width,float height,float u,float v,float us,float vs,unsigned colour,float depth,bool flag){events.insert(events.end(),{2,bits(x),bits(y),bits(width),bits(height),bits(u),bits(v),bits(us),bits(vs),colour,bits(depth),flag});}
// Actual Port_InitAtStart46CBE1 clears EBX before46CCE6 seeds the angle counter from it.
void __attribute__((naked))initTrig(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x40,%esp;xorl %ebx,%ebx;movl $0x46CCE6,%eax;call *%eax;addl $0x40,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
int water,currentSky,targetSky;int&gWaterEffect=water;unsigned&G_DB_SKY_COLOR=*reinterpret_cast<unsigned*>(&currentSky);unsigned&G_DB_SKY_COLOR_TARGET=*reinterpret_cast<unsigned*>(&targetSky);
struct CPlayer{};struct PlayerFramePreparation{CPlayer&player_;void updateWaterEffect();};
void Db_UpdateSky(){events.insert(events.end(),{3,unsigned(currentSky)});}
'''
    code += controller_method((root / 'src/game/player/player_status_updates.cpp').read_text(), 'void PlayerFramePreparation::updateWaterEffect(') + '\n'
    code += r'''
int main(int argc,char**argv){assert(argc==3);original_executable::loadOriginalImage(argv[1]);*reinterpret_cast<unsigned char*>(0x46CD51)=0xC3;initTrig();assert(*reinterpret_cast<short*>(0x610C48)==0&&*reinterpret_cast<short*>(0x610C4A)==4096);patch(0x506440,(void*)&texture);patch(0x507470,(void*)&quad);int family=atoi(argv[2]);long long v[9];while(std::cin>>v[0]){for(int i=1;i<9;i++)std::cin>>v[i];events.clear();if(family){water=v[0];currentSky=v[1];targetSky=v[2];CPlayer p;PlayerFramePreparation{p}.updateWaterEffect();std::cout<<'['<<water<<','<<unsigned(currentSky)<<','<<unsigned(targetSky);}else{*reinterpret_cast<int*>(0x60FA9C)=v[0];*reinterpret_cast<int*>(0x5FAE98)=v[1];*reinterpret_cast<unsigned*>(0x6B4CA8)=v[2];*reinterpret_cast<int*>(0x60FA24)=v[3];*reinterpret_cast<unsigned short*>(0x60FA28)=v[4];*reinterpret_cast<int*>(0x568154)=v[5];*reinterpret_cast<int*>(0x568158)=v[6];*reinterpret_cast<unsigned*>(0x61B5FC)=v[7];*reinterpret_cast<unsigned*>(0x628614)=v[8];reinterpret_cast<void(*)()>(0x46ACD0)();std::cout<<'['<<*reinterpret_cast<int*>(0x60FA9C)<<','<<*reinterpret_cast<int*>(0x5FAE98)<<','<<*reinterpret_cast<int*>(0x60FA24)<<','<<*reinterpret_cast<unsigned short*>(0x60FA28);}std::cout<<",[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]]\n";}}
'''
    cases = []
    for water in [-1, 0, 1, 2]:
        for paused in [-1, 0, 1]:
            for clock in [0, 1, 42, 170, 123456, 0x7fffffff, 0x80000000, 0xffffffff]:
                for size in [(640, 480, 640, 480), (1280, 720, 1280, 720), (640, 480, 1280, 720)]:
                    cases.append([water, paused, clock, -12345, 65535, *size])
    rng = random.Random(0x46ACD0)
    for _ in range(300):
        cases.append([rng.choice([0, 1]), rng.choice([0, 1]), rng.randrange(1 << 32), rng.randint(-2147483648, 2147483647),
                      rng.randrange(65536), rng.randrange(1, 8193), rng.randrange(1, 8193), rng.randrange(1, 8193), rng.randrange(1, 8193)])
    preparation = [[water, current, target, 0, 0, 0, 0, 0, 0] for water in [-2147483648, -1, 0, 1, 2, 2147483647]
                   for current in [0, 0x466973, 0x123456, 0xffffffff] for target in [0, 0x466973, 0x154070, 0xffffffff]]
    with tempfile.TemporaryDirectory(prefix='openspidey-water-post-') as folder:
        cpp = Path(folder) / 'reference.cpp'; binary = Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = []
        for family, name, requests in [(0, 'post', cases), (1, 'prepare', preparation)]:
            rows = ('\n'.join(' '.join(map(str, case)) for case in requests) + '\n').encode()
            lines = subprocess.check_output([str(binary), str(args.original_exe.resolve()), str(family)], input=rows).decode().splitlines()
            result += [dict(kind=name, input=case, expected=json.loads(line)) for case, line in zip(requests, lines, strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/water-post.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} unpatched original water post frames and {len(preparation)} actual source pre-player sky resets: genuine initialized sine/cosine, wrapped timer, pause/wordcountdown, float-sized quad and direct ARGB/slot6.')


if __name__ == '__main__':
    main()
