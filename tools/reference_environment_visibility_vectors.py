"""Capture actual PC command191 parsing, asset-name suffix hashes and static item flag writes."""
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
struct Item {unsigned next;unsigned short flags;unsigned char rest[58];};
std::vector<unsigned> hashes;std::vector<Item> items;std::vector<bool> found;int mode;unsigned short flags;
Item*find(unsigned hash){unsigned index=hashes.size();hashes.push_back(hash);
 bool hit=mode==1 || (mode==2 && index%2==0);found.push_back(hit);items[index].flags=flags;
 return hit?&items[index]:nullptr;}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
std::string decode(std::string hex){if(hex=="-")return "";std::string text;
 for(unsigned i=0;i<hex.size();i+=2)text.push_back(std::stoi(hex.substr(i,2),nullptr,16));return text;}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 // Preserve complete command dispatch, SkipString, suffix CRC and flag mutations.
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7);std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;jump(0x4C9230,reinterpret_cast<void*>(&find));
 unsigned first,last,visible,initial;std::string hex;
 while(std::cin>>hex>>first>>last>>visible>>initial>>mode){std::string name=decode(hex);flags=initial;
  hashes.clear();found.clear();items.assign(first<=last?last-first+1:0,{});
  std::vector<unsigned short> program={191};unsigned words=(name.size()+2)/2;program.resize(1+words,0);
  std::memcpy(program.data()+1,name.c_str(),name.size()+1);program.push_back(first);program.push_back(last);program.push_back(visible);program.push_back(65535);
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data(),65535,0);
  std::cout<<'[';for(unsigned i=0;i<hashes.size();i++){if(i)std::cout<<',';
   std::cout<<"["<<hashes[i]<<','<<(found[i]?1:0)<<','<<items[i].flags<<']';}std::cout<<"]\n";
 }}
'''
    cases = []
    for name in ['VaultDoor', 'N_Sign', '', 'abcdefghijkL', 'Äb', 'vaultDoor']:
        for first, last in [(0, 2), (8, 11), (95, 99), (9, 8), (100, 102), (255, 256), (65535, 65535)]:
            for visible in [0, 1, 2, 256, 65535]:
                for flags in [0, 1, 8, 0x1020, 65535]:
                    for mode in [0, 1, 2]:
                        cases.append(dict(name=name, first=first, last=last, visible=visible, flags=flags, mode=mode))
    with tempfile.TemporaryDirectory(prefix='openspidey-environment-visibility-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [case['name'].encode('latin1').hex() or '-', case['first'], case['last'],
                                 case['visible'], case['flags'], case['mode']])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/environment-visibility.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC command191 executions; original suffix CRC, inclusive range and item flag writes remain untouched.')


if __name__ == '__main__':
    main()
