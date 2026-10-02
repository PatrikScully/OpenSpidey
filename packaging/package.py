"""Create an asset-free OpenSpidey folder with a frozen setup app."""
import argparse
import importlib.metadata
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parent.parent


def copy_file(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)


def linux_runtime(binary, output):
    library_root = Path('/usr/lib/i386-linux-gnu')
    extras = [library_root / name for name in (
        'libGL.so.1', 'libGLX_mesa.so.0', 'libX11.so.6', 'libXext.so.6',
        'libXcursor.so.1', 'libXi.so.6', 'libXfixes.so.3', 'libXrandr.so.2',
        'libXss.so.1', 'libpulse.so.0', 'libasound.so.2', 'libudev.so.1',
        'dri/swrast_dri.so')]
    extras.extend(path for path in (library_root / 'dri').glob('*_dri.so') if path not in extras)
    libraries = set()
    for item in [binary] + extras:
        if not item.is_file():
            raise RuntimeError('A Linux runtime dependency is missing: ' + str(item))
        lines = subprocess.check_output(['lddtree', '-l', str(item)], text=True).splitlines()
        libraries.update(Path(line) for line in lines if line.startswith('/') and Path(line) != item)
        if item != binary:
            libraries.add(item)
    packages = set()
    owned_libraries = set(libraries)
    copied = {}
    for library in sorted(libraries):
        if library.name.startswith('ld-linux'):
            target = output / 'runtime/ld-linux.so.2'
        elif library.parent.name == 'dri':
            target = output / 'runtime/lib32/dri' / library.name
        else:
            target = output / 'runtime/lib32' / library.name
        identity = (library.stat().st_dev, library.stat().st_ino)
        if identity in copied:
            if copied[identity] == target:
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            os.link(copied[identity], target)
        else:
            copy_file(library, target)
            copied[identity] = target
    # Keep SDL first without putting bundled libc or Mesa ahead of the host.
    sdl_override = output / 'runtime/sdl3/libSDL3.so.0'
    sdl_override.parent.mkdir(parents=True, exist_ok=True)
    os.link(output / 'runtime/lib32/libSDL3.so.0', sdl_override)
    # The frozen Python app also includes native libraries from the builder.
    for library in (output / '_internal').rglob('*'):
        if library.is_file() and '.so' in library.name:
            for directory in ['/usr/lib/x86_64-linux-gnu', '/lib/x86_64-linux-gnu']:
                original = Path(directory) / library.name
                if original.is_file():
                    owned_libraries.add(original)
                    break
    for library in sorted(owned_libraries):
        owners = subprocess.run(['dpkg-query', '-S', str(library)], capture_output=True, text=True)
        if owners.returncode:
            owners = subprocess.run(['dpkg-query', '-S', str(library.resolve())], capture_output=True, text=True)
        for line in owners.stdout.splitlines():
            package = line.rsplit(': ', 1)[0]
            packages.add(package)
            copyright_file = Path('/usr/share/doc') / package.split(':')[0] / 'copyright'
            if copyright_file.is_file():
                copy_file(copyright_file, output / 'licenses/runtime' / (package.replace(':', '-') + '.txt'))
    source_packages = {}
    for package in sorted(packages):
        value = subprocess.check_output(['dpkg-query', '-W', '-f=${source:Package}\t${source:Version}', package], text=True).strip()
        name, version = value.split('\t')
        source_packages[name] = version
    (output / 'runtime-packages.json').write_text(json.dumps({'binary_packages': sorted(packages), 'source_packages': source_packages}, indent=2) + '\n')


def package(args):
    if args.output.exists():
        raise RuntimeError('Choose a new output folder: ' + str(args.output))
    work = args.output.parent / (args.output.name + '-build')
    work.mkdir(parents=True, exist_ok=True)
    subprocess.run([sys.executable, '-m', 'PyInstaller', '--noconfirm', '--clean', '--noupx',
                    '--onedir', '--windowed', '--name', 'OpenSpidey',
                    '--distpath', str(work / 'dist'), '--workpath', str(work / 'work'),
                    '--specpath', str(work), str(ROOT / 'platform/launcher/launcher.py')], check=True)
    shutil.copytree(work / 'dist/OpenSpidey', args.output, symlinks=True)
    game_name = 'spider.exe' if args.platform == 'windows' else 'spider.bin'
    copy_file(args.binary, args.output / game_name)
    ffmpeg_name = 'ffmpeg.exe' if args.platform == 'windows' else 'ffmpeg'
    copy_file(args.ffmpeg_prefix / 'bin' / ffmpeg_name, args.output / ffmpeg_name)
    shutil.copytree(args.ffmpeg_prefix / 'share/openspidey/ffmpeg', args.output / 'licenses/ffmpeg', dirs_exist_ok=True)
    copy_file(ROOT / 'packaging/THIRD_PARTY.md', args.output / 'licenses/THIRD_PARTY.md')
    copy_file(ROOT / 'README.md', args.output / 'README.md')
    copy_file(ROOT / 'packaging/QUICK_START.txt', args.output / 'START_HERE.txt')
    if args.platform == 'windows':
        copy_file(args.sdl_prefix / 'bin/SDL3.dll', args.output / 'SDL3.dll')
        copy_file(args.sdl_prefix / 'share/licenses/SDL3/LICENSE.txt', args.output / 'licenses/SDL3.txt')
        compiler = args.compiler
        for name in ['libgcc_s_dw2-1.dll', 'libstdc++-6.dll', 'libwinpthread-1.dll', 'zlib1.dll']:
            location = subprocess.check_output([compiler, '-print-file-name=' + name], text=True).strip()
            if location == name:
                location = str(Path(args.runtime_bin) / name)
            if not Path(location).is_file():
                raise RuntimeError('A Windows runtime dependency is missing: ' + name)
            copy_file(location, args.output / name)
        for name in ['gcc-libs', 'libgcc', 'libstdc++', 'winpthreads', 'zlib']:
            location = Path(args.runtime_bin).parent / 'share/licenses' / name
            if location.exists():
                shutil.copytree(location, args.output / 'licenses' / name, dirs_exist_ok=True)
        copy_file(args.output / 'OpenSpidey.exe', args.output / 'OpenSpidey Settings.exe')
    else:
        linux_runtime(args.binary, args.output)
        copy_file(args.sdl_license, args.output / 'licenses/SDL3.txt')
        wrapper = args.output / 'spider'
        wrapper.write_text('''#!/bin/sh
set -eu
app_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
if [ "$#" -eq 0 ] || [ "${1-}" = "--settings" ]; then
    exec "$app_dir/OpenSpidey" "$@"
fi
export PATH="$app_dir:$PATH"
export LIBGL_DRIVERS_PATH="$app_dir/runtime/lib32/dri"
exec "$app_dir/runtime/ld-linux.so.2" --library-path "$app_dir/runtime/lib32:/usr/lib/i386-linux-gnu:/usr/lib32" "$app_dir/spider.bin" "$@"
''')
        wrapper.chmod(0o755)
        copy_file(args.output / 'OpenSpidey', args.output / 'OpenSpidey-Settings')
    for name in ['LICENSE.txt', 'LICENSE']:
        location = Path(sys.base_prefix) / name
        if location.is_file():
            copy_file(location, args.output / 'licenses/Python.txt')
            break
    if args.platform == 'linux':
        for package_name, license_name in [('python3.10', 'python3.10'), ('libtcl8.6', 'tcl8.6'), ('libtk8.6', 'tk8.6')]:
            location = Path('/usr/share/doc') / package_name / 'copyright'
            copy_file(location, args.output / 'licenses' / (license_name + '.txt'))
    else:
        copy_file(ROOT / 'packaging/licenses/Tcl.txt', args.output / 'licenses/tcl8.6.txt')
        for name in ['tcl8.6', 'tk8.6']:
            location = Path(sys.base_prefix) / 'tcl' / name / 'license.terms'
            if location.is_file():
                copy_file(location, args.output / 'licenses' / (name + '.txt'))
    distribution = importlib.metadata.distribution('pyinstaller')
    for name in distribution.files or []:
        if str(name).endswith('COPYING.txt'):
            copy_file(distribution.locate_file(name), args.output / 'licenses/PyInstaller.txt')
    manifest = {'version': args.version, 'platform': args.platform, 'game_assets': False,
                'runtime_dependencies': json.loads((ROOT / 'packaging/versions.json').read_text())}
    (args.output / 'build-info.json').write_text(json.dumps(manifest, indent=2) + '\n')
    subprocess.run([sys.executable, str(ROOT / 'packaging/check_package.py'), str(args.output)], check=True)
    forbidden = {'data.pkr', 'media.pkr', 'texture.dat', 'spideypc.exe'}
    if any(path.name.lower() in forbidden for path in args.output.rglob('*')):
        raise RuntimeError('The package contains game assets.')
    print(args.output)




if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--platform', choices=('linux', 'windows'), required=True)
    parser.add_argument('--binary', type=Path, required=True)
    parser.add_argument('--ffmpeg-prefix', type=Path, required=True)
    parser.add_argument('--sdl-prefix', type=Path)
    parser.add_argument('--sdl-license', type=Path)
    parser.add_argument('--runtime-bin', default='/mingw32/bin')
    parser.add_argument('--compiler', default='g++')
    parser.add_argument('--version', default='development')
    parser.add_argument('--output', type=Path, required=True)
    package(parser.parse_args())
