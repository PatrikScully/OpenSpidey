"""Capture genuine PC184/166/169 stores, full4BAD90 lighting and original face/raster depth gates."""
import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <cstring>
template<class T>T&at(void*p,unsigned n){return *reinterpret_cast<T*>(static_cast<unsigned char*>(p)+n);}
void patch(unsigned a,void*f){auto*p=reinterpret_cast<unsigned char*>(a);p[0]=0xE9;*reinterpret_cast<unsigned*>(p+1)=reinterpret_cast<unsigned>(f)-a-5;}
void log(const char*,...){}
extern "C" __attribute__((naked)) void commandEnd(){asm volatile("mov %ebx,%eax;pop %ebp;pop %edi;pop %esi;pop %ebx;ret");}
extern "C" __attribute__((naked)) unsigned command(unsigned,void*){asm volatile("push %ebx;push %esi;push %edi;push %ebp;mov 0x18(%esp),%ebx;mov 0x14(%esp),%eax;cmp $166,%eax;je 1f;cmp $169,%eax;je 2f;mov $0x4E1279,%eax;jmp *%eax;1:mov $0x4E1F9F,%eax;jmp *%eax;2:mov $0x4E1FC2,%eax;jmp *%eax");}
extern "C" __attribute__((naked)) void faceEnd(){asm volatile("movzwl (%ecx),%eax;add $0x100,%esp;pop %esi;pop %ebx;ret");}
extern "C" __attribute__((naked)) unsigned face(unsigned){asm volatile("push %ebx;push %esi;sub $0x100,%esp;mov 0x10C(%esp),%ebx;lea 0x80(%esp),%ecx;xor %esi,%esi;mov 0x6B78F8,%eax;mov $0x477A1D,%edx;jmp *%edx");}
extern "C" __attribute__((naked)) void biasEnd(){asm volatile("flds 0x10(%esp);add $0x20,%esp;ret");}
extern "C" __attribute__((naked)) float bias(){asm volatile("sub $0x20,%esp;mov $0x506B47,%eax;jmp *%eax");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);patch(0x4015B0,(void*)&log);patch(0x4E2B3F,(void*)&commandEnd);patch(0x477A67,(void*)&faceEnd);patch(0x506B79,(void*)&biasEnd);
 int mode;while(std::cin>>mode){alignas(4)unsigned char player[0xF20]={};
 if(mode==0){unsigned op,a,b,c;int first,second,third;std::cin>>op>>a>>b>>c>>first>>second>>third;unsigned short words[3]={static_cast<unsigned short>(a),static_cast<unsigned short>(b),static_cast<unsigned short>(c)};
 *reinterpret_cast<short*>(0x660F78)=first;*reinterpret_cast<short*>(0x660F7A)=second;*reinterpret_cast<short*>(0x660F7C)=third;at<unsigned>(player,0x574)=160;*reinterpret_cast<void**>(0x6A9038)=player;
 unsigned end=command(op,words);std::cout<<'['<<*reinterpret_cast<short*>(0x660F78)<<','<<*reinterpret_cast<short*>(0x660F7A)<<','<<*reinterpret_cast<short*>(0x660F7C)<<','<<at<unsigned>(player,0x574)<<','<<end-reinterpret_cast<unsigned>(words)<<"]\n";
 }else if(mode==1){unsigned initial,last,target;int ticks,seed;std::cin>>initial>>last>>target>>ticks>>seed;at<unsigned>(player,0x570)=initial;at<int>(player,0x80)=ticks;*reinterpret_cast<unsigned*>(0x556498)=last;
 for(int i=0;i<9;i++)*reinterpret_cast<short*>(0x5559F4+i*2)=101+seed+i*7;for(int i=0;i<3;i++)*reinterpret_cast<int*>(0x555A08+i*4)=9001+seed+i*13;
 std::cout<<'[';for(int visit=0;visit<3;visit++){reinterpret_cast<void(__attribute__((thiscall))*)(void*,unsigned short)>(0x4BAD90)(player,target);if(visit)std::cout<<',';std::cout<<'['<<at<unsigned>(player,0x570)<<','<<*reinterpret_cast<unsigned*>(0x556498);for(int i=0;i<9;i++)std::cout<<','<<*reinterpret_cast<short*>(0x5559F4+i*2);for(int i=0;i<3;i++)std::cout<<','<<*reinterpret_cast<int*>(0x555A08+i*4);std::cout<<']';}std::cout<<"]\n";
 }else{unsigned flags;int low,first,second,third,normalModel,lowModel;float scale;std::cin>>flags>>low>>first>>second>>third>>normalModel>>lowModel>>scale;
 *reinterpret_cast<int*>(0x6B78F8)=low;*reinterpret_cast<short*>(0x614F88)=first;*reinterpret_cast<short*>(0x614F98)=second;*reinterpret_cast<short*>(0x615018)=third;unsigned faceBias=face(flags);int key=static_cast<int>(static_cast<unsigned>(low?lowModel:normalModel)+faceBias);*reinterpret_cast<int*>(0xAC08E0)=key;*reinterpret_cast<float*>(0x568194)=scale;float result=bias();unsigned bits;std::memcpy(&bits,&result,4);std::cout<<'['<<faceBias<<','<<key<<','<<bits<<"]\n";}
 }}
'''
    rng = random.Random(0x4BAD90)
    commands = [dict(opcode=op, arguments=[value & 65535, green, blue], initial=[-17, 31, 123])
                for op in [166, 169, 184] for value in [-32768, -1, 0, 1, 140, 300, 32767, 32768, 65535]
                for green in [0, 1, 65535] for blue in [0, 140, 32768]]
    brightness = [dict(current=current, last=last, target=target, ticks=ticks, seed=rng.randrange(0, 5000))
                  for current in [0, 1, 16, 140, 160, 208, 256, 65535, 2147483647, 4294967280, 4294967295]
                  for target in [0, 1, 140, 160, 208, 256, 65535]
                  for ticks in [-2147483648, -6, -1, 0, 1, 2, 6, 31]
                  for last in [current, target, 4294967295]]
    depth = [dict(flags=flags, low=low, scratch=scratch, normal=normal, lowModel=low_model, scale=1.9624)
             for flags in [0, 1, 8192, 12287, 16384, 24576, 32767, 4294967295] for low in [0, 1]
             for scratch in [[300, -30, 0], [-32768, 32767, -1], [0, 0, 0]]
             for normal in [-2147483648, -1, 0, 7, 2147483647] for low_model in [-1, 0, 20]]
    lines = [' '.join(map(str, [0, c['opcode'], *c['arguments'], *c['initial']])) for c in commands]
    lines += [' '.join(map(str, [1, c['current'], c['last'], c['target'], c['ticks'], c['seed']])) for c in brightness]
    lines += [' '.join(map(str, [2, c['flags'], c['low'], *c['scratch'], c['normal'], c['lowModel'], c['scale']])) for c in depth]
    with tempfile.TemporaryDirectory(prefix='openspidey-brightness-depth-') as folder:
        cpp = Path(folder)/'reference.cpp'; binary = Path(folder)/'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-O0', '-fwrapv', '-fno-pie', '-no-pie',
                        '-I', str(root/'src'), '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        results = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                          input=('\n'.join(lines)+'\n').encode()).decode().splitlines()
    for case, result in zip(commands+brightness+depth, results, strict=True): case['expected'] = json.loads(result)
    output = dict(commands=commands, brightness=brightness, depth=depth)
    (root/'modern/OpenSpidey.Tests/Fixtures/player-brightness-render-depth.json').write_text(json.dumps(output, separators=(',', ':'))+'\n')
    print(f'Captured {len(commands)} original166/169/184 stores, {len(brightness)} full4BAD90 sequences/{len(brightness)*3} visits and {len(depth)} untouched face/raster bias leaves.')


if __name__ == '__main__': main()
