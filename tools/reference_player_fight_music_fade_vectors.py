"""Capture original215/216 command bodies and4C66E6 pre-input gain; compile the genuine constructor level switch."""
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
    source = (root / 'src/game/player/spidey.cpp').read_text()
    start = source.index('\tswitch (Trig_GetLevelId())', source.index('CPlayer::CPlayer('))
    construction = source[start:source.index('\tthis->field_360 = 2;', start)]
    code = r'''
#include "original_executable.h"
#include <iostream>
int level;int Trig_GetLevelId(){return level;}
struct ConstructorGate{unsigned char field_C5C;void run(){
'''+construction+r'''
}};
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void log(const char*){}
extern "C" __attribute__((naked)) void leafEnd(){asm volatile("pop %edi;pop %esi;pop %ebx;ret");}
extern "C" __attribute__((naked)) void fadeStep(void*){asm volatile("push %ebx;push %esi;push %edi;mov 16(%esp),%esi;xor %ebx,%ebx;mov $0x4C66E6,%eax;jmp *%eax");}
extern "C" __attribute__((naked)) void commandStep(unsigned){asm volatile("push %ebx;push %esi;push %edi;mov 16(%esp),%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4C674B,(void*)&leafEnd);patch(0x4E2B3F,(void*)&leafEnd);patch(0x4015B0,(void*)&log);
 int family;long long v[6];while(std::cin>>family){for(auto&value:v)std::cin>>value;alignas(4)unsigned char player[0xD00]={};
 at<unsigned char>(player,0xC5C)=v[1];at<int>(player,0xC64)=v[2];at<unsigned char>(player,0xC68)=v[3];at<unsigned char>(player,0xC69)=v[4];at<int>(player,0x80)=v[5];
 if(family==0)fadeStep(player);else if(family==1){*(void**)0x6A9038=player;commandStep(v[0]==215?0x4E1329:0x4E134E);}else{level=v[0];ConstructorGate gate{static_cast<unsigned char>(v[1])};gate.run();at<unsigned char>(player,0xC5C)=gate.field_C5C;}
 std::cout<<'['<<unsigned(at<unsigned char>(player,0xC5C))<<','<<at<int>(player,0xC64)<<','<<unsigned(at<unsigned char>(player,0xC68))<<','<<unsigned(at<unsigned char>(player,0xC69))<<"]\n";
 }}
'''
    frames = [[0, enabled, gain, down, up, ticks] for enabled in [0, 1, 255] for down in [0, 1, 255] for up in [0, 1, 255]
              for gain in [-2147483648, -1, 0, 1, 63, 64, 4095, 4096, 4097, 2147483647]
              for ticks in [-2147483648, -1, 0, 1, 2, 6, 128, 2147483647]]
    rng = random.Random(0x4C66E6)
    for _ in range(320):
        frames.append([0, rng.randrange(256), rng.randrange(-2147483648, 2147483648),
                       rng.randrange(256), rng.randrange(256), rng.randrange(-2147483648, 2147483648)])
    commands = [[opcode, 255, gain, down, up, 2] for opcode in [215, 216] for gain in [-2147483648, -1, 0, 4096, 2147483647]
                for down in [0, 1, 255] for up in [0, 1, 255]]
    construction_cases = [[level, initial, -123, 255, 255, 2] for level in [0x101, 0x102, 0x103, 0x104, 0x201, 0x202, 0x203,
        0x301, 0x401, 0x501, 0x502, 0x503, 0x504, 0x505, 0x506, 0x507, 0x601, 0x602, 0x603, 0x604,
        0x701, 0x702, 0x703, 0x704, 0x705, 0x801, 0x802, 0x803, 0x804, 0x805, 0x806, 0x901, -1] for initial in [0, 1, 255]]
    families = [(0, frames), (1, commands), (2, construction_cases)]
    rows = [(family, row) for family, values in families for row in values]
    with tempfile.TemporaryDirectory(prefix='openspidey-player-fight-music-fade-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-O0', '-fwrapv', '-m32', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        lines = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, [family, *row])) for family, row in rows) + '\n').encode()).decode().splitlines()
    fixture = [dict(family=family, input=row, expected=json.loads(line)) for (family, row), line in zip(rows, lines, strict=True)]
    path = root / 'modern/OpenSpidey.Tests/Fixtures/player-fight-music-fade.json'
    path.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print(f'Captured {len(frames)} original gain steps +{len(commands)} original command215/216 writes +{len(construction_cases)} compiled source constructor gates; audible Music_MusicUpdate is outside this scalar package.')


if __name__ == '__main__':
    main()
