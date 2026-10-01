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
