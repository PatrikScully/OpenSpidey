"""Capture untouched CChunkControl AddChunk/AI and every original Chnk packet from the source archive."""
import argparse
import json
from pathlib import Path
import random
import struct
import subprocess
import tempfile
import zlib


def chunk_packets(path):
    with path.open('rb') as source:
        source.seek(struct.unpack('<II', source.read(8))[1])
        _, directory_count, file_count = struct.unpack('<III', source.read(12))
        directories = [struct.unpack('<32sII', source.read(40)) for _ in range(directory_count)]
        files = [struct.unpack('<32sIiIII', source.read(52)) for _ in range(file_count)]
        result = []
        for directory, first, count in directories:
            for entry in files[first:first + count]:
                asset = '/'.join(part for part in (directory.split(b'\0')[0].decode() + '/' +
                    entry[0].split(b'\0')[0].decode()).replace('\\', '/').split('/') if part).lower()
                if not asset.endswith('_g.psx'):
                    continue
                source.seek(entry[3]); data = source.read(entry[5])
                if entry[2] == 2:
                    data = zlib.decompress(data)
                word = lambda offset: struct.unpack_from('<I', data, offset)[0]
                offset = word(4)
                while offset and word(offset) != 0xFFFFFFFF:
                    kind, size = word(offset), word(offset + 4)
                    if kind == 0x6B6E6843:
                        packet = data[offset + 8:offset + 8 + size]
                        count = struct.unpack_from('<I', packet)[0]; records = []; start = 4 + count * 8
                        for index in range(count):
                            checksum, byte_offset = struct.unpack_from('<II', packet, 4 + index * 8)
                            record_offset = start + (byte_offset >> 2) * 4
                            packed = struct.unpack_from('<I', packet, record_offset)[0]
                            stationary, moving = packed & 65535, packed >> 16
                            pieces = struct.unpack_from('<' + 'I' * (stationary + moving), packet, record_offset + 4)
                            records.append(dict(checksum=checksum, offset=byte_offset,
                                stationary=list(pieces[:stationary]), moving=list(pieces[stationary:])))
                        result.append(dict(asset=asset, packet=packet.hex(), records=records))
                    offset += 8 + size
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    parser.add_argument('--archive', type=Path, required=True)
    args = parser.parse_args(); root = Path(__file__).resolve().parent.parent
    code = r'''
#include "original_executable.h"
#include <array>
#include <cmath>
#include <iostream>
struct Item { unsigned char bytes[64]; };
struct Entry { Item* item; int velocity[3]; int reserved; short angular[3],padding; unsigned lifetime; };
static_assert(sizeof(Entry)==32);
template<class T>T& at(void* raw,unsigned offset){return *reinterpret_cast<T*>(reinterpret_cast<unsigned char*>(raw)+offset);}
int& word(unsigned address){return *reinterpret_cast<int*>(address);}
void capture(unsigned char*control,Entry*entries,Item*items,int added) {
 std::cout<<"{\"bodyFlags\":"<<at<unsigned short>(control,0x46)<<",\"added\":"<<at<unsigned short>(control,0xFA)<<",\"entries\":[";
 for(int i=0;i<added;i++){if(i)std::cout<<',';Entry&e=entries[i];std::cout<<'['<<(e.item?1:0)<<','<<e.velocity[0]<<','<<e.velocity[1]<<','<<e.velocity[2]
  <<','<<e.angular[0]<<','<<e.angular[1]<<','<<e.angular[2]<<','<<e.lifetime<<']';}
 std::cout<<"],\"items\":[";
 for(int i=0;i<4;i++){if(i)std::cout<<',';std::cout<<'['<<at<unsigned short>(&items[i],4);
  for(unsigned p:{8u,12u,16u})std::cout<<','<<at<int>(&items[i],p);
  for(unsigned p:{20u,22u,24u})std::cout<<','<<at<short>(&items[i],p);std::cout<<']';}
 std::cout<<"],\"random\":["<<word(0x6B4C7C)<<','<<word(0x6B4B90)<<','<<word(0x6B4B94)<<"]}";
}
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
 for(int i=0;i<4096;i++){double angle=i*0.001536096911877394;reinterpret_cast<short*>(0x610C48)[i*2]=std::sin(angle)*4096.;reinterpret_cast<short*>(0x610C48)[i*2+1]=std::cos(angle)*4096.;}
 int seed,capacity,requested,steps;
 while(std::cin>>seed>>capacity>>requested>>steps){
  std::array<unsigned char,256>control{};std::array<Entry,4>entries{};std::array<Item,4>items{};
  for(auto&item:items){int flags,x,y,z,ax,ay,az;std::cin>>flags>>x>>y>>z>>ax>>ay>>az;
   at<unsigned short>(&item,4)=flags;at<int>(&item,8)=x;at<int>(&item,12)=y;at<int>(&item,16)=z;
   at<short>(&item,20)=ax;at<short>(&item,22)=ay;at<short>(&item,24)=az;}
  std::array<unsigned,2>vtable{0,0x4606F0};at<unsigned>(control.data(),0)=reinterpret_cast<unsigned>(vtable.data());
  at<unsigned short>(control.data(),0x46)=0x16;at<unsigned short>(control.data(),0xF8)=capacity;at<Entry*>(control.data(),0xFC)=entries.data();
  reinterpret_cast<void(*)(int)>(0x4E5D80)(seed);
  for(int i=0;i<requested;i++)reinterpret_cast<void(__attribute__((thiscall)) *)(void*,Item*)>(0x4272F0)(control.data(),&items[i]);
  std::cout<<'[';capture(control.data(),entries.data(),items.data(),at<unsigned short>(control.data(),0xFA));
  for(int step=0;step<steps;step++){unsigned ticks;std::cin>>ticks;at<unsigned>(control.data(),0x80)=ticks;
   reinterpret_cast<void(__attribute__((thiscall)) *)(void*)>(0x427230)(control.data());std::cout<<',';
   capture(control.data(),entries.data(),items.data(),at<unsigned short>(control.data(),0xFA));}
  std::cout<<"]\n";
 }
}
'''
    rng = random.Random(0x427230)
    cases = []
    for seed in [0, 1, -1, 0x12A6CC58, -2147483648, 2147483647]:
        for capacity in [0, 1, 3]:
            for requested in [0, 1, 4]:
                items = [[rng.choice([0, 1, 0x20, 0x8001, 65535]),
                    *[rng.choice([-2147483648, 2147483647, rng.randint(-40000000, 40000000)]) for _ in range(3)],
                    *[rng.choice([-32768, 32767, rng.randint(-32768, 32767)]) for _ in range(3)]] for _ in range(4)]
                cases.append(dict(seed=seed, capacity=capacity, requested=requested, items=items,
                    ticks=[0, 1, 2, 6, 112, 128, 65535, 4294967295]))
    rows = []
    for case in cases:
        values = [case['seed'], case['capacity'], case['requested'], len(case['ticks'])]
        for item in case['items']: values.extend(item)
        values.extend(case['ticks']); rows.append(' '.join(map(str, values)))
    with tempfile.TemporaryDirectory(prefix='openspidey-environment-chunk-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'; cpp.write_text(code)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv', '-I', str(root / 'src'),
            '-I', str(root / 'tests/runtime'), str(cpp), '-o', str(binary)], check=True)
        output = subprocess.check_output([str(binary), str(args.original_exe.resolve())], input=('\n'.join(rows) + '\n').encode())
    fixtures = dict(controls=[dict(**case, expected=json.loads(row))
        for case, row in zip(cases, output.decode().splitlines(), strict=True)], packets=chunk_packets(args.archive))
    (root / 'modern/OpenSpidey.Tests/Fixtures/environment-chunk.json').write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(cases)} whole original CChunkControl sequences/{len(cases) * 8} AI updates and51 actual Chnk records in7 banks.')


if __name__ == '__main__': main()
