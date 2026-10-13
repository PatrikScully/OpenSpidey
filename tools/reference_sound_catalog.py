"""Extract bounded PC WAV group metadata; original game files stay outside the repository."""
import argparse
import json
from pathlib import Path
import struct


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('executable', type=Path)
    args = parser.parse_args()
    data = args.executable.read_bytes()
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    if data[pe:pe+4] != b'PE\0\0': raise ValueError('Not a PE executable')
    count, optional_size = struct.unpack_from('<H', data, pe+6)[0], struct.unpack_from('<H', data, pe+20)[0]
    base = struct.unpack_from('<I', data, pe+24+28)[0]
    sections = [struct.unpack_from('<8sIIII', data, pe+24+optional_size+40*i) for i in range(count)]
    def offset(address):
        relative = address-base
        for _, _, va, size, raw in sections:
            if va <= relative < va+size: return raw+relative-va
        raise ValueError(f'Unmapped metadata address {address:X}')
    def name(address):
        if address == 0: return None
        start = offset(address)
        end = data.index(b'\0',start,start+256)
        return data[start:end].decode('ascii')
    names = ['menu','spidey']
    for chapter,areas in [('1',4),('2',2),('3',5),('4',1),('5',7),('6',4),('7',5),('8',6),('9',4),('b',4),('c',4),('d',3),('e',4),('f',1),('g',1),('h',1)]:
        names += [f'l{chapter}a{area}' for area in range(1,areas+1)]
    groups = []
    for group,key in enumerate(names):
        pointers = struct.unpack_from('<64I',data,offset(0x55AD64+group*64*4))
        groups.append(dict(Name=key,Group=group,Names=[name(pointer) for pointer in pointers]))
    root = Path(__file__).resolve().parent.parent
    player = json.loads((root/'modern/OpenSpidey.Core/Audio/player-sound-names.json').read_text())
    if groups[1]['Names'] != player: raise ValueError('Player group differs from validated source table')
    output = root/'modern/OpenSpidey.Core/Audio/sound-groups.json'
    output.write_text(json.dumps(groups,separators=(',',':'))+'\n')
    print(f'Extracted {len(groups)} PC WAV groups; existing player table verified.')


if __name__ == '__main__': main()
