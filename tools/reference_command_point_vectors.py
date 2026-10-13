"""Capture actual PC direct command-point dispatch, collision rearming and pending-list order."""
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
#include <array>
#include <iostream>
#include <vector>
struct Point { unsigned short* commands; unsigned char collision,executed,initial,pulses;
 unsigned short remaining,node; unsigned checksum; Point* similar; Point* next; };
static_assert(sizeof(Point)==0x18);
std::array<Point,16> points;
std::vector<unsigned short> completed;
unsigned short emptyCommands[]={65535};
void execute(unsigned short*,unsigned short node,int){completed.push_back(node);}
void jump(unsigned address,void* target){unsigned char*code=reinterpret_cast<unsigned char*>(address);
 code[0]=0xE9;*reinterpret_cast<int*>(code+1)=reinterpret_cast<int>(target)-address-5;}
void output(int result,int count){std::cout<<"{\"result\":"<<result<<",\"flags\":[";
 for(int i=0;i<count;i++){if(i)std::cout<<',';std::cout<<'['<<(int)points[i].collision<<','<<(int)points[i].executed<<']';}
 std::cout<<"],\"pending\":[";bool comma=false;
 for(int i=0;i<16;i++)if(*reinterpret_cast<unsigned*>(0x6B468C+8*i)){
  if(comma)std::cout<<',';comma=true;std::cout<<*reinterpret_cast<unsigned short*>(0x6B4688+8*i);}
 std::cout<<"],\"completed\":[";for(unsigned i=0;i<completed.size();i++){if(i)std::cout<<',';std::cout<<completed[i];}
 std::cout<<"]}";
}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only the source diagnostic and actual command execution boundary are replaced.
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;jump(0x4E0210,reinterpret_cast<void*>(&execute));
 int count;while(std::cin>>count){
  std::memset(reinterpret_cast<void*>(0x6B4214),0,256*4);
  std::memset(reinterpret_cast<void*>(0x6B4688),0,16*8);
  *reinterpret_cast<Point**>(0x6B4708)=nullptr;completed.clear();
  for(int i=0;i<count;i++){unsigned checksum;std::cin>>checksum;Point&p=points[i];p=Point{};
   p.commands=emptyCommands;p.node=i;p.checksum=checksum;
   auto&bucket=*reinterpret_cast<Point**>(0x6B4214+4*(checksum&255));p.similar=bucket;bucket=&p;
   auto&head=*reinterpret_cast<Point**>(0x6B4708);p.next=head;head=&p;}
  int operations;std::cin>>operations;std::cout<<'[';
  for(int op=0;op<operations;op++){int kind;unsigned checksum;std::cin>>kind>>checksum;int result=-1;
   switch(kind){
   case 0:reinterpret_cast<void(*)()>(0x4DE890)();break;
   case 1:{Point*p=reinterpret_cast<Point*(*)(unsigned,bool)>(0x4DE8D0)(checksum,false);result=p?p->node:-1;break;}
   case 2:reinterpret_cast<void(*)()>(0x4DE8B0)();break;
   case 3:reinterpret_cast<void(*)()>(0x4DE7F0)();break;
   default:assert(false);}
   if(op)std::cout<<',';output(result,count);
  }std::cout<<"]\n";
 }}
'''
    cases = []
    for count in range(0, 9):
        checksums = [0x1234F012] * count + [0x1234F112, 0xFFFFFFFF, 0]
        actions = [[0, 0]] + [[1, 0x1234F012]] * (count + 1) + [[1, 0x1234F112], [1, 0xFFFFFFFF], [1, 0], [1, 0xAAAA0012], [2, 0], [3, 0]]
        # On the next frame only the first match is contacted. Unvisited older
        # duplicates rearm and can be selected by subsequent direct triggers.
        actions += [[0, 0], [1, 0x1234F012], [2, 0], [3, 0], [1, 0x1234F012], [1, 0x1234F012], [2, 0], [3, 0]]
        cases.append(dict(checksums=checksums, actions=actions))
    rng = random.Random(0x4DE8D0)
    candidates = [0, 0xFFFFFFFF, 0x55, 0xFFFFFF55, 0x123455, 0xA98754, 0xFEED0001]
    for _ in range(240):
        checksums = [rng.choice(candidates) for _ in range(rng.randrange(1, 12))]
        actions = []
        for _ in range(12):
            actions += [[0, 0]]
            actions += [[1, rng.choice(candidates)] for _ in range(rng.randrange(1, 8))]
            actions += [[2, 0], [3, 0]]
        cases.append(dict(checksums=checksums, actions=actions))
    with tempfile.TemporaryDirectory(prefix='openspidey-command-point-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [len(case['checksums']), *case['checksums'], len(case['actions']),
                                 *[value for action in case['actions'] for value in action]])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, states=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/command-point.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC command-point sequences and {sum(len(case["actions"]) for case in fixtures)} state transitions.')


if __name__ == '__main__':
    main()
