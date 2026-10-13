"""Capture untouched CBaddy::DoPhysics404C50 with zero, negative and ordinary CBody clock results."""
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
void __attribute__((naked)) initHeading(){asm("pushl %ebx;pushl %ebp;pushl %esi;pushl %edi;subl $0x28,%esp;xorl %ebx,%ebx;movl $0x46cce6,%eax;call *%eax;addl $0x28,%esp;popl %edi;popl %esi;popl %ebp;popl %ebx;ret");}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);*(unsigned char*)0x46CD51=0xC3;initHeading();
 int local,flag,ticks,limit,remaining,recorded,wait;
 while(std::cin>>local>>flag>>ticks>>limit>>remaining>>recorded>>wait){alignas(4)unsigned char body[0x400]={};
  at<unsigned>(body,0x2A8)=local;at<unsigned>(body,0x2AC)=flag;at<int>(body,0x80)=ticks;at<int>(body,0x1F8)=remaining;at<int>(body,0x230)=wait;at<unsigned short>(body,0xE0)=7;
  for(unsigned offset:{8,0x60,0x6C,0x27C})for(unsigned axis=0;axis<3;axis++)std::cin>>at<int>(body,offset+axis*4);
  for(unsigned offset:{0x14,0x88,0x8E})for(unsigned axis=0;axis<3;axis++){int value;std::cin>>value;at<short>(body,offset+axis*2)=value;}
  for(unsigned offset:{0x78,0x94})for(unsigned axis=0;axis<3;axis++){int value;std::cin>>value;at<unsigned char>(body,offset+axis)=value;}
  for(unsigned axis=0;axis<3;axis++){at<int>(body,0x2B8+axis*4)=at<int>(body,8+axis*4);at<int>(body,0x2C4+axis*4)=(axis==1?-1:1)*(axis+1)*10000;at<int>(body,0x2D0+axis*4)=(axis==1?-1:1)*(axis+1)*101;
   at<short>(body,0x2DC+axis*2)=at<short>(body,0x14+axis*2);at<short>(body,0x2E2+axis*2)=(axis+1)*100;at<short>(body,0x2E8+axis*2)=axis==0?13:axis==1?-27:41;}
  if(recorded==1)at<int>(body,0x2B4)=9;else if(recorded==2)at<int>(body,0x2B0)=9;
  std::cout<<'[';for(int frame=0;frame<6;frame++){
   reinterpret_cast<void(__attribute__((thiscall))*)(void*,int)>(0x404C50)(body,limit);
   if(frame)std::cout<<',';std::cout<<'[';int count=0;
   for(unsigned offset:{8,0x60,0x27C})for(unsigned axis=0;axis<3;axis++){if(count++)std::cout<<',';std::cout<<at<int>(body,offset+axis*4);}
   for(unsigned offset:{0x14,0x88})for(unsigned axis=0;axis<3;axis++)std::cout<<','<<at<short>(body,offset+axis*2);
   std::cout<<','<<at<unsigned>(body,0x2AC)<<','<<at<unsigned short>(body,0xE0)<<','<<at<int>(body,0x1F8)<<','<<at<int>(body,0x2B0)<<','<<at<int>(body,0x2B4)<<','<<at<int>(body,0x230)<<']';
  }std::cout<<"]\n";
 }
}
'''
    rng = random.Random(0x404C50)
    cases = []
    for local in [False, True]:
        for flag in [0, 1, 4]:
            for ticks in [-6, -1, 0, 1, 2, 6]:
                for limit, remaining in [(False, 10), (True, 0), (True, 1), (True, -3)]:
                    for recorded in [0, 1, 2]:
                        for wait in [0, 9]:
                            initial = [rng.randint(-20000000, 20000000) for _ in range(12)]+[rng.randint(-32768, 32767) for _ in range(9)]
                            shifts = [rng.choice([0, 1, 4, 31, 32, 255]) for _ in range(6)]
                            cases.append(dict(local=local, flag=flag, ticks=ticks, limit=limit, remaining=remaining,
                                              recorded=recorded, wait=wait, initial=initial, shifts=shifts))
    rows = [' '.join(map(str, [int(case['local']), case['flag'], case['ticks'], int(case['limit']),
                             case['remaining'], case['recorded'], case['wait'], *case['initial'], *case['shifts']])) for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-actor-clock-motion-') as folder:
        cpp, binary = Path(folder)/'reference.cpp', Path(folder)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root/'modern/OpenSpidey.Tests/Fixtures/actor-clock-motion.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(cases)} untouched404C50 sequences / {len(cases)*6} updates including source zero and negative elapsed ticks.')


if __name__ == '__main__':
    main()
