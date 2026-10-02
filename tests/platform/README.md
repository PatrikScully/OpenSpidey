This test uses SDL3 and OpenGL. It does not need game assets.

Build it from the repository root with 32-bit SDL3 and OpenGL libraries:

```sh
PKG_CONFIG_LIBDIR=/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig \
g++ -m32 -std=c++11 -w -fpermissive -DSPIDEY_STANDALONE \
  -ffunction-sections -fdata-sections tests/platform/setup_sdl.cpp \
  platform/sdl3/audio_settings.cpp platform/sdl3/music_audio.cpp \
  platform/sdl3/movie_audio.cpp -Wl,--gc-sections \
  -Wl,--wrap=SDL_SetAudioStreamGain -Wl,--wrap=_Z12Plat_GfxFlipv \
  -o setup_sdl $(PKG_CONFIG_LIBDIR=/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig pkg-config --cflags --libs sdl3) \
  -lGL -lpthread
```

Run on a test X11 display with a window manager. For example, start Xvfb
with a 1920x1080 screen and Openbox, then run:

```sh
DISPLAY=:108 SDL_VIDEO_DRIVER=x11 SDL_AUDIO_DRIVER=dummy \
  SPIDEY_WINDOW_MODE=windowed SPIDEY_WIDTH=1280 SPIDEY_HEIGHT=720 \
  SPIDEY_VSYNC=0 ./setup_sdl windowed
```

Use `borderless` or `fullscreen` for both the window mode and the last
argument to check those modes. For exclusive fullscreen, choose a mode
the test display supports. On a 1920x1080 test display, set both output
dimensions to 1920 and 1080. A request for an unsupported exclusive mode
must pass the `borderless` check. A request for unsupported MSAA must still
open a working window.

The test checks viewport size, black bars, framebuffer pixels, resized
captures, movie drawing, and the gains SDL applies to all three audio
streams. The test reads movie pixels before swapping buffers. It also
checks master mute and invalid volume input. Xvfb may not support vsync;
the backend reports the actual setting in its log.

The rectangle raster test needs no game assets. Build it with the same
32-bit SDL3 and OpenGL libraries:

```sh
PKG_CONFIG_LIBDIR=/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig \
g++ -m32 -std=c++11 -w -fpermissive -DSPIDEY_STANDALONE \
  -ffunction-sections -fdata-sections tests/platform/video_raster.cpp \
  -Wl,--gc-sections -o video_raster \
  $(PKG_CONFIG_LIBDIR=/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig pkg-config --cflags --libs sdl3) \
  -lGL -lpthread
```

Run it at 640x480 and 1280x960 with `SPIDEY_MSAA=0` and `4`. It draws
opaque rectangles over a colored world and checks every framebuffer pixel,
including all four outer edges. A bare private Xvfb display is enough for
these windowed cases. If Xvfb and Mesa run in different containers, disable
the test X server's MIT-SHM extension or share their IPC namespace.

Run `video_raster borderless` with a test window manager, or a headless
Wayland compositor. Set `SPIDEY_WINDOW_MODE=borderless` and a saved size
smaller than that desktop. It checks the fullscreen state and compares
physical output dimensions with the desktop mode and its pixel density.
A 2x Wayland desktop also checks real HiDPI output. The four-edge raster
check must pass there with MSAA both off and at 4x.

Keyboard polling also needs to refresh SDL events when the game displays a
comic cover without a frame loop. SDL documents that `SDL_GetKeyboardState`
uses cached state, updated by `SDL_PumpEvents` or `SDL_PollEvent`:
https://wiki.libsdl.org/SDL3/SDL_GetKeyboardState
https://wiki.libsdl.org/SDL3/SDL_PumpEvents

Build the keyboard fixture with the same libraries:

```sh
PKG_CONFIG_LIBDIR=/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig \
g++ -m32 -std=c++11 -w -fpermissive -DSPIDEY_STANDALONE \
  -ffunction-sections -fdata-sections tests/platform/input_sdl.cpp \
  -Wl,--gc-sections -o input_sdl \
  $(PKG_CONFIG_LIBDIR=/usr/lib/i386-linux-gnu/pkgconfig:/usr/share/pkgconfig pkg-config --cflags --libs sdl3) \
  -lGL -lpthread
```

On a private Xvfb display with XTEST enabled, run the driver with
`python-xlib` installed:

```sh
DISPLAY=:108 python3 tests/platform/input_sdl.py ./input_sdl
```

The driver injects real X11 key presses and releases through XTEST and a
`WM_DELETE_WINDOW` close message. The fixture only polls platform keyboard
state; it does not render or call a separate event pump. It checks A and
Escape down/up, F12 down/up with quit state, and window close in a fresh
process. Injecting only `SDL_PushEvent` keyboard events would miss the
cached keyboard-state regression. Game timing and cover duration are not
part of this platform fixture.
