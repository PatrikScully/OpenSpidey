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
            target.parent.mkdir(parents=True, exist_ok=True)
            os.link(copied[identity], target)
        else:
            copy_file(library, target)
            copied[identity] = target
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


