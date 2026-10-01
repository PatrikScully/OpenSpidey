# Bundled components

OpenSpidey packages contain code and runtime libraries. They contain no
Spider-Man game assets. Choose the data from your own PC copy during setup.

- SDL 3.4.16, zlib license: https://github.com/libsdl-org/SDL/tree/release-3.4.16
- FFmpeg 8.0.1, LGPL 2.1 or later: https://ffmpeg.org/releases/ffmpeg-8.0.1.tar.xz
  This build only enables Bink decoding and raw video / PCM output. Its
  complete source archive, license, configuration and build script are in
  `licenses/ffmpeg` inside the app folder.
- CPython, Python license: https://docs.python.org/3/license.html
- Tcl and Tk, BSD style license: https://www.tcl-lang.org/software/tcltk/license.html
- PyInstaller 6.22.3, GPL with a distribution exception:
  https://pyinstaller.org/en/stable/license.html
- The Windows C++ runtime comes from MSYS2 MinGW. See its licenses in the
  `licenses` folder: https://www.msys2.org/
- Linux runtime libraries come from Ubuntu 22.04. Their package names and
  licenses are in `runtime-packages.json` and `licenses/runtime`. The release
  also has a separate runtime source archive with their source packages.

The Python and Tcl/Tk libraries are included by PyInstaller. Linux packages
include matching Mesa graphics drivers and a software rendering fallback.
The Windows Tcl license is copied from the official Tcl 8.6.13 source:
https://github.com/tcltk/tcl/blob/core-8-6-13/license.terms
