"""Capture the untouched PC static kind2/9 SendPulse program and kind6 pulse-policy boundaries."""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import tempfile
import zlib


def authored_contacts(archive):
    with archive.open('rb') as source:
        source.seek(struct.unpack('<II', source.read(8))[1])
        _, directory_count, file_count = struct.unpack('<III', source.read(12))
        directories = [struct.unpack('<32sII', source.read(40)) for _ in range(directory_count)]
        files = [struct.unpack('<32sIiIII', source.read(52)) for _ in range(file_count)]
        contacts = []
        for directory, first, count in directories:
            for entry in files[first:first + count]:
                path = '/'.join(part for part in (directory.split(b'\0')[0].decode() + '/' +
                    entry[0].split(b'\0')[0].decode()).replace('\\', '/').split('/') if part).lower()
                if not path.endswith('_t.trg'):
                    continue
                source.seek(entry[3])
                data = source.read(entry[5])
                if entry[2] == 2:
                    data = zlib.decompress(data)
                node_count = struct.unpack_from('<H', data, 8)[0]
                offsets = struct.unpack_from('<' + 'I' * node_count, data, 12)
                ends = sorted(set(offsets)) + [len(data)]
                end_by_start = dict(zip(ends, ends[1:]))
                for node, offset in enumerate(offsets):
                    kind = struct.unpack_from('<H', data, offset)[0]
                    if kind not in (2, 9):
                        continue
                    link_count = struct.unpack_from('<H', data, offset + 2)[0]
                    links = struct.unpack_from('<' + 'H' * link_count, data, offset + 4)
                    checksum_offset = (offset + 4 + link_count * 2 + 3) & ~3
                    contacts.append(dict(bank=path, node=node, kind=kind, offset=offset,
                        data=data[offset:end_by_start[offset]].hex(), links=list(links),
                        checksum=struct.unpack_from('<I', data, checksum_offset)[0]))
        return contacts


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--archive', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <array>
#include <iostream>
#include <vector>
struct Point { unsigned short* commands; unsigned char collision,executed,initial,pulses;
 unsigned short remaining,node; unsigned checksum; Point* similar; Point* next; };
int& word(unsigned address) { return *reinterpret_cast<int*>(address); }
unsigned char& byte(unsigned address) { return *reinterpret_cast<unsigned char*>(address); }
int main(int argc,char**argv) {
 assert(argc==2); original_executable::loadOriginalImage(argv[1]);
 // Omit only Windows SEH bookkeeping and diagnostic printing. The complete source dispatch remains intact.
 std::memset(reinterpret_cast<void*>(0x4E021D),0x90,6); byte(0x4E021D)=0x31; byte(0x4E021E)=0xC0;
 std::memset(reinterpret_cast<void*>(0x4E0224),0x90,7); std::memset(reinterpret_cast<void*>(0x4E2BA0),0x90,7);
 byte(0x4015B0)=0xC3;
 auto shared=reinterpret_cast<unsigned short*>(0x5580C0); assert(shared[0]==3 && shared[1]==65535);
 int kind,remaining,pulses,links;
 while(std::cin>>kind>>remaining>>pulses>>links) {
  std::memset(reinterpret_cast<void*>(0x6B4688),0,16*8);
  std::vector<unsigned short> source{static_cast<unsigned short>(kind),static_cast<unsigned short>(links)};
  for(int i=0;i<links;i++) source.push_back(i+1);
  std::array<unsigned short,8> sourceProgram{134,static_cast<unsigned short>(remaining),3,65535};
  std::array<unsigned short,4> targetProgram{65535};
  std::array<std::array<unsigned short,4>,3> targets{};
  std::array<unsigned short*,4> nodes{}; nodes[0]=source.data();
  std::array<Point,4> points{};
  points[0].commands=kind==6?sourceProgram.data():shared; points[0].node=0; points[0].remaining=remaining;
  for(int i=1;i<4;i++) {targets[i-1][0]=6; nodes[i]=targets[i-1].data(); points[i].commands=targetProgram.data();
   points[i].node=i; points[i].pulses=pulses;points[i-1].next=&points[i];}
  word(0x6B466C)=reinterpret_cast<unsigned>(nodes.data());word(0x6B4670)=4;word(0x6B4708)=reinterpret_cast<unsigned>(points.data());
  reinterpret_cast<void(*)(unsigned short*,unsigned,int)>(0x4E0210)(points[0].commands,0,0);
  std::cout<<"{\"remaining\":"<<points[0].remaining<<",\"pulses\":[";
  for(int i=1;i<4;i++){if(i>1)std::cout<<',';std::cout<<unsigned(points[i].pulses);}
  std::cout<<"],\"queued\":[";bool comma=false;
  for(int i=0;i<16;i++) if(word(0x6B468C+i*8)) {if(comma)std::cout<<',';comma=true;std::cout<<*reinterpret_cast<unsigned short*>(0x6B4688+i*8);}
  std::cout<<"],\"sharedWords\":["<<shared[0]<<','<<shared[1]<<"]}\n";
 }
}
'''
    inputs = [[kind, remaining, pulses, links]
        for kind in (2, 9, 6)
        for remaining in ([0, 1, 2, 255, 65535] if kind == 6 else [0])
        for pulses in (0, 1, 254, 255)
        for links in (0, 1, 3)]
    with tempfile.TemporaryDirectory(prefix='openspidey-contact-command-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-I', str(root / 'src'),
                        '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        rows = subprocess.check_output([str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(' '.join(map(str, values)) for values in inputs) + '\n').encode()).decode().splitlines()
    fixtures = dict(boundaries=[dict(input=values, expected=json.loads(row))
        for values, row in zip(inputs, rows, strict=True)], authored=authored_contacts(args.archive))
    (root / 'modern/OpenSpidey.Tests/Fixtures/contact-command.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(inputs)} untouched PC whole-dispatch boundaries and {len(fixtures["authored"])} unchanged contacts in five actual mission banks.')


if __name__ == '__main__':
    main()
