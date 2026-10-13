"""Capture actual PC CScriptOnlyBaddy compass/owned SFX dispatch and signed AI reposition timing."""
import argparse
import json
from pathlib import Path
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
std::vector<long long> events; unsigned soundReturn;
void stop(unsigned handle){events.insert(events.end(),{0,handle});}
unsigned global(unsigned sound,int volume,int pitch){events.insert(events.end(),{1,sound,volume,pitch});return soundReturn;}
unsigned positional(unsigned sound,int* position,int pitch){events.insert(events.end(),{2,sound,position[0],position[1],position[2],pitch});return soundReturn;}
void update(unsigned handle,int* position,int delta){events.insert(events.end(),{3,handle,position[0],position[1],position[2],delta});}
void jump(unsigned address,void* target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<int*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void omit(unsigned address,int count){std::memset(reinterpret_cast<void*>(address),0x90,count);}
unsigned& word(unsigned address){return *reinterpret_cast<unsigned*>(address);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Omit only Windows exception registration and diagnostic printing; execute untouched source branches.
 for(unsigned start:{0x407A50u,0x4050B0u,0x407747u}){omit(start,6);*reinterpret_cast<unsigned short*>(start)=0xC031;}
 for(unsigned start:{0x407A5Eu,0x407E21u,0x407E77u,0x4050BEu,0x406ACBu,0x40774Eu,0x407829u})omit(start,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;
 jump(0x471C50,(void*)&global);jump(0x471EA0,(void*)&positional);jump(0x4723D0,(void*)&stop);jump(0x472000,(void*)&update);
 // The source CBody destructor is outside the compass/SFX leaf being captured; CBaddy cleanup remains original.
 *reinterpret_cast<unsigned char*>(0x460780)=0xC3;
 int mode,opcode,operand,period,oldPeriod,skip,fast,x,y,z;unsigned handle,flags,clock;
 while(std::cin>>mode>>opcode>>operand>>period>>handle>>oldPeriod>>flags>>soundReturn>>skip>>fast>>clock>>x>>y>>z){
  alignas(4) unsigned char body[0x400]={};unsigned short operands[2]={static_cast<unsigned short>(operand),static_cast<unsigned short>(period)};
  *reinterpret_cast<unsigned short*>(body+0x38)=203;*reinterpret_cast<unsigned short**>(body+0x24C)=operands;
  *reinterpret_cast<unsigned*>(body+0x328)=handle;*reinterpret_cast<short*>(body+0x32C)=oldPeriod;
  *reinterpret_cast<unsigned*>(body+0x218)=flags;*reinterpret_cast<int*>(body+0x234)=skip;
  *reinterpret_cast<int*>(body+8)=x;*reinterpret_cast<int*>(body+12)=y;*reinterpret_cast<int*>(body+16)=z;
  *reinterpret_cast<unsigned char*>(0x60CFC4)=fast;*reinterpret_cast<unsigned char*>(0x60F77C)=mode==2;
  *reinterpret_cast<int*>(0x60F708)=91;*reinterpret_cast<int*>(0x60F70C)=-3;*reinterpret_cast<int*>(0x60F710)=5;word(0x60F768)=17;
  events.clear();int result=1;
  if(mode==0)result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x407A50)(body,opcode);
  else if(mode==1){word(0x60CFA8)=clock;*reinterpret_cast<int*>(body+0x20C)=1;*reinterpret_cast<int*>(body+0x230)=1000;body[0x80]=2;
    reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x407840)(body);
  }else{word(0x56E994)=reinterpret_cast<unsigned>(body);word(0x56E98C)=1;
    reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x407740)(body);
  }
  std::cout<<"{\"result\":"<<result<<",\"handle\":"<<*reinterpret_cast<unsigned*>(body+0x328)<<",\"period\":"<<*reinterpret_cast<short*>(body+0x32C)
   <<",\"flags\":"<<*reinterpret_cast<unsigned*>(body+0x218)<<",\"consumed\":"<<reinterpret_cast<unsigned char*>(*reinterpret_cast<unsigned short**>(body+0x24C))-reinterpret_cast<unsigned char*>(operands)
   <<",\"compass\":["<<static_cast<int>(*reinterpret_cast<unsigned char*>(0x60F77C))<<','<<*reinterpret_cast<int*>(0x60F708)<<','<<*reinterpret_cast<int*>(0x60F70C)<<','<<*reinterpret_cast<int*>(0x60F710)<<','<<word(0x60F768)<<"],\"events\":[";
  for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}std::cout<<"]}\n";
 }
}
'''
    cases = []
    values = [0, 1, 49, 56, 299, 300, 309, 310, 0x212B, 32767, 32768, 65535]
    for opcode in [0x428D, 0x450B, 0x4507, 0x4508, 0x4509, 0x450C, 0x428E, 0x4290, 0x428F, 0x4291]:
        for operand in values:
            for handle in [0, 0x102]:
                for skip, fast in [(0, 0), (1, 1)]:
                    cases.append(dict(mode=0, opcode=opcode, operand=operand, period=65533 if operand & 1 else 3,
                                      handle=handle, oldPeriod=-7, flags=0x40000001, soundReturn=0x20,
                                      skip=skip, fast=fast, clock=0, position=[-4097, 4095, 8193]))
    for period in [-32768, -7, -3, -1, 1, 2, 3, 7, 32767]:
        for clock in [0, 1, 2, 3, 7, 21, 32768, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFE, 0xFFFFFFFF]:
            for handle in [0, 0x102]:
                cases.append(dict(mode=1, opcode=0, operand=0, period=0, handle=handle, oldPeriod=period,
                                  flags=0, soundReturn=0, skip=0, fast=0, clock=clock, position=[-4097, 4095, 8193]))
    for handle in [0, 0x102]:
        for flags in [0, 1, 0x80, 0x1000080]:
            cases.append(dict(mode=2, opcode=0, operand=0, period=0, handle=handle, oldPeriod=3,
                              flags=flags, soundReturn=0, skip=0, fast=0, clock=0, position=[-4097, 4095, 8193]))
    rows = [' '.join(map(str, [case['mode'], case['opcode'], case['operand'], case['period'], case['handle'],
                              case['oldPeriod'], case['flags'], case['soundReturn'], case['skip'], case['fast'],
                              case['clock'], *case['position']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-original-control-effects-') as directory:
        cpp, binary = Path(directory) / 'reference.cpp', Path(directory) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        completed = subprocess.run([str(binary), str(args.original_exe.resolve())], input='\n'.join(rows) + '\n',
                                   text=True, capture_output=True)
        if completed.returncode:
            raise RuntimeError(f'Original control effect fixture failed {completed.returncode}: {completed.stderr}')
    outputs = completed.stdout.splitlines()
    if len(outputs) != len(cases):
        raise RuntimeError(f'Expected {len(cases)} PC rows, got {len(outputs)}')
    result = dict(originalPcDispatcher='407A50', originalPcAi='407840', originalPcDestructor='407740',
                  cases=[dict(**case, expected=json.loads(row)) for case, row in zip(cases, outputs, strict=True)])
    (root / 'modern/OpenSpidey.Tests/Fixtures/control-actor-effects.json').write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} original control compass/sound command, signed reposition and destructor cases.')


if __name__ == '__main__':
    main()
