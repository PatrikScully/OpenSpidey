"""Capture untouched PC startup scalar and visibility-box mission dispatch, including real item-list writes."""
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
struct Item { unsigned char bytes[64]; };
int& word(unsigned address) { return *reinterpret_cast<int*>(address); }
unsigned char& byte(unsigned address) { return *reinterpret_cast<unsigned char*>(address); }
void set(Item& item, unsigned offset, int value) { std::memcpy(item.bytes+offset,&value,4); }
int main(int argc,char**argv) {
 assert(argc==2); original_executable::loadOriginalImage(argv[1]);
 // Only Windows exception-registration bookkeeping and debug output are omitted.
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6); byte(0x4E021D)=0x31; byte(0x4E021E)=0xC0;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7); std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 byte(0x4015B0)=0xC3;
 std::array<unsigned char,0xD00> player{}; word(0x6A9038)=reinterpret_cast<unsigned>(player.data());
 int opcode,operand,sourceOffset,visible,inside,boxCount,itemCount;
 while(std::cin>>opcode>>operand>>sourceOffset>>visible>>inside>>boxCount>>itemCount) {
  std::array<std::vector<Item>,4> lists;
  for(unsigned list=0;list<4;list++) {
   lists[list].resize(itemCount);
   for(int i=0;i<itemCount;i++) {
    int x,y,z,flags,usable; std::cin>>x>>y>>z>>flags>>usable;
    Item& item=lists[list][i]; std::memset(item.bytes,0,64); set(item,8,x);set(item,12,y);set(item,16,z);
    unsigned short shortFlags=flags; std::memcpy(item.bytes+4,&shortFlags,2); item.bytes[31]=usable?0:1;
    set(item,32,i+1<itemCount?reinterpret_cast<unsigned>(&lists[list][i+1]):0);
   }
  }
  word(0x6B2EFC)=itemCount?reinterpret_cast<unsigned>(lists[0].data()):0;
  word(0x56E990)=itemCount?reinterpret_cast<unsigned>(lists[1].data()):0;
  word(0x60DAAC)=itemCount?reinterpret_cast<unsigned>(lists[2].data()):0;
  word(0x60FB94)=itemCount?reinterpret_cast<unsigned>(lists[3].data()):0;
  byte(0x6B244A)=1;byte(0x6B244A+68)=0;
  word(0x5FCD14)=0x13579BDF; *reinterpret_cast<int*>(player.data()+0xC60)=0x2468ACE0;
  // Source offsets0/2 reproduce both absolute dword-alignment cases.
  std::vector<unsigned short> program(sourceOffset/2,0); unsigned start=program.size();
  program.push_back(opcode);
  if(opcode==147||opcode==213) program.push_back(operand);
  else {
   program.push_back(visible);program.push_back(inside);
   for(int box=0;box<boxCount;box++) {
    while(program.size()%2)program.push_back(0xA55A);
    for(int axis=0;axis<6;axis++) {int value;std::cin>>value;unsigned raw=value;program.push_back(raw&65535);program.push_back(raw>>16);}
   }
   program.push_back(255);
   // A following genuine command proves the box walker consumed exactly its own words.
   program.push_back(147);program.push_back(0x5678);
  }
  program.push_back(65535);
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(program.data()+start,65535,0);
  std::cout<<"{\"gameLevel\":"<<word(0x5FCD14)<<",\"fightMusicTime\":"<<*reinterpret_cast<int*>(player.data()+0xC60)<<",\"flags\":[";
  for(unsigned list=0;list<4;list++){if(list)std::cout<<',';std::cout<<'[';
   for(int i=0;i<itemCount;i++){if(i)std::cout<<',';unsigned short flags;std::memcpy(&flags,lists[list][i].bytes+4,2);std::cout<<flags;}
   std::cout<<']';}
  std::cout<<"]}\n";
 }
}
'''
    cases = []
    for opcode in [147, 213]:
        for operand in [0, 1, 3, 125, 255, 256, 4095, 32767, 32768, 65534, 65535]:
            cases.append(dict(opcode=opcode, operand=operand, sourceOffset=0, visible=0, inside=0, boxes=[], items=[[], [], [], []]))
    points = [(0, 0, 0), (-10, -20, -30), (10, 20, 30), (-11, -21, -31), (11, 21, 31),
              (-11, 0, 0), (0, -21, 0), (0, 0, -31), (11, 21, 0), (11, 0, 31), (0, 21, 31),
              (-10, 20, -30), (10, -20, 30), (0x7fffffff, -0x80000000, 0), (0, 0x7fffffff, -0x80000000)]
    boxes = [[[-10, -20, -30, 10, 20, 30]],
             [[10, 20, 30, -10, -20, -30]],
             [[-10, -20, -30, 10, 20, 30], [-5, -5, -5, 5, 5, 5]],
             [[-524289, -524288, -1048576, 524288, 524287, 1048575]], []]
    rng = random.Random(141192209)
    for opcode in [141, 192, 209]:
        for visible in [0, 1, 256, 65535]:
            for inside in [0, 1, 256, 65535]:
                for source_offset in [0, 2]:
                    for selected_boxes in boxes:
                        items = []
                        for list_index in range(4):
                            group = []
                            for point_index, point in enumerate(points):
                                group.append(dict(position=[value * 4096 for value in point[:3]] if point_index < 13 else list(point),
                                                  flags=rng.choice([0, 1, 8, 0x1020, 0x8001, 65535]),
                                                  usable=(point_index + list_index) % 4 != 3))
                            items.append(group)
                        cases.append(dict(opcode=opcode, operand=0, sourceOffset=source_offset,
                                          visible=visible, inside=inside, boxes=selected_boxes, items=items))
    rows = []
    for case in cases:
        row = [case['opcode'], case['operand'], case['sourceOffset'], case['visible'], case['inside'],
               len(case['boxes']), len(case['items'][0])]
        for group in case['items']:
            for item in group:
                row.extend([*item['position'], item['flags'], int(item['usable'])])
        for box in case['boxes']:
            row.extend(box)
        rows.append(' '.join(map(str, row)))
    with tempfile.TemporaryDirectory(prefix='openspidey-mission-startup-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows)+'\n').encode())
    fixtures = [dict(**case, expected=json.loads(row)) for case, row in zip(cases, result.decode().splitlines(), strict=True)]
    (root / 'modern/OpenSpidey.Tests/Fixtures/mission-startup.json').write_text(json.dumps(fixtures, separators=(',', ':'))+'\n')
    print(f'Captured {len(fixtures)} actual PC startup commands: untouched147/213 zero extension and141/192/209 box parsing, regions, list selection and flag writes.')


if __name__ == '__main__':
    main()
