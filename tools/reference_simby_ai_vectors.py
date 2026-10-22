"""Capture CSimby CheckStateFlags, Initialize and MoveFreely idle latch against SpideyPC.exe / recovered source."""
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
 alignas(4) unsigned char body[0x500]={};std::memset(body,0,sizeof body);
 *(int*)(body+0x31C)=state;
 *(short*)(body+0x314)=0;
 unsigned short flags=reinterpret_cast<unsigned short(__attribute__((thiscall))*)(void*,void*,int)>(0x403160)(body,(void*)0x555564,25);
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


def init_expected(ground: int, setup_high: int = 261, emerge: int = 0) -> list[int]:
    # Stage, Substate, BodyY, ShadowY, ShadowGround, Field3A0
    if ground == -1:
        return [8, 0, 0, 0, 0, 0]
    height = ground - (100 << 12)
    display = 0x10 if emerge else 0
    return [0, 1, height, height, ground, display]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--original-exe', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    image = args.original_exe.read_bytes()
    table = pe_slice(image, 0x555564, 25 * 4)
    entries = [struct.unpack_from('<HH', table, i * 4) for i in range(25)]
    assert entries[24] == (0, 0x001B), entries[24]
    idle = struct.unpack_from('<II', pe_slice(image, 0x5521B8, 8))
    assert idle == (513, 0), idle
    fixtures = []
    for ground in [-1, 0, 100 << 12, -50 << 12, 0x123456]:
        for emerge in [0, 1]:
            fixtures.append(dict(mode='I', input=[ground, emerge], expected=init_expected(ground, emerge=emerge)))
    # MoveFreely idle latch: SetupHigh high-byte non-zero + alert0 => RunAnim6, setup=(513,0), substate10
    fixtures.append(dict(mode='M', input=[261, 0], expected=[4, 10, 513, 0, 6]))
    fixtures.append(dict(mode='M', input=[0x0105, 100], expected=[4, 10, 513, 0, 6]))
    # FollowWaypoints (0x4A9240) leaves recovered from IDA: stage,sub,field1F0,combat,procs,completion,anim
    # (AI ORs flag-0x800 bit 0x10 into the CBody word at 0x46, not field_2A8.)
    # input = [sub, field1F0, waypointFlags, canAdvance, completionFlags]
    fixtures.append(dict(mode='W', input=[0, 0, 0, 0, 0], expected=[2, 1, 0, 0x10000201, 2, 0, 5]))
    fixtures.append(dict(mode='W', input=[1, 0, 0, 0, 1], expected=[4, 0, 0, 0x201, 0, 0, 5]))
    fixtures.append(dict(mode='W', input=[1, 3, 0, 0, 1], expected=[2, 0, 2, 0x201, 0, 0, 5]))
    flag_states = [-1, 0, 1, 2, 4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 100]
    with tempfile.TemporaryDirectory(prefix='openspidey-simby-ai-') as folder:
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
    out = root / 'modern/OpenSpidey.Tests/Fixtures/simby-ai.json'
    out.write_text(json.dumps(fixtures, separators=(',', ':')) + '\n')
    print(f'Captured {len(fixtures)} Simby AI cases '
          f'({sum(1 for c in fixtures if c["mode"]=="I")} Initialize, '
          f'{sum(1 for c in fixtures if c["mode"]=="M")} MoveFreely latch, '
          f'{sum(1 for c in fixtures if c["mode"]=="W")} FollowWaypoints, '
          f'{sum(1 for c in fixtures if c["mode"]=="F")} original CheckStateFlags).')


if __name__ == '__main__':
    main()
