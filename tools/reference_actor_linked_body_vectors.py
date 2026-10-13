"""Capture original CBaddy450F/4511/4512 with real Trig_GetLinksPointer and mixed active/inactive body records."""
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
template<class T>T&at(void*body,unsigned offset){return *reinterpret_cast<T*>((unsigned char*)body+offset);}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Omit Windows exception registration only. Dispatcher, raw operand read,
 // Trig_GetLinksPointer and all matching-body loops remain untouched PC code.
 std::memset((void*)0x4050B0,0x90,6);*reinterpret_cast<unsigned short*>(0x4050B0)=0xC031;
 for(unsigned address:{0x4050BEu,0x406ACBu})std::memset((void*)address,0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;
 int opcode,value,count;
 while(std::cin>>opcode>>value>>count){
  std::vector<unsigned short> node={1,203,4096,static_cast<unsigned short>(count)};
  for(int i=0,link;i<count;i++){std::cin>>link;node.push_back(link);}
  std::array<unsigned short*,155> nodes{};nodes[154]=node.data();
  *reinterpret_cast<unsigned short***>(0x6B466C)=nodes.data();*reinterpret_cast<unsigned*>(0x6B4670)=nodes.size();
  alignas(4)unsigned char bodies[8][0x300]={};
  for(auto&body:bodies){int id,flags,first,second;std::cin>>id>>flags>>first>>second;
   at<unsigned short>(body,0xDE)=id;at<unsigned short>(body,4)=flags;at<short>(body,0x100)=first;at<short>(body,0x102)=second;}
  int active;std::cin>>active;std::vector<int> order(active);for(auto&i:order)std::cin>>i;
  for(unsigned i=0;i<order.size();i++)at<void*>(bodies[order[i]],0x20)=i+1<order.size()?bodies[order[i+1]]:nullptr;
  *reinterpret_cast<void**>(0x56E990)=order.empty()?nullptr:bodies[order[0]];
  alignas(4)unsigned char caller[0x300]={};unsigned short operand=value;
  at<unsigned short>(caller,0xDE)=154;at<unsigned short*>(caller,0x24C)=&operand;
  int result=reinterpret_cast<int(__attribute__((thiscall)) *)(void*,unsigned short)>(0x4050B0)(caller,opcode);
  std::cout<<'['<<result<<','<<(reinterpret_cast<unsigned char*>(at<unsigned short*>(caller,0x24C))-reinterpret_cast<unsigned char*>(&operand));
  for(auto&body:bodies)std::cout<<','<<at<unsigned short>(body,4)<<','<<at<short>(body,0x100)<<','<<at<short>(body,0x102);
  std::cout<<"]\n";
 }
}
'''
    random_source = random.Random(0x4068E9)
    cases = []
    for opcode in [0x450F, 0x4511, 0x4512]:
        for value in [0, 1, 100, 0x2000, 0x212B, 32767, 32768, 65535]:
            for links in [[], [3], [7, 3], [3, 3, 7, 65535], [42], [154, 102, 3]]:
                for active in [[], [0, 1, 2, 3, 4, 5], [5, 2, 0], [7, 6, 2]]:
                    bodies = [[id, random_source.randrange(65536), random_source.randint(-32768, 32767),
                               random_source.randint(-32768, 32767)] for id in [3, 7, 3, 65535, 154, 7, 102, 3]]
                    cases.append(dict(opcode=opcode, value=value, links=links, active=active, bodies=bodies))
    rows = [' '.join(map(str, [case['opcode'], case['value'], len(case['links']), *case['links'],
                              *(value for body in case['bodies'] for value in body), len(case['active']), *case['active']]))
            for case in cases]
    with tempfile.TemporaryDirectory(prefix='openspidey-linked-baddy-reference-') as directory:
        cpp, binary = Path(directory)/'reference.cpp', Path(directory)/'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-std=c++17', '-m32', '-fno-pie', '-no-pie', '-I', str(root/'src'),
                        '-I', str(root/'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
                                         input=('\n'.join(rows)+'\n').encode())
    records = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    destination = root/'modern/OpenSpidey.Tests/Fixtures/actor-linked-bodies.json'
    destination.write_text(json.dumps(records, separators=(',', ':'))+'\n')
    print(f'Captured {len(records)} original CBaddy450F/4511/4512 commands, including raw variable-like words, empty links, duplicate matches and inactive bodies.')


if __name__ == '__main__':
    main()
