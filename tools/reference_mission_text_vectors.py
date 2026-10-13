"""Capture original TextMessage198 alignment, input-action substitution and shared source text buffers."""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    image = args.original_exe.read_bytes()
    pe = struct.unpack_from('<I', image, 60)[0]
    count, size = struct.unpack_from('<H', image, pe + 6)[0], struct.unpack_from('<H', image, pe + 20)[0]
    image_base = struct.unpack_from('<I', image, pe + 24 + 28)[0]
    sections = [struct.unpack_from('<IIII', image, pe + 24 + size + index * 40 + 8)[1:] for index in range(count)]

    def read(address, length):
        relative = address - image_base
        for virtual, stored, offset in sections:
            if virtual <= relative and relative + length <= virtual + stored:
                return image[offset + relative - virtual:offset + relative - virtual + length]
        raise ValueError(f'Original data is outside its stored PE section: {address:X}')

    def string(address):
        result = bytearray()
        for index in range(256):
            value = read(address + index, 1)[0]
            if value == 0:
                return result.decode('latin1')
            result.append(value)
        raise ValueError('Unterminated original message table string')

    substitutions = []
    for index in range(9):
        pattern, format_address = struct.unpack('<II', read(0x558054 + index * 8, 8))
        first, second = struct.unpack('<HH', read(0x55809C + index * 4, 4))
        substitutions.append(dict(pattern=string(pattern), format=string(format_address), first=first, second=second))
    code = r'''
#include "original_executable.h"
#include <iostream>
#include <vector>
#include <string>
#include <cstdarg>
#include <cstdio>
int mode;std::vector<unsigned>actions;char*output;unsigned outX,outY,outDuration;int clears;
void binding(unsigned action,unsigned*code){actions.push_back(action);if(action==0 || mode==0 || (mode==2&&action==64) || (mode==3&&action==32)){*code=0x4000;return;}
 switch(action){case 16:*code=32;break;case 32:*code='K';break;case 64:*code='J';break;case 128:*code='L';break;
 case 256:*code='T';break;case 512:*code='E';break;case 1024:*code='S';break;default:std::cerr<<"Unexpected source action "<<action<<'\n';assert(false);}}
void keyName(unsigned char code,char*name){if(code==0)std::strcpy(name,"NoKey");else if(code==32)std::strcpy(name,"Space");else{name[0]=code;name[1]=0;}}
int format(char*dest,const char*fmt,...){va_list args;va_start(args,fmt);int n=std::vsnprintf(dest,256,fmt,args);va_end(args);assert(n>=0&&n<256);return n;}
void*show(char*text,unsigned x,unsigned y,unsigned duration){output=text;outX=x;outY=y;outDuration=duration;return nullptr;}
void clear(){clears++;}
void jump(unsigned address,void* target){unsigned char*c=reinterpret_cast<unsigned char*>(address);c[0]=0xE9;
 *reinterpret_cast<int*>(c+1)=reinterpret_cast<int>(target)-address-5;}
std::string decode(std::string hex){if(hex=="-")return "";std::string text;
 for(unsigned i=0;i<hex.size();i+=2)text.push_back(std::stoi(hex.substr(i,2),nullptr,16));return text;}
void bytes(const std::string&text){std::cout<<'[';for(unsigned i=0;i<text.size();i++){if(i)std::cout<<',';std::cout<<(unsigned)(unsigned char)text[i];}std::cout<<']';}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6);*reinterpret_cast<unsigned short*>(0x4E021D)=0xC031;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7);std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 *reinterpret_cast<unsigned char*>(0x4015B0)=0xC3;
 jump(0x50A400,reinterpret_cast<void*>(&binding));jump(0x501550,reinterpret_cast<void*>(&keyName));jump(0x529F90,reinterpret_cast<void*>(&format));
 jump(0x458850,reinterpret_cast<void*>(&show));jump(0x4588E0,reinterpret_cast<void*>(&clear));
 unsigned x,y,duration;int initialMode;std::string hex;
 while(std::cin>>hex>>initialMode>>x>>y>>duration){std::string text=decode(hex);std::vector<unsigned short> program={198};
  unsigned words=(text.size()+2)/2;program.resize(1+words,0);std::memcpy(program.data()+1,text.c_str(),text.size()+1);
  program.push_back(x);program.push_back(y);program.push_back(duration);program.push_back(65535);mode=initialMode;actions.clear();clears=0;
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data(),65535,0);char*first=output;std::string original=output;
  std::cout<<"{\"text\":";bytes(original);std::cout<<",\"actions\":[";for(unsigned i=0;i<actions.size();i++){if(i)std::cout<<',';std::cout<<actions[i];}
  std::cout<<"],\"x\":"<<outX<<",\"y\":"<<outY<<",\"duration\":"<<outDuration;
  mode=2;actions.clear();reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data(),65535,0);
  std::cout<<",\"second\":";bytes(output);std::cout<<",\"firstAfterRepeat\":";bytes(first);
  unsigned short clearing[]={218,65535};reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(clearing,65535,0);
  std::cout<<",\"clears\":"<<clears<<"}\n";
 }}
'''
    messages = [item['pattern'].replace('??', 'AB') for item in substitutions]
    messages += ['AB to Punch and CD to Kick', 'aB to Punch and cD to Kick', 'Press E to open the vault',
                 '?? to fire webs EXTRA', 'ab to punch and cd to kick', '', 'cafÉ', 'Press ']
    cases = []
    for text in messages:
        for mode in [0, 1, 2, 3]:
            if mode == 3 and len(text) == len(substitutions[0]['pattern']) and 'to Punch and' in text:
                continue  # Original sprintf passes one string to a two-placeholder format; no undefined read is executed.
            for x, y, duration in [(256, 64, 125), (65535, 32768, 65535)]:
                cases.append(dict(text=text, mode=mode, x=x, y=y, duration=duration))
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-text-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = [' '.join(map(str, [case['text'].encode('latin1').hex() or '-', case['mode'], case['x'], case['y'], case['duration']])) for case in cases]
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-text.json').write_text(json.dumps(dict(substitutions=substitutions, cases=fixtures), separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} actual PC198/218 traces; original matching, table action IDs, printf arity branches and shared buffer reuse remain untouched.')


if __name__ == '__main__':
    main()
