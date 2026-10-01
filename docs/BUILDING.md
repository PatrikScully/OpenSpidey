# Build OpenSpidey

The release downloads are the simplest way to play. These instructions are
for building the app or working on the decompilation. Game assets are never
part of a build or package.

Clone the repository:

```sh
git clone https://github.com/PatrikScully/OpenSpidey.git
cd OpenSpidey
```

## Linux release package

The release container builds on Ubuntu 22.04 for a glibc 2.35 baseline. It
builds the 32 bit game, a 64 bit setup launcher, SDL3, the Bink decoder and
the packaged Linux runtime. Dependency downloads are versioned and checked
against the hashes in [versions.json](../packaging/versions.json).

```sh
docker build -f packaging/Dockerfile.release \
  --build-arg BUILD_VERSION=development -t openspidey-build .
build_container=$(docker create openspidey-build)
docker cp "$build_container":/out ./release-out
docker rm "$build_container"
```

Open the `OpenSpidey` app inside `release-out/openspidey-linux-x86_64/`.
The container also writes the bundled runtime's corresponding source files
under `release-out/runtime-sources/`.

## Linux source build

You need GCC with 32 bit support, CMake, 32 bit SDL3 and OpenGL development
libraries, Python 3.10 or newer with Tkinter, and FFmpeg for Bink audio and
movies. Release packages include those runtime components; a source build
uses your installed tools.

On a recent Ubuntu release with SDL3 packages:

```sh
sudo dpkg --add-architecture i386
sudo apt update
sudo apt install g++-multilib cmake pkg-config libsdl3-dev:i386 \
  libgl-dev:i386 python3-tk ffmpeg
cmake -S . -B out-sa -DSPIDEY_STANDALONE=ON \
  -DSPIDEY_BACKEND=sdl3 -DCMAKE_BUILD_TYPE=RelWithDebInfo
cmake --build out-sa -j8
./out-sa/spider
```

If your distribution has no 32 bit SDL3 package, use the release container
or build the pinned SDL3 version for i386. The normal source launcher is
copied to `out-sa/launcher/`. Start with `--settings` to reopen setup, or
pass an existing game directory to run directly:

```sh
./out-sa/spider --settings
./out-sa/spider /path/to/game-dir
```

## Native Windows source build

Use the **MINGW32** environment from MSYS2. Install the 32 bit GCC, CMake,
zlib, pkgconf and Ninja packages. Use a 64 bit Python with Tkinter for the
setup app. The native Windows CI job is the reference for packaging.

```sh
pacman -S --needed mingw-w64-i686-gcc mingw-w64-i686-cmake \
  mingw-w64-i686-zlib mingw-w64-i686-pkgconf mingw-w64-i686-ninja
```

Extract the pinned SDL3 MinGW development archive from
[versions.json](../packaging/versions.json). Set `PKG_CONFIG_PATH` to the
`lib/pkgconfig` directory in its `i686-w64-mingw32` prefix. Then configure
the game from the MINGW32 shell:

```sh
cmake -S . -B out-native-win -G Ninja -DSPIDEY_STANDALONE=ON \
  -DSPIDEY_BACKEND=sdl3 -DCMAKE_BUILD_TYPE=RelWithDebInfo
cmake --build out-native-win -j8
```

Keep `SDL3.dll` and the MinGW runtime DLLs beside `spider.exe`, or run it
from the configured MSYS2 environment. Source builds also need `python.exe`
with Tkinter on `PATH`; packaged downloads freeze the launcher and include
its dependencies. This native build is separate from the MSVC6 proxy DLL.

## Original-game Windows DLL

This build runs inside the original game and is used to compare decompiled
functions. It is not the OpenSpidey setup app.

Download the pinned
[MSVC6 toolchain](https://github.com/krystalgamer/spidey-decomp-vs/releases)
and extract it to `C:\vs`, then run:

```bat
build.bat
```

The result is `Release\spider.dll`. In your installed game folder, keep the
original `binkw32.dll` as `binkw32_.dll`, then place the new DLL there as
`binkw32.dll`. The original `SpideyPC.exe` launches this build.

Use clean build output when switching source branches. Keep the local
CD-check bypass commented out in committed source. The proxy's known late
Wine stack overflow remains under investigation.

## Checks

The default Linux build checks compilation and struct layouts. It does not
launch the game:

```sh
cmake -S . -B out
cmake --build out -j8
./out/spider
```

Run launcher and tag checks:

```sh
python3 -m unittest discover -s tests/launcher -v
python3 tools/dunno.py
```

`tools/dunno.py` needs `tree-sitter` and `tree-sitter-cpp`. GUI tests need a
display server; the CI uses Xvfb on Linux. Platform tests and game scenario
coverage are described in [tests/platform](../tests/platform/README.md) and
[tests/gameplay/scenarios.json](../tests/gameplay/scenarios.json).

The native backup-file fixture links the real save routines and needs no
game assets. Build it on Linux and run it in a private writable folder:

```sh
g++ -m32 -w -fpermissive -std=c++11 -DSPIDEY_STANDALONE \
  -ffunction-sections -fdata-sections tests/platform/save_paths.cpp \
  pcdcBkup.cpp non_win32.cpp -Wl,--gc-sections -o /tmp/openspidey-save-test
```

It checks save-directory creation, callback behavior and a file write/read
round trip. On MINGW32, use the same command without `-m32`.

Tagged releases are packaged by [the release workflow](../.github/workflows/release.yml).
The [build workflow](../.github/workflows/build.yml) checks normal changes.
Runtime licenses and source details are in [THIRD_PARTY.md](../packaging/THIRD_PARTY.md).
