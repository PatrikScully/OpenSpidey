"""Capture actual PC140/176 checkpoint sequences, source lookup, notification order and original save-name copy."""
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
    names = ['Re_Start_Death', 'Re_Start', 'Checkpoint', 'RE_START', 'CAFÉ', 'a' * 49]
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <string>
#include <vector>
std::vector<int> messages;
void clear(){messages.push_back(1);}void show(const char*,void*program){assert(program==nullptr);messages.push_back(2);}
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
std::string decode(std::string hex){if(hex=="-")return "";std::string text;
 for(unsigned i=0;i<hex.size();i+=2)text.push_back(std::stoi(hex.substr(i,2),nullptr,16));return text;}
void bytes(const char*text){std::cout<<'[';bool comma=false;while(*text){if(comma)std::cout<<',';comma=true;std::cout<<(unsigned)(unsigned char)*text++;}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Keep original node lookup, Trig_GetPosition, ASCII-only comparison, save-name copy and command branches.
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7);std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;jump(0x4587A0,reinterpret_cast<void*>(&clear));jump(0x458800,reinterpret_cast<void*>(&show));
 int nodeCount;std::cin>>nodeCount;std::vector<std::vector<unsigned short>> nodes(nodeCount+1);
 nodes[0]={4,65535};for(int i=1;i<=nodeCount;i++){std::string hex;std::cin>>hex;std::string name=decode(hex);
  nodes[i].assign(11+(name.size()+2)/2+1,0);nodes[i][0]=8;
  std::memcpy(nodes[i].data()+11,name.c_str(),name.size()+1);nodes[i].back()=65535;}
 std::vector<unsigned short*> pointers;for(auto&node:nodes)pointers.push_back(node.data());
 word(0x6B466C)=reinterpret_cast<int>(pointers.data());word(0x6B4670)=pointers.size();
 int count;while(std::cin>>count){word(0x55804C)=65535;word(0x6B470C)=0;
  std::memset(reinterpret_cast<void*>(0x682865),0,50);std::memcpy(reinterpret_cast<void*>(0x682865),"initial",8);
  std::cout<<'[';for(int step=0;step<count;step++){int opcode;std::string hex;std::cin>>opcode>>hex;std::string name=decode(hex);
   std::vector<unsigned short> program={static_cast<unsigned short>(opcode)};unsigned words=(name.size()+2)/2;program.resize(1+words,0);
   std::memcpy(program.data()+1,name.c_str(),name.size()+1);program.push_back(65535);messages.clear();
   reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data(),65535,0);
   if(step)std::cout<<',';std::cout<<"["<<word(0x55804C)<<','<<word(0x6B470C)<<',';bytes(reinterpret_cast<char*>(0x682865));std::cout<<",[";
   for(unsigned i=0;i<messages.size();i++){if(i)std::cout<<',';std::cout<<messages[i];}std::cout<<"]]";
  }std::cout<<"]\n";
 }}
'''
    choices = ['re_start_death', 'RE_START', 'Checkpoint', 'checkpoint', 'CAFÉ', 'cafÉ', 'a' * 49]
    cases = [dict(commands=[[140, 're_start_death'], [140, 'RE_START'], [140, 'Checkpoint'],
                            [176, 'Re_Start_Death'], [140, 'RE_START_DEATH']])]
    for first in choices:
        for second in choices:
            for opcode in [140, 176]:
                cases.append(dict(commands=[[140, first], [opcode, second], [opcode, second], [140, 're_start_death']]))
    rng = random.Random(0x4E05B7)
    cases.extend(dict(commands=[[rng.choice([140, 176]), rng.choice(choices)] for _ in range(20)]) for _ in range(30))
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-checkpoint-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [str(len(names)) + ' ' + ' '.join(name.encode('latin1').hex() for name in names)]
        rows.extend(str(len(case['commands'])) + ' ' + ' '.join(f"{opcode} {name.encode('latin1').hex()}" for opcode, name in case['commands']) for case in cases)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-checkpoint.json').write_text(json.dumps(dict(names=names, cases=fixtures), separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC140/176 sequences; original checkpoint name/first-node lookup and Front_SaveGameState copy remain untouched.')


if __name__ == '__main__':
    main()
