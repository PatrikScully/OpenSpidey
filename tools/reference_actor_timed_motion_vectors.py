"""Capture untouched PC4280 with actual CBaddy/CPlatform virtual physics, raw/variable duration and source recording state."""
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
template<class T>T&at(void*body,unsigned offset){return *reinterpret_cast<T*>((unsigned char*)body+offset);}
int level, resolved, calls;
int getLevel(){return level;}
short __attribute__((thiscall)) getVariable(void*body,unsigned short opcode){return resolved;}
void __attribute__((thiscall)) physics(void*body){calls++;assert(at<int>(body,0x80)==2);
 if(at<unsigned short>(body,0x38)==402)reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x469510)(body);
 else reinterpret_cast<void(__attribute__((thiscall)) *)(void*,int)>(0x404C50)(body,0);
}
void jump(unsigned address,void*target){auto*p=reinterpret_cast<unsigned char*>(address);p[0]=0xE9;*reinterpret_cast<int*>(p+1)=reinterpret_cast<unsigned>(target)-address-5;}
void __attribute__((naked)) initHeading(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x28,%esp;xorl %ebx,%ebx;movl $0x46cce6,%eax;call *%eax;addl $0x28,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset((void*)0x4050B0,0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 for(unsigned address:{0x4050BEu,0x406ACBu})std::memset((void*)address,0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;*reinterpret_cast<unsigned char*>(0x46CD51)=0xC3;initHeading();
 jump(0x4DE770,(void*)&getLevel);
 int type,skip,fast,operand,elapsed,local,old,mode;
 while(std::cin>>type>>level>>skip>>fast>>operand>>resolved>>elapsed>>local>>old>>mode){
  alignas(4)unsigned char body[0x400]={};void*vtable[17]={};vtable[11]=(void*)&physics;vtable[16]=(void*)&getVariable;at<void*>(body,0)=vtable;
  unsigned short words[1]={static_cast<unsigned short>(operand)};at<unsigned short*>(body,0x24C)=words;
  at<unsigned short>(body,0x38)=type;at<int>(body,0x234)=skip;at<int>(body,0x80)=elapsed;
  at<int>(body,0x230)=-5;at<int>(body,0x2A8)=local;at<short>(body,0x274)=mode;
  *reinterpret_cast<unsigned char*>(0x60CFC4)=fast;
  for(unsigned offset:{8,0x60,0x6C,0x27C})for(unsigned axis=0;axis<3;axis++)std::cin>>at<int>(body,offset+axis*4);
  for(unsigned offset:{0x14,0x88,0x8E})for(unsigned axis=0;axis<3;axis++){int value;std::cin>>value;at<short>(body,offset+axis*2)=value;}
  unsigned char damp[6]={1,4,31,1,5,31};for(unsigned i=0;i<3;i++){at<unsigned char>(body,0x78+i)=damp[i];at<unsigned char>(body,0x94+i)=damp[i+3];}
  at<int>(body,0x2B0)=old?1:0;at<int>(body,0x2B4)=old?3:0;
  for(unsigned axis=0;axis<3;axis++){
   at<int>(body,0x2B8+axis*4)=at<int>(body,8+axis*4)+10000;
   at<int>(body,0x2C4+axis*4)=at<int>(body,8+axis*4)+20000;
   at<int>(body,0x2D0+axis*4)=(axis+1)*100;
   at<short>(body,0x2DC+axis*2)=at<short>(body,0x14+axis*2)+100;
   at<short>(body,0x2E2+axis*2)=at<short>(body,0x14+axis*2)+200;
   at<short>(body,0x2E8+axis*2)=axis+2;
  }
  calls=0;int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(body,0x4280);
  std::cout<<'['<<result<<','<<(reinterpret_cast<unsigned char*>(at<unsigned short*>(body,0x24C))-reinterpret_cast<unsigned char*>(words))
   <<','<<calls<<','<<at<int>(body,0x80)<<','<<at<int>(body,0x230)<<','<<at<int>(body,0x2B0)<<','<<at<int>(body,0x2B4);
  for(unsigned offset:{8,0x60,0x6C,0x27C})for(unsigned axis=0;axis<3;axis++)std::cout<<','<<at<int>(body,offset+axis*4);
  for(unsigned offset:{0x14,0x88,0x8E})for(unsigned axis=0;axis<3;axis++)std::cout<<','<<at<short>(body,offset+axis*2);
  for(unsigned offset:{0x2B8,0x2C4,0x2D0})for(unsigned axis=0;axis<3;axis++)std::cout<<','<<at<int>(body,offset+axis*4);
  for(unsigned offset:{0x2DC,0x2E2,0x2E8})for(unsigned axis=0;axis<3;axis++)std::cout<<','<<at<short>(body,offset+axis*2);
  std::cout<<"]\n";
 }
}
'''
    random_source = random.Random(0x405F75)
    cases = []
    for actor_type in [203, 304, 402]:
        for level in [0x101, 0x102, 0x803]:
            for skip, fast in [(False, False), (True, False), (True, True)]:
                for operand, resolved in [(0, 0), (1, 1), (3, 3), (40, 40), (0x212B, 5), (0xA001, 7),
                                          (0x8000, 0x8000), (0x212B, 0x8000), (65535, 65535)]:
                    for shape in range(3):
                        for old in [False, True]:
                            initial = [random_source.randint(-10000000, 10000000) for _ in range(12)]
                            initial += [random_source.randint(-32768, 32767) for _ in range(9)]
                            if shape == 0 or resolved > 100 and not operand & 0x2000:
                                initial[3:9] = [0]*6; initial[15:21] = [0]*6
                            cases.append(dict(actorType=actor_type, level=level, skip=skip, fast=fast, operand=operand,
                                              resolved=resolved, elapsed=6, local=shape == 2, old=old, mode=shape,
                                              initial=initial))
    rows = [' '.join(map(str, [case['actorType'], case['level'], int(case['skip']), int(case['fast']), case['operand'],
                              case['resolved'], case['elapsed'], int(case['local']), int(case['old']), case['mode'], *case['initial']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-timed-motion-reference-') as directory:
        cpp, binary = Path(directory)/'reference.cpp', Path(directory)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-m32', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    records = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-timed-motion.json').write_text(json.dumps(records, separators=(',', ':'))+'\n')
    print(f'Captured {len(records)} original4280 cases using genuine404C50/469510 physics: gates, raw/variable duration, old recordings and source position/angle restoration.')


if __name__ == '__main__':
    main()
