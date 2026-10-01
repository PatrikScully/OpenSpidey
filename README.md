# Spidey Decomp

Decompilation of Spider-Man (2000) for PC. The game was made by Neversoft and
ported to Windows by LTI Gray Matter. The goal is readable, buildable C++
that does what the original binary does, function by function, and a native
build of the game that runs on today's systems.

This repository is a fork of [krystalgamer/spidey-decomp](https://github.com/krystalgamer/spidey-decomp),
the original project. All the groundwork, the tooling and the reverse
engineering method come from there. Functions that are finished here go back
upstream as pull requests. This fork adds the standalone build (no original
exe code runs, only its data files are used) and uses AI coding agents for a
lot of the decompiling and debugging work, with the original game running
under Wine as the reference.

## What you need

The game data from your own copy of Spider-Man (2000) for PC. The build
never ships game data. From the installed game directory you need:

- `data.pkr` and `media.pkr` (the archives with models, textures, sounds)
- `texture.dat`
- `SpideyPC.exe` (only its data section is read, no code from it runs)

Put them in one directory. That directory is the "game dir" below.

## How it runs

Two builds come out of this repository.

1. The standalone game (`spider`). A 32 bit native binary with an SDL3 and
   OpenGL backend. Everything it does is our decompiled code. This is the
   Phase 2 build. It renders the maps, runs player and enemy logic, plays
   sound effects, streamed dialogue, music and movies. Mission progression
   and remaining gameplay functions are still being restored and tested.
2. The Phase 1 DLL (`binkw32.dll`). A drop in for the game's Bink DLL on
   Windows (or Wine). It loads the original game and hooks the decompiled
   functions into it, one by one. This is how each function is checked
   against the original.

## Linux

Build with Docker (no 32 bit packages needed on the host):

```
git archive --format=tar --prefix=src/ HEAD > /tmp/ctx.tar
tar -rf /tmp/ctx.tar --transform 's,^platform/,,' platform/Dockerfile.sdl3
docker build -t spidey-sa -f Dockerfile.sdl3 - < /tmp/ctx.tar
id=$(docker create spidey-sa); docker cp $id:/out ./sa-run; docker rm $id
```

`sa-run/` then holds `spider` and the i386 `libSDL3.so.0` it needs. Run it:

```
cd sa-run
LD_LIBRARY_PATH=. ./spider /path/to/game-dir
```

Or build natively. You need a 32 bit toolchain and SDL3 for i386
(`g++-multilib cmake libsdl3-dev:i386 libgl-dev:i386` on Ubuntu 25.04 or
newer):

```
cmake -B out-sa -DSPIDEY_STANDALONE=ON -DSPIDEY_BACKEND=sdl3 -DCMAKE_BUILD_TYPE=RelWithDebInfo
cmake --build out-sa -j8
./out-sa/spider /path/to/game-dir
```

Keys: Enter selects, WASD or the arrow keys move, Space jumps, left mouse
punches and right mouse fires webs. The mouse moves the camera. Escape
pauses or skips an eligible scene; F12 quits. `SPIDEY_MOUSE_SENSITIVITY`
sets camera speed (default 3), and `SPIDEY_MODERN_CONTROLS=0` restores the
original keyboard controls. Set `SPIDEY_FULLSCREEN=1` for fullscreen.
`SPIDEY_BACKEND=null` builds a headless version that runs the
game logic without a window (used for tests and CI).

Install `ffmpeg` on the machine running the game to decode the original
Bink movies, dialogue and streamed music. Playback reports a failure if
the decoder or requested original asset is unavailable. `SPIDEY_TRACE_MUSIC=1`
logs streamed track names and completion.

The SDL3 renderer smooths distant repeating world surfaces with mipmaps and
up to 8x anisotropic filtering. `SPIDEY_MIPMAPS=0` restores the original
filtering; `SPIDEY_ANISOTROPY=1` disables anisotropy. Sprites and atlas regions
keep their original sampling.

Debugging switches (environment variables): `SPIDEY_KEYS="6000:enter,4000:enter"`
presses keys at the given times in ms, `SPIDEY_QUIT_MS=N` ends the run after N
ms, `SPIDEY_TRACE_PLAYER=1` prints the player and camera state once per frame,
`SPIDEY_DUMPPOLYS=N` with `SPIDEY_DUMPPOLYS_AT=ms` prints the polygons of N
frames, `SPIDEY_NOCULL=1`, `SPIDEY_NODEPTH=1` and `SPIDEY_GLDEBUG=ms` change or
log the OpenGL state in the SDL3 backend.

The plain `cmake -B out && cmake --build out && ./out/spider` build is the
compile check every function has to pass on Linux. It does not run the game.

## Windows

The Phase 1 DLL is built with the same MSVC 6 toolchain the game used.
Download it from the
[spidey-decomp-vs release](https://github.com/krystalgamer/spidey-decomp-vs/releases),
extract it to `C:\vs` and run `build.bat`. The result is `Release\spider.dll`.
In your game directory rename the original `binkw32.dll` to `binkw32_.dll`
and copy `spider.dll` there as `binkw32.dll`. Start `SpideyPC.exe` as usual.
The CI builds this DLL on every push and attaches it to releases.

The standalone game has Windows code paths (see `platform/exemem.cpp`) but a
Windows build of it with SDL3 has not been tested yet.

## macOS

There is no native macOS build. The game and both builds are 32 bit x86,
and macOS dropped 32 bit support. Run the Linux build in a Linux virtual
machine, or use the Windows DLL through Wine or CrossOver with your own copy
of the game.

## Working on the code

- Every function carries one tag comment (`@Ok`, `@NotOk`, `@SMALLTODO`,
  `@MEDIUMTODO`, `@BIGTODO`, `@Bogus`). `python tools/dunno.py` checks them
  (needs `tree-sitter` and `tree-sitter-cpp`).
- `tools/differ.py` and `tools/compare.py` compare the built functions
  against the original bytes in `tools/functions/`.
- `tobey_validator` from
  [krystalgamer/tobey-validator](https://github.com/krystalgamer/tobey-validator)
  checks the struct layouts (`VALIDATE` macros) in the built DLL.
- The standalone build keeps the exe's data block mapped at its original
  address (`platform/exemem.cpp`) and replays the exe's static initializers
  (`platform/exe_static_init.cpp`, generated by `platform/gen_exe_static_init.py`).

## Credits

- [krystalgamer](https://github.com/krystalgamer) started and leads the
  original spidey-decomp project. Go there to get involved.
- Everyone who contributed matches, tools and notes upstream.
