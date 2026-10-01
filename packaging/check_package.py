"""Check that a release has its setup app, runtime and no original assets."""
import json
from pathlib import Path
import sys
import struct

root = Path(sys.argv[1])
info = json.loads((root / 'build-info.json').read_text())
required = ['_internal/_tcl_data/init.tcl', '_internal/_tk_data/tk.tcl', 'START_HERE.txt', 'README.md', 'licenses/THIRD_PARTY.md', 'licenses/ffmpeg/ffmpeg-8.0.1.tar.xz', 'licenses/tcl8.6.txt', 'licenses/tk8.6.txt', 'licenses/PyInstaller.txt', '_internal']
if info['platform'] == 'windows':
    required += ['OpenSpidey.exe', 'OpenSpidey Settings.exe', 'spider.exe', 'SDL3.dll', 'ffmpeg.exe', 'libwinpthread-1.dll', 'libstdc++-6.dll', 'libgcc_s_dw2-1.dll', 'zlib1.dll']
else:
    required += ['OpenSpidey', 'OpenSpidey-Settings', 'spider', 'spider.bin', 'ffmpeg', 'runtime/ld-linux.so.2', 'runtime/lib32/libSDL3.so.0', 'runtime/lib32/dri/swrast_dri.so']
missing = [name for name in required if not (root / name).exists()]
if missing:
    raise SystemExit('Missing package files: ' + ', '.join(missing))
game = root / ('spider.exe' if info['platform'] == 'windows' else 'spider.bin')
if info['platform'] == 'windows':
    data = game.read_bytes()
    pe = struct.unpack_from('<I', data, 0x3C)[0]
    if data[pe:pe + 4] != b'PE\0\0' or struct.unpack_from('<H', data, pe + 4)[0] != 0x14C:
        raise SystemExit('The Windows game must be a 32 bit x86 PE executable.')
    sections = struct.unpack_from('<H', data, pe + 6)[0]
    optional = struct.unpack_from('<H', data, pe + 20)[0]
    image_base = struct.unpack_from('<I', data, pe + 24 + 28)[0]
    reserved = False
    for index in range(sections):
        offset = pe + 24 + optional + index * 40
        name = data[offset:offset + 8].rstrip(b'\0')
        virtual_size, virtual_address, raw_size = struct.unpack_from('<III', data, offset + 8)
        if name == b'.exemem':
            reserved = image_base + virtual_address == 0x53B000 and virtual_size == 0x28D1000 and raw_size == 0
    if not reserved:
        raise SystemExit('The Windows game data range must be reserved by its PE image.')
else:
    if game.read_bytes()[:5] != b'\x7fELF\x01':
        raise SystemExit('The Linux game must be a 32 bit ELF executable.')
    if (root / 'OpenSpidey').read_bytes()[:5] != b'\x7fELF\x02':
        raise SystemExit('The Linux launcher must be a 64 bit ELF executable.')
    if (root / 'OpenSpidey-Settings').read_bytes()[:5] != b'\x7fELF\x02':
        raise SystemExit('The Linux Settings shortcut must be a 64 bit ELF executable.')
if not any((root / 'licenses' / name).is_file() for name in ['Python.txt', 'python3.10.txt']):
    raise SystemExit('The Python license is missing.')
for path in root.rglob('*'):
    if path.name.lower() in {'data.pkr', 'media.pkr', 'texture.dat', 'spideypc.exe'} or path.suffix.lower() in {'.iso', '.pkr', '.bik'}:
        raise SystemExit('Game assets must not be bundled: ' + str(path))
print('The app, settings shortcut and runtime are present. No game assets are bundled.')
