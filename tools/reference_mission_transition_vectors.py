"""Execute actual PC mission transition, sound and suspension-distance command branches."""
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
#include <string>
#include <vector>
std::vector<int> audio;int killed;
int&word(unsigned address){return *reinterpret_cast<int*>(address);}
unsigned char&byte(unsigned address){return *reinterpret_cast<unsigned char*>(address);}
void play(unsigned sound,int volume,int pitch){audio.insert(audio.end(),{105,(int)sound,volume,pitch});}
void stop(unsigned handle){audio.insert(audio.end(),{106,(int)handle});}
void kill(){killed++;}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
std::string decode(std::string hex){if(hex=="-")return "";std::string text;
 for(unsigned i=0;i<hex.size();i+=2)text.push_back(std::stoi(hex.substr(i,2),nullptr,16));return text;}
void stringBytes(const char*text){std::cout<<'[';bool comma=false;while(*text){if(comma)std::cout<<',';comma=true;std::cout<<(unsigned)(unsigned char)*text++;}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Only Windows SEH registration and engine leaf effects are replaced.
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6);byte(0x4E021D)=0x31;byte(0x4E021E)=0xC0;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7);std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 byte(0x4015B0)=0xC3;jump(0x4437E0,reinterpret_cast<void*>(&kill));
 jump(0x471C50,reinterpret_cast<void*>(&play));jump(0x4723D0,reinterpret_cast<void*>(&stop));
 int opcode,status,player,operand;std::string oldHex,nextHex;
 while(std::cin>>opcode>>status>>player>>operand>>oldHex>>nextHex){
  std::string old=decode(oldHex),next=decode(nextHex);std::memset(reinterpret_cast<void*>(0x68285C),0,16);
  std::memcpy(reinterpret_cast<void*>(0x68285C),old.c_str(),old.size()+1);
  word(0x60CFA4)=status;word(0x6A9038)=player?0x1234:0;word(0x558050)=0x12345678;word(0x54E33C)=0x23456789;
  word(0x6828A0)=100;word(0x6828A4)=200;word(0x6828A8)=300;byte(0x6828D1)=4;byte(0x6828D2)=5;
  word(0x6A9058)=17;word(0x6A905C)=123;word(0x6A9064)=-456;byte(0x6A9060)=170;byte(0x6A9068)=187;
  killed=0;audio.clear();std::vector<unsigned short> program={(unsigned short)opcode};
  if(opcode==181){unsigned size=(next.size()+2)/2;unsigned start=program.size();program.resize(start+size,0);
   std::memcpy(program.data()+start,next.c_str(),next.size()+1);}
  else if(opcode!=158)program.push_back(operand);
  program.push_back(65535);
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data(),65535,0);
  std::cout<<"{\"status\":"<<word(0x60CFA4)<<",\"endNode\":"<<word(0x558050)<<",\"distance\":"<<word(0x54E33C)<<",\"missionBytes\":";
  stringBytes(reinterpret_cast<char*>(0x68285C));
  std::cout<<",\"saved\":["<<word(0x6828A0)<<','<<word(0x6828A4)<<','<<word(0x6828A8)<<','<<(int)byte(0x6828D1)<<','<<(int)byte(0x6828D2)<<"],\"killed\":"<<killed<<",\"audio\":[";
  for(unsigned i=0;i<audio.size();i++){if(i)std::cout<<',';std::cout<<audio[i];}std::cout<<"]}\n";
 }}
'''
    cases = []
    names = [('L1A1_T', 'L1A1_T'), ('L1A1_T', 'l1a1_t'), ('L1A1_T', 'L1A2_T'), ('L1A1_T', 'L1A2a_T'),
             ('L1A3_T', 'L1A4_T'), ('L1A4_T', 'L2A1_T'), ('', ''), ('', 'L1A1_T'), ('L1A1_T', ''),
             ('abcdefghi', 'abcdefghijklmnop'), ('ÄBC', 'äbc'), ('ÄBC', 'Äbc')]
    for old, new in names:
        for status in [0, 1, 2, 3, 8, 9, -1]:
            for player in [0, 1]:
                cases.append(dict(opcode=181, status=status, player=player, operand=0, mission=old, next=new))
    for opcode in [158, 302, 170, 105, 106]:
        operands = [0, 1, 2, 7, 0x8001, 0x8002, 0x8007] if opcode == 105 else [0, 1, 2, 32767, 32768, 65535]
        for operand in operands:
            for status in [0, 3, 9]:
                cases.append(dict(opcode=opcode, status=status, player=1, operand=operand, mission='L1A1_T', next='L1A2_T'))
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-transition-') as folder:
        cpp = Path(folder) / 'reference.cpp'
        binary = Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [case['opcode'], case['status'], case['player'], case['operand'],
                                 case['mission'].encode('latin1').hex() or '-', case['next'].encode('latin1').hex() or '-'])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-transition.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC mission command executions; original string comparison/copy and branch dispatch remain untouched.')


if __name__ == '__main__':
    main()
