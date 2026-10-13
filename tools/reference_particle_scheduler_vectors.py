"""Record original PC bit-list traversal using synthetic bits and real list mutations."""
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
struct Bit { unsigned char raw[0x40]{}; int id,list,kind,target; bool fired=false; };
unsigned roots[]={0x56EAD8,0x56E9DC,0x56EA50,0x56EB40,0x56E9E0,0x56EB30,
 0x56E9D8,0x56EB2C,0x56EB44,0x56E9A0,0x56E9E4,0x56EA34,0x56E9CC,0x56EB34};
std::array<Bit,256> bits; std::vector<int> events; int nextId,tick;
Bit*& head(int list){return *reinterpret_cast<Bit**>(roots[list]);}
Bit*& prev(Bit*b){return *reinterpret_cast<Bit**>(b->raw+4);}
Bit*& next(Bit*b){return *reinterpret_cast<Bit**>(b->raw+8);}
void detach(Bit*b){reinterpret_cast<void(__attribute__((thiscall)) *)(void*,void*)>(0x408900)(b,&head(b->list));}
void __attribute__((thiscall)) destroy(Bit*b,int){events.insert(events.end(),{-1,b->id});detach(b);}
void __attribute__((thiscall)) move(Bit*b);
void* vtable[]={reinterpret_cast<void*>(&destroy),reinterpret_cast<void*>(&move)};
Bit* create(int list,int kind,int target,bool dead=false){
 Bit*b=&bits[nextId];*b=Bit{};b->id=nextId++;b->list=list;b->kind=kind;b->target=target;
 *reinterpret_cast<void***>(b->raw)=vtable;b->raw[0x37]=dead;b->raw[0x36]=1;
 next(b)=head(list);if(next(b))prev(next(b))=b;head(list)=b;return b;
}
void __attribute__((thiscall)) move(Bit*b){
 events.insert(events.end(),{tick,b->id});if(b->fired)return;b->fired=true;
 switch(b->kind){
 case 1:create(b->target,0,0);break;
 case 2:b->raw[0x37]=1;break;
 case 3:detach(b);break;
 case 4:if(next(b))detach(next(b));break;
 case 5:if(next(b))next(b)->raw[0x37]=1;break;
 }
}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 int count;while(std::cin>>count){nextId=0;events.clear();for(auto address:roots)*reinterpret_cast<unsigned*>(address)=0;
 for(int i=0;i<count;i++){int list,kind,target,dead;std::cin>>list>>kind>>target>>dead;create(list,kind,target,dead);}
 for(tick=1;tick<=2;tick++){reinterpret_cast<void(*)()>(0x411B30)();reinterpret_cast<void(*)()>(0x408610)();}
 std::cout<<"{\"events\":[";for(unsigned i=0;i<events.size();i++){if(i)std::cout<<',';std::cout<<events[i];}
 std::cout<<"],\"heads\":[";for(int l=0;l<14;l++){if(l)std::cout<<',';std::cout<<'[';
 bool comma=false;for(Bit*b=head(l);b;b=next(b)){if(comma)std::cout<<',';comma=true;std::cout<<b->id;}std::cout<<']';}
 std::cout<<"]}\n";
 }}
'''
    rng = random.Random(0x411B30)
    cases = [dict(bits=[[list_id, 0, 0, 0] for list_id in range(14)]),
             dict(bits=[[list_id, 0, 0, 1] for list_id in range(14)])]
    for source in range(14):
        for target in range(14):
            cases.append(dict(bits=[[source, 0, 0, 0], [source, 1, target, 0]]))
    for kind in range(2, 6):
        for list_id in range(14):
            cases.append(dict(bits=[[list_id, 0, 0, 0], [list_id, kind, 0, 0]]))
    for _ in range(160):
        cases.append(dict(bits=[[rng.randrange(14), rng.randrange(6), rng.randrange(14),
                                 int(rng.randrange(5) == 0)] for _ in range(rng.randrange(1, 50))]))
    with tempfile.TemporaryDirectory(prefix='openspidey-particle-scheduler-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        input_rows = [' '.join(map(str, [len(case['bits'])] +
                      [value for bit in case['bits'] for value in bit])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                        input=('\n'.join(input_rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row))
                for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/particle-scheduler.json').write_text(
        json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} original PC move/cleanup schedules, including live list mutations.')


if __name__ == '__main__':
    main()
