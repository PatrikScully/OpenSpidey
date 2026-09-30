#pragma once

// Phase 2 platform API. The DirectX translation units (DXsound.cpp,
// DXinit.cpp, SpideyDX.cpp, PCTex.cpp, PCTimer.cpp) call these from their
// SPIDEY_STANDALONE branches instead of DirectDraw/D3D7/DirectInput/
// DirectSound. One backend is linked in: platform/null (headless, no-op
// renderer, scripted input) or platform/sdl3 (SDL3 + OpenGL).
//
// Plain C-style functions on purpose: the callers are 1999 C++ compiled as
// MSVC6 code on Windows, this header must stay trivial for them too.

#ifndef PLAT_H
#define PLAT_H

#include "../my_types.h"

struct SDXPolyField;

// ---------------------------------------------------------------- window

// Creates the window and the renderer. Returns 0 on failure.
i32 Plat_Init(i32 width, i32 height, i32 fullscreen);
void Plat_Shutdown(void);
// Pumps events. Returns 0 when the user asked to quit.
i32 Plat_Yield(void);
u32 Plat_Ticks(void);   // milliseconds
void Plat_Sleep(u32 ms);

// ----------------------------------------------------------------- timer

// Calls fn(user) every periodMs milliseconds from a separate thread, like
// the Win32 multimedia timer PCTimer.cpp uses. Returns a handle, 0 on error.
typedef void (*PlatTimerFn)(void* user);
u32 Plat_TimerStart(u32 periodMs, PlatTimerFn fn, void* user);
void Plat_TimerStop(u32 handle);

// -------------------------------------------------------------- graphics

// Texture pixel formats the game produces (see PCTex.cpp, gPcTexContainer).
enum PlatTexFormat
{
	PLAT_TEX_RGB565 = 0,
	PLAT_TEX_ARGB1555 = 1,
	PLAT_TEX_ARGB4444 = 2,
	PLAT_TEX_ARGB8888 = 3,
};

struct PlatTexture;

PlatTexture* Plat_TexCreate(i32 width, i32 height, PlatTexFormat format);
// pitch in bytes. Uploads the whole image.
void Plat_TexUpload(PlatTexture*, const void* pixels, i32 pitch);
void Plat_TexDestroy(PlatTexture*);

// Frame
void Plat_GfxBeginScene(u32 clearColorARGB, i32 clearDepth);
void Plat_GfxEndScene(void);
void Plat_GfxFlip(void);

// State. Values follow the D3D7 numbers the game already uses so the
// DXsound.cpp callers stay one to one.
void Plat_GfxSetBlendMode(u32 gameBlendMode);      // 0..5, DXPOLY_SetBlendMode
void Plat_GfxSetDepthTest(i32 enable);             // D3DRENDERSTATE_ZENABLE
void Plat_GfxSetDepthWrite(i32 enable);            // D3DRENDERSTATE_ZWRITEENABLE
void Plat_GfxSetDepthFunc(u32 d3dCmpFunc);         // D3DCMP_* 1..8
void Plat_GfxSetFilter(i32 bilinear);
void Plat_GfxSetAddress(u32 d3dU, u32 d3dV);       // D3DTADDRESS_WRAP=1, CLAMP=3
void Plat_GfxSetTexAlpha(i32 modulateAlpha);       // D3DTSS_ALPHAOP modulate(4)/selectarg1(3)
void Plat_GfxSetTexture(PlatTexture*);             // 0 = untextured
void Plat_GfxSetFog(i32 enable, u32 colorRGB, f32 start, f32 end);

// One pre-transformed triangle fan (x, y in pixels, z in 0..1, rhw, ARGB, u, v).
void Plat_GfxDrawFan(const SDXPolyField* verts, i32 count);

// Reads back the frame as 24 bit BGR rows, top down. Returns 0 if unsupported.
i32 Plat_GfxReadPixels(u8* dstBGR, i32 width, i32 height);

// ----------------------------------------------------------------- input

// Keyboard state indexed by DirectInput DIK_* scancode, 0x80 bit = down,
// same contract as DXINPUT_PollKeyboard / gKeyState.
void Plat_InputPollKeyboard(u8 dikState[256]);
// Escape feeds the original skip action during an in-engine scene.
void Plat_InputMapCutsceneSkip(u8 dikState[256]);
// Relative mouse motion since the last call, 3 buttons (0x80 = down).
void Plat_InputPollMouse(i32* dx, i32* dy, u8 buttons[3]);
// Game controller. Returns 0 if none. Axes are -1000..1000 (the DIPROP_RANGE
// DXINPUT_SetupController asks DirectInput for), pov is a DirectInput POV
// (0xFFFFFFFF centred, else hundredths of degrees).
i32 Plat_InputPollController(i32* x, i32* y, u32* pov, u8 buttons[32], i32* numButtons);
void Plat_InputRumble(i32 on, f32 strength);

// ----------------------------------------------------------------- sound

struct PlatSoundBuffer;    // a loaded static sample (DXSOUND buffer)
struct PlatSoundVoice;     // a playing instance (DXSOUND holder)

i32 Plat_SndInit(void);
void Plat_SndShutdown(void);
// PCM only. channels 1/2, bits 8/16.
PlatSoundBuffer* Plat_SndCreateBuffer(const void* pcm, i32 bytes, i32 rate, i32 channels, i32 bits);
void Plat_SndDestroyBuffer(PlatSoundBuffer*);
PlatSoundVoice* Plat_SndCreateVoice(PlatSoundBuffer*);
void Plat_SndDestroyVoice(PlatSoundVoice*);
void Plat_SndPlay(PlatSoundVoice*, i32 loop);
void Plat_SndStop(PlatSoundVoice*);
i32 Plat_SndIsPlaying(PlatSoundVoice*);
void Plat_SndSetVolume(PlatSoundVoice*, i32 hundredthsDb);  // DirectSound scale, 0 .. -10000
void Plat_SndSetPan(PlatSoundVoice*, i32 hundredthsDb);     // -10000 .. 10000
void Plat_SndSetFrequency(PlatSoundVoice*, i32 hz);
i32 Plat_SndGetFrequency(PlatSoundVoice*);
i32 Plat_SndBufferRate(PlatSoundBuffer*);

// ---------------------------------------------------------------- movies

// Bink files are decoded by the ffmpeg executable. offset/bytes select a
// stored file inside media.pkr; bytes == 0 selects a loose file.
i32 Plat_MovieOpen(const char* path, u32 offset, u32 bytes);
i32 Plat_MovieNextFrame(void);
void Plat_MovieStop(void);
void Plat_MovieSetVolume(i32 volume);  // 0..255

// Backend movie sinks. PCM is stereo S16 at 44100 Hz. Queue size is bytes.
enum PlatMovieAudioOp
{
	PLAT_MOVIE_AUDIO_OPEN,
	PLAT_MOVIE_AUDIO_CLOSE,
	PLAT_MOVIE_AUDIO_QUEUE,
	PLAT_MOVIE_AUDIO_QUEUED,
	PLAT_MOVIE_AUDIO_START,
	PLAT_MOVIE_AUDIO_VOLUME,
};
i32 Plat_MovieAudio(PlatMovieAudioOp op, const void* pcm, i32 value);
void Plat_MovieDrawFrame(const u8* bgra, i32 width, i32 height);

#endif
