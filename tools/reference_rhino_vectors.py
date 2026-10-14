"""Capture CRhino constructor stores from recovered source and CheckStateFlags against SpideyPC.exe."""
import argparse
import json
import struct
import subprocess
import tempfile
from pathlib import Path

CPP = r'''
#include "original_executable.h"
#include <cstring>
#include <iostream>
int main(int argc,char**argv){assert(argc==2);original_executable::loadOriginalImage(argv[1]);
int state;while(std::cin>>state){
 alignas(4) unsigned char body[0x424]={};std::memset(body,0,sizeof body);
 *(int*)(body+0x31C)=state; // field_31C.bothFlags
 *(short*)(body+0x314)=0;    // field_314 starts cleared like a fresh CBaddy
 unsigned short flags=reinterpret_cast<unsigned short(__attribute__((thiscall))*)(void*,void*,int)>(0x403160)(body,(void*)0x5521D0,0xD);
 std::cout<<'['<<(int)flags<<','<<(int)*(short*)(body+0x314)<<"]\n";
}}
'''


def pe_slice(image: bytes, address: int, count: int) -> bytes:
    pe = struct.unpack_from('<I', image, 60)[0]
    optional = pe + 24
    image_base = struct.unpack_from('<I', image, optional + 28)[0]
    num = struct.unpack_from('<H', image, pe + 6)[0]
    opt_size = struct.unpack_from('<H', image, pe + 20)[0]
    table = optional + opt_size
    relative = address - image_base
    for index in range(num):
        entry = table + index * 40
        va, raw_size, offset = struct.unpack_from('<III', image, entry + 12)
        if va <= relative and relative + count <= va + raw_size:
            start = offset + (relative - va)
            return image[start:start + count]
    raise SystemExit(f'address {address:#x} outside PE sections')


def constructor_expected(node: int, attack: int = 42) -> list[int]:
    return [307, 100, attack, 175, 32, 64, node, 0, 0, attack - 240, 0x02000201, 1400, 0x201, 0]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    image = args.original_exe.read_bytes()
    strange = struct.unpack_from('<II', pe_slice(image, 0x5520B8, 8))
    assert strange == (0x201, 0), strange
    for index in range(0x17):
        field8 = struct.unpack_from('<i', pe_slice(image, 0x552208 + index * 0xC + 8, 4))[0]
        assert field8 == index, (index, field8)
    fixtures = []
    for node in [1, 10, 135, 0xFFFF]:
        for attack in [0, 42, 240, 1000]:
            fixtures.append(dict(mode='C', input=[node, attack], expected=constructor_expected(node, attack)))
    for ground in [-1, 0, 100 << 12, -50 << 12, 0x123456]:
        for attack in [0, 155, 400]:
            field358 = attack - 155
            if ground == -1:
                expected = [0, 0, 0, 0, 0, field358]
            else:
                y = ground - (100 << 12)
                expected = [y, 8, 0, y, ground, field358]
            fixtures.append(dict(mode='I', input=[ground, attack], expected=expected))
    flag_states = [-1, 0, 1, 2, 4, 6, 7, 8, 12, 14, 15, 16, 17, 18, 21, 22, 100]
    with tempfile.TemporaryDirectory(prefix='openspidey-rhino-') as folder:
        cpp, binary = Path(folder) / 'reference.cpp', Path(folder) / 'reference'
        cpp.write_text(CPP)
        subprocess.run(['g++', '-m32', '-std=c++17', '-fno-pie', '-no-pie', '-fwrapv',
                        '-I', str(root / 'src'), '-I', str(root / 'tests/runtime'),
                        str(cpp), '-o', str(binary)], check=True)
        result = subprocess.check_output(
            [str(binary), str(args.original_exe.resolve())],
            input=('\n'.join(str(state) for state in flag_states) + '\n').encode())
    for state, line in zip(flag_states, result.decode().splitlines(), strict=True):
        fixtures.append(dict(mode='F', input=[state], expected=json.loads(line)))
    out = root / 'modern/OpenSpidey.Tests/Fixtures/rhino.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} Rhino cases '
          f'({sum(1 for c in fixtures if c["mode"]=="C")} recovered constructor, '
          f'{sum(1 for c in fixtures if c["mode"]=="I")} recovered RhinoInit, '
          f'{sum(1 for c in fixtures if c["mode"]=="F")} original CheckStateFlags).')


if __name__ == '__main__':
    main()
