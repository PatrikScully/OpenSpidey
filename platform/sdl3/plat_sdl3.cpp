// SDL3 + OpenGL backend for the standalone build.
//
// Rendering: the game hands us pre-transformed screen space triangle fans
// (D3D7 TL vertices: x, y in pixels, z in 0..1, rhw, ARGB, u, v). That maps
// onto fixed function GL with an orthographic projection one to one, so this
// uses immediate mode GL 1.x, the simplest thing that can possibly work.
// Input: SDL keyboard state translated to DirectInput DIK codes, relative
// mouse, first gamepad. Sound: a small software mixer feeding one SDL audio
// stream (volume/pan in DirectSound hundredths of dB, frequency changes by
// linear resampling), which is exactly the DirectSound buffer/duplicate
// model DXsound.cpp expects.
//
// Needs libsdl3-dev:i386 and libgl-dev:i386 (32 bit build).

#include "../plat.h"
#include "audio_settings.h"
#include "../../DXsound.h"   // SDXPolyField
#include "../../PCInput.h"
#include "../../spidey.h"
#include "../../camera.h"
#include "../../ps2m3d.h"
#include "../../front.h"

#include <SDL3/SDL.h>
#include <SDL3/SDL_opengl.h>

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cmath>

// Debugging aids, read once in Plat_GfxInit:
//   SPIDEY_NOCULL=1     no back face culling
//   SPIDEY_NODEPTH=1    depth test always off
//   SPIDEY_GLDEBUG=ms   from that many ms after start, log the GL state and
//                       vertices of the first 60 fans after each of the next
//                       4 clears to stderr
static i32 gDbgNoDepth = 0;
static i32 gDbgLogFrom = -1;
static i32 gDbgClears = 0;
static i32 gDbgSinceClear = 0;
#include <pthread.h>
#include <unistd.h>

// ---------------------------------------------------------------- window

static SDL_Window* gWindow;
static SDL_GLContext gGL;
// Zero means the current context has no anisotropic filtering support.
static GLfloat gTextureAnisotropy;
typedef void (APIENTRY *GenerateMipmapProc)(GLenum);
static GenerateMipmapProc gGenerateMipmap;
static i32 gWidth = 640, gHeight = 480;
static i32 gViewportX, gViewportY, gViewportWidth = 640, gViewportHeight = 480;
static i32 gQuit;

// @Bogus
static i32 VideoSetting(const char* name, i32 fallback, i32 minimum, i32 maximum)
{
	const char* setting = getenv(name);
	if (!setting || !*setting)
		return fallback;
	char* end = 0;
	long value = strtol(setting, &end, 10);
	if (*end || value < minimum || value > maximum)
		return fallback;
	return (i32)value;
}

// @Bogus
static void UpdateViewport(void)
{
	i32 width = 0, height = 0;
	if (!gWindow || !SDL_GetWindowSizeInPixels(gWindow, &width, &height) || width <= 0 || height <= 0)
		return;
	gViewportWidth = width;
	gViewportHeight = height;
	if ((f64)width / height > (f64)gWidth / gHeight)
		gViewportWidth = (i32)((f64)height * gWidth / gHeight);
	else
		gViewportHeight = (i32)((f64)width * gHeight / gWidth);
	if (gViewportWidth < 1) gViewportWidth = 1;
	if (gViewportHeight < 1) gViewportHeight = 1;
	gViewportX = (width - gViewportWidth) / 2;
	gViewportY = (height - gViewportHeight) / 2;
	glViewport(gViewportX, gViewportY, gViewportWidth, gViewportHeight);
}

// @Bogus
i32 Plat_Init(i32 width, i32 height, i32 fullscreen)
{
	if (!SDL_Init(SDL_INIT_VIDEO | SDL_INIT_AUDIO | SDL_INIT_GAMEPAD))
	{
		printf("Plat(sdl3): SDL_Init failed: %s\n", SDL_GetError());
		return 0;
	}

	gWidth = width;
	gHeight = height;
	gQuit = 0;

	SDL_GL_SetAttribute(SDL_GL_DOUBLEBUFFER, 1);
	SDL_GL_SetAttribute(SDL_GL_DEPTH_SIZE, 24);

	SDL_WindowFlags flags = SDL_WINDOW_OPENGL | SDL_WINDOW_RESIZABLE | SDL_WINDOW_HIGH_PIXEL_DENSITY;
	i32 scale = fullscreen ? 1 : VideoSetting("SPIDEY_SCALE", 2, 1, 8);
	i32 outputWidth = VideoSetting("SPIDEY_WIDTH", width * scale, 320, 7680);
	i32 outputHeight = VideoSetting("SPIDEY_HEIGHT", height * scale, 240, 4320);
	const char* windowMode = getenv("SPIDEY_WINDOW_MODE");
	i32 requestedMode = fullscreen ? 1 : 0;
	if (windowMode)
	{
		if (!strcmp(windowMode, "windowed")) requestedMode = 0;
		else if (!strcmp(windowMode, "borderless")) requestedMode = 1;
		else if (!strcmp(windowMode, "fullscreen")) requestedMode = 2;
	}

	// SPIDEY_MSAA sets the requested samples; 0 disables antialiasing.
	i32 samples = VideoSetting("SPIDEY_MSAA", 4, 0, 16);

	for (;;)
	{
		bool attributes = SDL_GL_SetAttribute(SDL_GL_MULTISAMPLEBUFFERS, samples > 0 ? 1 : 0)
			&& SDL_GL_SetAttribute(SDL_GL_MULTISAMPLESAMPLES, samples);
		gWindow = attributes ? SDL_CreateWindow("OpenSpidey", outputWidth, outputHeight, flags) : 0;
		gGL = gWindow ? SDL_GL_CreateContext(gWindow) : 0;
		if (gGL)
			break;

		printf("Plat(sdl3): OpenGL initialization failed with %d samples: %s\n", samples, SDL_GetError());
		if (gWindow)
			SDL_DestroyWindow(gWindow);
		gWindow = 0;
		if (!samples)
		{
			SDL_Quit();
			return 0;
		}
		samples = 0;
	}
	if (!requestedMode)
	{
		f32 density = SDL_GetWindowPixelDensity(gWindow);
		if (density > 0.0f && density != 1.0f)
		{
			SDL_SetWindowSize(gWindow, (i32)(outputWidth / density), (i32)(outputHeight / density));
			SDL_SyncWindow(gWindow);
		}
	}
	if (requestedMode)
	{
		SDL_DisplayMode mode;
		if (requestedMode == 2)
		{
			if (!SDL_GetClosestFullscreenDisplayMode(SDL_GetDisplayForWindow(gWindow),
				outputWidth, outputHeight, 0.0f, true, &mode)
				|| mode.w != outputWidth || mode.h != outputHeight
				|| !SDL_SetWindowFullscreenMode(gWindow, &mode))
			{
				printf("Plat(sdl3): fullscreen mode %dx%d unavailable, using borderless\n", outputWidth, outputHeight);
				requestedMode = 1;
			}
		}
		if (requestedMode == 1)
			SDL_SetWindowFullscreenMode(gWindow, 0);
		if (!SDL_SetWindowFullscreen(gWindow, true))
			printf("Plat(sdl3): fullscreen failed, using windowed: %s\n", SDL_GetError());
		SDL_SyncWindow(gWindow);
	}
	SDL_GL_MakeCurrent(gWindow, gGL);
	// GLX updates resized drawables at the first swap.
	glClearColor(0, 0, 0, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	SDL_GL_SwapWindow(gWindow);
	i32 vsync = VideoSetting("SPIDEY_VSYNC", 1, 0, 1);
	if (!SDL_GL_SetSwapInterval(vsync))
		printf("Plat(sdl3): requested vsync %d unavailable: %s\n", vsync, SDL_GetError());

	GLint sampleBuffers = 0, actualSamples = 0;
	glGetIntegerv(GL_SAMPLE_BUFFERS, &sampleBuffers);
	glGetIntegerv(GL_SAMPLES, &actualSamples);
	if (sampleBuffers && actualSamples > 1)
		glEnable(GL_MULTISAMPLE);
	else
		glDisable(GL_MULTISAMPLE);

	printf("Plat(sdl3): %dx%d, GL %s / %s, MSAA %d\n", width, height,
			(const char*)glGetString(GL_VERSION), (const char*)glGetString(GL_RENDERER),
			sampleBuffers ? actualSamples : 0);
	i32 drawableWidth = 0, drawableHeight = 0, actualVsync = 0;
	SDL_GetWindowSizeInPixels(gWindow, &drawableWidth, &drawableHeight);
	SDL_GL_GetSwapInterval(&actualVsync);
	const char* actualMode = (SDL_GetWindowFlags(gWindow) & SDL_WINDOW_FULLSCREEN)
		? (SDL_GetWindowFullscreenMode(gWindow) ? "fullscreen" : "borderless") : "windowed";
	printf("Plat(sdl3): output %dx%d, %s, vsync %d\n", drawableWidth, drawableHeight, actualMode, actualVsync);

	gTextureAnisotropy = 0.0f;
	i32 major = 0, minor = 0;
	sscanf((const char*)glGetString(GL_VERSION), "%d.%d", &major, &minor);
	if (major > 4 || (major == 4 && minor >= 6)
		|| SDL_GL_ExtensionSupported("GL_ARB_texture_filter_anisotropic")
		|| SDL_GL_ExtensionSupported("GL_EXT_texture_filter_anisotropic"))
	{
		GLfloat maximum = 1.0f;
		glGetFloatv(0x84FF, &maximum); // GL_MAX_TEXTURE_MAX_ANISOTROPY
		const char* setting = getenv("SPIDEY_ANISOTROPY");
		GLfloat requested = setting ? (GLfloat)strtod(setting, 0) : 8.0f;
		if (!(requested >= 1.0f))
			requested = 1.0f;
		if (requested > maximum)
			requested = maximum;
		gTextureAnisotropy = requested;
	}
	printf("Plat(sdl3): texture anisotropy %.1fx%s\n", gTextureAnisotropy ? gTextureAnisotropy : 1.0f,
		gTextureAnisotropy ? "" : " (unsupported)");
	gGenerateMipmap = 0;
	const char* mipmaps = getenv("SPIDEY_MIPMAPS");
	if (!mipmaps || atoi(mipmaps))
	{
		if (major >= 3 || SDL_GL_ExtensionSupported("GL_ARB_framebuffer_object"))
			gGenerateMipmap = (GenerateMipmapProc)SDL_GL_GetProcAddress("glGenerateMipmap");
		else if (SDL_GL_ExtensionSupported("GL_EXT_framebuffer_object"))
			gGenerateMipmap = (GenerateMipmapProc)SDL_GL_GetProcAddress("glGenerateMipmapEXT");
	}
	printf("Plat(sdl3): repeating world mipmaps %s\n", gGenerateMipmap ? "enabled" : "off");

	UpdateViewport();
	glMatrixMode(GL_PROJECTION);
	glLoadIdentity();
	// D3D7 TL vertices address pixel centres, GL addresses pixel corners:
	// the half pixel shift keeps textured 2D quads crisp.
	glOrtho(-0.5, width - 0.5, height - 0.5, -0.5, 0.0, 1.0);
	glMatrixMode(GL_MODELVIEW);
	glLoadIdentity();
	// glOrtho's near..far is along -z in eye space; the game's z (0 near ..
	// 1 far) is positive, so mirror it. Screen winding is unaffected.
	glScalef(1.0f, 1.0f, -1.0f);

	glDisable(GL_LIGHTING);
	glDisable(GL_DITHER);
	glShadeModel(GL_SMOOTH);
	glEnable(GL_TEXTURE_2D);
	glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_MODULATE);
	glDepthFunc(GL_LEQUAL);
	glEnable(GL_DEPTH_TEST);
	// D3D7 does not clip pretransformed (XYZRHW) vertices on z, it clamps
	// the depth. GL would drop every triangle that reaches past the far
	// plane, so clamp instead of clip.
#ifdef GL_DEPTH_CLAMP
	glEnable(GL_DEPTH_CLAMP);
#else
	glEnable(0x864F);
#endif
	// D3D7 CULL_CCW keeps polygons that are clockwise on a y-down screen.
	// After the ortho y flip those are clockwise in GL window space too, so
	// clockwise is the front face here.
	glFrontFace(getenv("SPIDEY_FRONTCCW") ? GL_CCW : GL_CW);   // SPIDEY_FRONTCCW=1: test the other winding
	glCullFace(GL_BACK);
	glEnable(GL_CULL_FACE);
	if (getenv("SPIDEY_NOCULL"))   // debugging aid
		glDisable(GL_CULL_FACE);
	gDbgNoDepth = getenv("SPIDEY_NODEPTH") ? 1 : 0;
	if (gDbgNoDepth)
		glDisable(GL_DEPTH_TEST);
	gDbgLogFrom = getenv("SPIDEY_GLDEBUG") ? atoi(getenv("SPIDEY_GLDEBUG")) : -1;
	glEnable(GL_ALPHA_TEST);
	glAlphaFunc(GL_GEQUAL, 8.0f / 255.0f);   // ALPHAREF 8 / GREATEREQUAL, ALPHATESTENABLE is off by
	glDisable(GL_ALPHA_TEST);                 // default in DXPOLY_Init, matched below

	SDL_SetWindowRelativeMouseMode(gWindow, true);
	return 1;
}

void Plat_Shutdown(void)
{
	Plat_MusicStop();
	Plat_MovieStop();
	if (gGL)
		SDL_GL_DestroyContext(gGL);
	if (gWindow)
		SDL_DestroyWindow(gWindow);
	gGL = 0;
	gWindow = 0;
	SDL_Quit();
}

// @Bogus
i32 Plat_Yield(void)
{
	SDL_Event e;
	while (SDL_PollEvent(&e))
	{
		if (e.type == SDL_EVENT_QUIT)
			gQuit = 1;
		if (e.type == SDL_EVENT_KEY_DOWN && e.key.scancode == SDL_SCANCODE_F12)
			gQuit = 1;
		if (e.type == SDL_EVENT_KEY_DOWN && !e.key.repeat && e.key.scancode == SDL_SCANCODE_F1)
			SDL_SetWindowRelativeMouseMode(gWindow, !SDL_GetWindowRelativeMouseMode(gWindow));
		if (e.type == SDL_EVENT_WINDOW_PIXEL_SIZE_CHANGED)
			UpdateViewport();
	}
	return !gQuit;
}

u32 Plat_Ticks(void)
{
	return (u32)SDL_GetTicks();
}

void Plat_Sleep(u32 ms)
{
	SDL_Delay(ms);
}

// ----------------------------------------------------------------- timer

struct STimer
{
	pthread_t thread;
	u32 periodMs;
	PlatTimerFn fn;
	void* user;
	volatile i32 stop;
};

static STimer gTimers[4];

static void* timerThread(void* arg)
{
	STimer* t = (STimer*)arg;
	while (!t->stop)
	{
		usleep(t->periodMs * 1000);
		if (!t->stop)
			t->fn(t->user);
	}
	return 0;
}

u32 Plat_TimerStart(u32 periodMs, PlatTimerFn fn, void* user)
{
	for (i32 i = 0; i < 4; i++)
	{
		if (!gTimers[i].fn)
		{
			gTimers[i].periodMs = periodMs ? periodMs : 1;
			gTimers[i].fn = fn;
			gTimers[i].user = user;
			gTimers[i].stop = 0;
			pthread_create(&gTimers[i].thread, 0, timerThread, &gTimers[i]);
			return i + 1;
		}
	}
	return 0;
}

void Plat_TimerStop(u32 handle)
{
	if (handle < 1 || handle > 4 || !gTimers[handle - 1].fn)
		return;
	STimer* t = &gTimers[handle - 1];
	t->stop = 1;
	pthread_join(t->thread, 0);
	t->fn = 0;
}

// -------------------------------------------------------------- graphics

struct PlatTexture
{
	GLuint id;
	i32 width, height;
	PlatTexFormat format;
	i32 filter;     // last applied, -1 = none
	i32 wrapU, wrapV;
	GLfloat anisotropy;
	bool mipmapsReady;
};

static PlatTexture* gBoundTex;
static i32 gFilter = 1;
static u32 gAddrU = 3, gAddrV = 3;
static i32 gTexAlphaModulate = 1;

PlatTexture* Plat_TexCreate(i32 width, i32 height, PlatTexFormat format)
{
	PlatTexture* t = new PlatTexture;
	memset(t, 0, sizeof(*t));
	t->width = width;
	t->height = height;
	t->format = format;
	t->filter = -1;
	t->wrapU = t->wrapV = -1;
	glGenTextures(1, &t->id);
	return t;
}

// @Bogus
void Plat_TexUpload(PlatTexture* t, const void* pixels, i32 pitch)
{
	GLenum fmt, type;
	i32 bpp;
	switch (t->format)
	{
		case PLAT_TEX_RGB565:   fmt = GL_RGB;  type = GL_UNSIGNED_SHORT_5_6_5;        bpp = 2; break;
		case PLAT_TEX_ARGB1555: fmt = GL_BGRA; type = GL_UNSIGNED_SHORT_1_5_5_5_REV;  bpp = 2; break;
		case PLAT_TEX_ARGB4444: fmt = GL_BGRA; type = GL_UNSIGNED_SHORT_4_4_4_4_REV;  bpp = 2; break;
		default:                fmt = GL_BGRA; type = GL_UNSIGNED_INT_8_8_8_8_REV;    bpp = 4; break;
	}

	// SPIDEY_DUMPTEX=<dir>: write every uploaded texture as texNNNN_WxH_fF.ppm
	// (RGB, alpha 0 pixels painted green) so the decode path can be checked
	static const char* dumpDir = getenv("SPIDEY_DUMPTEX");
	if (dumpDir)
	{
		static i32 dumpIndex = 0;
		char name[512];
		snprintf(name, sizeof(name), "%s/tex%04d_%dx%d_f%d.ppm", dumpDir, dumpIndex++, t->width, t->height, t->format);
		FILE* f = fopen(name, "wb");
		if (f)
		{
			fprintf(f, "P6\n%d %d\n255\n", t->width, t->height);
			for (i32 y = 0; y < t->height; y++)
			{
				const u8* row = (const u8*)pixels + y * pitch;
				for (i32 x = 0; x < t->width; x++)
				{
					u32 r, g, b, a = 255;
					if (bpp == 4)
					{
						u32 p = ((const u32*)row)[x];
						a = p >> 24; r = (p >> 16) & 0xFF; g = (p >> 8) & 0xFF; b = p & 0xFF;
					}
					else
					{
						u32 p = ((const u16*)row)[x];
						switch (t->format)
						{
							case PLAT_TEX_RGB565:
								r = (p >> 11) * 255 / 31; g = ((p >> 5) & 0x3F) * 255 / 63; b = (p & 0x1F) * 255 / 31; break;
							case PLAT_TEX_ARGB1555:
								a = (p >> 15) * 255; r = ((p >> 10) & 0x1F) * 255 / 31; g = ((p >> 5) & 0x1F) * 255 / 31; b = (p & 0x1F) * 255 / 31; break;
							default:
								a = (p >> 12) * 17; r = ((p >> 8) & 0xF) * 17; g = ((p >> 4) & 0xF) * 17; b = (p & 0xF) * 17; break;
						}
					}
					if (a == 0) { r = 0; g = 255; b = 0; }
					u8 rgb[3] = { (u8)r, (u8)g, (u8)b };
					fwrite(rgb, 1, 3, f);
				}
			}
			fclose(f);
		}
	}

	glBindTexture(GL_TEXTURE_2D, t->id);
	glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
	glPixelStorei(GL_UNPACK_ROW_LENGTH, pitch / bpp);
	glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, t->width, t->height, 0, fmt, type, pixels);
	glPixelStorei(GL_UNPACK_ROW_LENGTH, 0);
	t->filter = -1;
	t->wrapU = t->wrapV = -1;
	t->mipmapsReady = false;
	if (gBoundTex)
		glBindTexture(GL_TEXTURE_2D, gBoundTex->id);
}

void Plat_TexDestroy(PlatTexture* t)
{
	if (!t)
		return;
	if (gBoundTex == t)
		gBoundTex = 0;
	glDeleteTextures(1, &t->id);
	delete t;
}

// @Bogus
void Plat_GfxBeginScene(u32 clearColorARGB, i32 clearDepth)
{
	UpdateViewport();
	GLboolean scissor = glIsEnabled(GL_SCISSOR_TEST);
	GLint scissorBox[4];
	glGetIntegerv(GL_SCISSOR_BOX, scissorBox);
	glDisable(GL_SCISSOR_TEST);
	glClearColor(0, 0, 0, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	glEnable(GL_SCISSOR_TEST);
	glScissor(gViewportX, gViewportY, gViewportWidth, gViewportHeight);
	gDbgSinceClear = 0;
	if (gDbgLogFrom >= 0 && (i32)SDL_GetTicks() >= gDbgLogFrom && gDbgClears < 4)
	{
		gDbgClears++;
		fprintf(stderr, "GLCLEAR #%d t=%u color=%08x depth=%d\n", gDbgClears, (u32)SDL_GetTicks(), clearColorARGB, clearDepth);
	}
	glClearColor(((clearColorARGB >> 16) & 0xFF) / 255.0f,
			((clearColorARGB >> 8) & 0xFF) / 255.0f,
			(clearColorARGB & 0xFF) / 255.0f, 1.0f);
	glClearDepth(1.0);
	// D3D's Clear ignores ZWRITEENABLE, GL's glClear honours glDepthMask. The
	// game often ends a frame with depth writes off (alpha polys), which left
	// the depth buffer uncleared here and every opaque poly of the next frame
	// failed the LEQUAL test against stale depth (the level rendered as the
	// bare sky colour, 2026-09-03).
	GLboolean depthMask = GL_TRUE;
	glGetBooleanv(GL_DEPTH_WRITEMASK, &depthMask);
	glDepthMask(GL_TRUE);
	glClear(GL_COLOR_BUFFER_BIT | (clearDepth ? GL_DEPTH_BUFFER_BIT : 0));
	glDepthMask(depthMask);
	glScissor(scissorBox[0], scissorBox[1], scissorBox[2], scissorBox[3]);
	if (!scissor)
		glDisable(GL_SCISSOR_TEST);
}

void Plat_GfxEndScene(void)
{
}

void Plat_GfxFlip(void)
{
	SDL_GL_SwapWindow(gWindow);
}

// DXPOLY_SetBlendMode's cases, D3DBLEND numbers in the comments
void Plat_GfxSetBlendMode(u32 mode)
{
	switch (mode)
	{
		case 3:   // ZERO(1), INVSRCCOLOR(4): darken
			glEnable(GL_BLEND);
			glBlendFunc(GL_ZERO, GL_ONE_MINUS_SRC_COLOR);
			break;
		case 1:
		case 5:   // SRCALPHA(5), INVSRCALPHA(6)
			glEnable(GL_BLEND);
			glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
			break;
		case 2:
		case 4:   // SRCALPHA(5), ONE(2): additive
			glEnable(GL_BLEND);
			glBlendFunc(GL_SRC_ALPHA, GL_ONE);
			break;
		default:  // ONE, ZERO, blend off
			glDisable(GL_BLEND);
			break;
	}
}

void Plat_GfxSetDepthTest(i32 enable)
{
	if (gDbgNoDepth)
		enable = 0;
	if (enable)
		glEnable(GL_DEPTH_TEST);
	else
		glDisable(GL_DEPTH_TEST);
}

void Plat_GfxSetDepthWrite(i32 enable)
{
	glDepthMask(enable ? GL_TRUE : GL_FALSE);
}

void Plat_GfxSetDepthFunc(u32 d3dCmp)
{
	static const GLenum table[9] = { GL_LEQUAL, GL_NEVER, GL_LESS, GL_EQUAL, GL_LEQUAL,
		GL_GREATER, GL_NOTEQUAL, GL_GEQUAL, GL_ALWAYS };
	glDepthFunc(table[d3dCmp < 9 ? d3dCmp : 0]);
}

void Plat_GfxSetFilter(i32 bilinear)
{
	gFilter = bilinear ? 1 : 0;
}

void Plat_GfxSetAddress(u32 u, u32 v)
{
	gAddrU = u;
	gAddrV = v;
}

void Plat_GfxSetTexAlpha(i32 modulate)
{
	gTexAlphaModulate = modulate;
}

void Plat_GfxSetTexture(PlatTexture* t)
{
	gBoundTex = t;
	if (t)
	{
		glEnable(GL_TEXTURE_2D);
		glBindTexture(GL_TEXTURE_2D, t->id);
	}
	else
	{
		glDisable(GL_TEXTURE_2D);
	}
}

void Plat_GfxSetFog(i32, u32, f32, f32)
{
	// PCGfx does its fog in software (colour tables), nothing to do here
}

static void applyTextureState(void)
{
	PlatTexture* t = gBoundTex;
	if (!t)
		return;
	if (t->filter != gFilter)
	{
		GLint f = gFilter ? GL_LINEAR : GL_NEAREST;
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, f);
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, f);
		t->filter = gFilter;
	}
	if (t->wrapU != (i32)gAddrU)
	{
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, gAddrU == 1 ? GL_REPEAT : GL_CLAMP_TO_EDGE);
		t->wrapU = gAddrU;
	}
	if (t->wrapV != (i32)gAddrV)
	{
		glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, gAddrV == 1 ? GL_REPEAT : GL_CLAMP_TO_EDGE);
		t->wrapV = gAddrV;
	}

	// COLOROP MODULATE always; ALPHAOP MODULATE (texture * diffuse) or
	// SELECTARG1 (texture alpha only)
	glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_COMBINE);
	glTexEnvi(GL_TEXTURE_ENV, GL_COMBINE_RGB, GL_MODULATE);
	glTexEnvi(GL_TEXTURE_ENV, GL_SOURCE0_RGB, GL_TEXTURE);
	glTexEnvi(GL_TEXTURE_ENV, GL_SOURCE1_RGB, GL_PRIMARY_COLOR);
	glTexEnvi(GL_TEXTURE_ENV, GL_COMBINE_ALPHA, gTexAlphaModulate ? GL_MODULATE : GL_REPLACE);
	glTexEnvi(GL_TEXTURE_ENV, GL_SOURCE0_ALPHA, GL_TEXTURE);
	glTexEnvi(GL_TEXTURE_ENV, GL_SOURCE1_ALPHA, GL_PRIMARY_COLOR);
}

// @Bogus
void Plat_GfxDrawFan(const SDXPolyField* v, i32 count)
{
	applyTextureState();
	bool tiled = false;
	if (count > 0 && gBoundTex && gGenerateMipmap && gFilter && gAddrU == 1 && gAddrV == 1
		&& glIsEnabled(GL_DEPTH_TEST) && !glIsEnabled(GL_BLEND) && !glIsEnabled(GL_ALPHA_TEST))
	{
		// Atlas UVs can carry whole-period offsets. Only a span larger than a
		// complete texture proves repetition, with tolerance for texel offsets.
		f32 paddingU = 1.0f / gBoundTex->width, paddingV = 1.0f / gBoundTex->height;
		f32 minU = v[0].field_14, maxU = minU, minV = v[0].field_18, maxV = minV;
		for (i32 i = 1; i < count; i++)
		{
			if (v[i].field_14 < minU) minU = v[i].field_14;
			if (v[i].field_14 > maxU) maxU = v[i].field_14;
			if (v[i].field_18 < minV) minV = v[i].field_18;
			if (v[i].field_18 > maxV) maxV = v[i].field_18;
		}
		tiled = maxU - minU > 1.0f + paddingU || maxV - minV > 1.0f + paddingV;
		if (tiled)
		{
			if (!gBoundTex->mipmapsReady)
			{
				gGenerateMipmap(GL_TEXTURE_2D);
				gBoundTex->mipmapsReady = true;
			}
			glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR);
			gBoundTex->filter = -1; // Restore the original filter for the next fan.
		}
	}
	if (gBoundTex && gTextureAnisotropy)
	{
		GLfloat anisotropy = tiled ? gTextureAnisotropy : 1.0f;
		if (gBoundTex->anisotropy != anisotropy)
		{
			glTexParameterf(GL_TEXTURE_2D, 0x84FE, anisotropy); // GL_TEXTURE_MAX_ANISOTROPY
			gBoundTex->anisotropy = anisotropy;
		}
	}
	gDbgSinceClear++;
	if (gDbgLogFrom >= 0 && gDbgClears > 0 && gDbgClears <= 4 && (i32)SDL_GetTicks() >= gDbgLogFrom && gDbgSinceClear <= (getenv("SPIDEY_GLDEBUG_N") ? atoi(getenv("SPIDEY_GLDEBUG_N")) : 60))
	{
		GLint tex = 0, dfunc = 0, dmask = 0;
		glGetIntegerv(GL_TEXTURE_BINDING_2D, &tex);
		glGetIntegerv(GL_DEPTH_FUNC, &dfunc);
		glGetIntegerv(GL_DEPTH_WRITEMASK, &dmask);
		extern i32 gDbgFanBlend, gDbgFanTex, gDbgFanBucket;
		fprintf(stderr, "GLFAN #%d n=%d tex=%d cull=%d depth=%d/%d mask=%d alpha=%d blend=%d poly(blend=%d tex=%p bucket=%d) v0=(%.1f,%.1f,%.4f c=%08x) v1=(%.1f,%.1f,%.4f) v2=(%.1f,%.1f,%.4f)\n",
			gDbgSinceClear, count, tex, glIsEnabled(GL_CULL_FACE), glIsEnabled(GL_DEPTH_TEST), dfunc, dmask,
			glIsEnabled(GL_ALPHA_TEST), glIsEnabled(GL_BLEND), gDbgFanBlend, (void*)(size_t)gDbgFanTex, gDbgFanBucket,
			v[0].field_0, v[0].field_4, v[0].field_8, v[0].field_10, v[1].field_0, v[1].field_4, v[1].field_8, v[2].field_0, v[2].field_4, v[2].field_8);
	}

	glBegin(GL_TRIANGLE_FAN);
	for (i32 i = 0; i < count; i++)
	{
		u32 c = v[i].field_10;
		glColor4ub((c >> 16) & 0xFF, (c >> 8) & 0xFF, c & 0xFF, (c >> 24) & 0xFF);
		// D3D XYZRHW vertices carry reciprocal depth at offset 0xC.
		// Projective texture coordinates preserve it with our screen-space vertices.
		f32 rhw = v[i].field_C;
		glTexCoord4f(v[i].field_14 * rhw, v[i].field_18 * rhw, 0.0f, rhw);
		glVertex3f(v[i].field_0, v[i].field_4, v[i].field_8);
	}
	glEnd();
}

// @Bogus
i32 Plat_GfxReadPixels(u8* dst, i32 width, i32 height)
{
	if (!dst || width != gWidth || height != gHeight)
		return 0;
	glPixelStorei(GL_PACK_ALIGNMENT, 1);
	if (gViewportWidth == width && gViewportHeight == height)
	{
		glReadPixels(gViewportX, gViewportY, width, height, GL_BGR, GL_UNSIGNED_BYTE, dst);
	}
	else
	{
		i32 fw = gViewportWidth, fh = gViewportHeight;
		u8* big = (u8*)malloc(fw * fh * 3);
		if (!big)
			return 0;
		glReadPixels(gViewportX, gViewportY, fw, fh, GL_BGR, GL_UNSIGNED_BYTE, big);
		for (i32 y = 0; y < height; y++)
			for (i32 x = 0; x < width; x++)
				memcpy(dst + (y * width + x) * 3, big + ((y * fh / height) * fw + x * fw / width) * 3, 3);
		free(big);
	}
	// GL rows are bottom up, the caller wants top down
	i32 rowBytes = width * 3;
	u8* tmp = (u8*)malloc(rowBytes);
	if (!tmp)
		return 0;
	for (i32 y = 0; y < height / 2; y++)
	{
		memcpy(tmp, dst + y * rowBytes, rowBytes);
		memcpy(dst + y * rowBytes, dst + (height - 1 - y) * rowBytes, rowBytes);
		memcpy(dst + (height - 1 - y) * rowBytes, tmp, rowBytes);
	}
	free(tmp);
	return 1;
}

// ----------------------------------------------------------------- input

// SDL scancode -> DirectInput DIK_* (PC/AT set 1 make codes)
static u8 gDikTable[SDL_SCANCODE_COUNT];

static void buildDikTable(void)
{
	static i32 built;
	if (built)
		return;
	built = 1;
	struct { SDL_Scancode sc; u8 dik; } map[] = {
		{ SDL_SCANCODE_ESCAPE, 0x01 }, { SDL_SCANCODE_1, 0x02 }, { SDL_SCANCODE_2, 0x03 },
		{ SDL_SCANCODE_3, 0x04 }, { SDL_SCANCODE_4, 0x05 }, { SDL_SCANCODE_5, 0x06 },
		{ SDL_SCANCODE_6, 0x07 }, { SDL_SCANCODE_7, 0x08 }, { SDL_SCANCODE_8, 0x09 },
		{ SDL_SCANCODE_9, 0x0A }, { SDL_SCANCODE_0, 0x0B }, { SDL_SCANCODE_MINUS, 0x0C },
		{ SDL_SCANCODE_EQUALS, 0x0D }, { SDL_SCANCODE_BACKSPACE, 0x0E }, { SDL_SCANCODE_TAB, 0x0F },
		{ SDL_SCANCODE_Q, 0x10 }, { SDL_SCANCODE_W, 0x11 }, { SDL_SCANCODE_E, 0x12 },
		{ SDL_SCANCODE_R, 0x13 }, { SDL_SCANCODE_T, 0x14 }, { SDL_SCANCODE_Y, 0x15 },
		{ SDL_SCANCODE_U, 0x16 }, { SDL_SCANCODE_I, 0x17 }, { SDL_SCANCODE_O, 0x18 },
		{ SDL_SCANCODE_P, 0x19 }, { SDL_SCANCODE_LEFTBRACKET, 0x1A }, { SDL_SCANCODE_RIGHTBRACKET, 0x1B },
		{ SDL_SCANCODE_RETURN, 0x1C }, { SDL_SCANCODE_LCTRL, 0x1D }, { SDL_SCANCODE_A, 0x1E },
		{ SDL_SCANCODE_S, 0x1F }, { SDL_SCANCODE_D, 0x20 }, { SDL_SCANCODE_F, 0x21 },
		{ SDL_SCANCODE_G, 0x22 }, { SDL_SCANCODE_H, 0x23 }, { SDL_SCANCODE_J, 0x24 },
		{ SDL_SCANCODE_K, 0x25 }, { SDL_SCANCODE_L, 0x26 }, { SDL_SCANCODE_SEMICOLON, 0x27 },
		{ SDL_SCANCODE_APOSTROPHE, 0x28 }, { SDL_SCANCODE_GRAVE, 0x29 }, { SDL_SCANCODE_LSHIFT, 0x2A },
		{ SDL_SCANCODE_BACKSLASH, 0x2B }, { SDL_SCANCODE_Z, 0x2C }, { SDL_SCANCODE_X, 0x2D },
		{ SDL_SCANCODE_C, 0x2E }, { SDL_SCANCODE_V, 0x2F }, { SDL_SCANCODE_B, 0x30 },
		{ SDL_SCANCODE_N, 0x31 }, { SDL_SCANCODE_M, 0x32 }, { SDL_SCANCODE_COMMA, 0x33 },
		{ SDL_SCANCODE_PERIOD, 0x34 }, { SDL_SCANCODE_SLASH, 0x35 }, { SDL_SCANCODE_RSHIFT, 0x36 },
		{ SDL_SCANCODE_KP_MULTIPLY, 0x37 }, { SDL_SCANCODE_LALT, 0x38 }, { SDL_SCANCODE_SPACE, 0x39 },
		{ SDL_SCANCODE_CAPSLOCK, 0x3A }, { SDL_SCANCODE_F1, 0x3B }, { SDL_SCANCODE_F2, 0x3C },
		{ SDL_SCANCODE_F3, 0x3D }, { SDL_SCANCODE_F4, 0x3E }, { SDL_SCANCODE_F5, 0x3F },
		{ SDL_SCANCODE_F6, 0x40 }, { SDL_SCANCODE_F7, 0x41 }, { SDL_SCANCODE_F8, 0x42 },
		{ SDL_SCANCODE_F9, 0x43 }, { SDL_SCANCODE_F10, 0x44 }, { SDL_SCANCODE_NUMLOCKCLEAR, 0x45 },
		{ SDL_SCANCODE_SCROLLLOCK, 0x46 }, { SDL_SCANCODE_KP_7, 0x47 }, { SDL_SCANCODE_KP_8, 0x48 },
		{ SDL_SCANCODE_KP_9, 0x49 }, { SDL_SCANCODE_KP_MINUS, 0x4A }, { SDL_SCANCODE_KP_4, 0x4B },
		{ SDL_SCANCODE_KP_5, 0x4C }, { SDL_SCANCODE_KP_6, 0x4D }, { SDL_SCANCODE_KP_PLUS, 0x4E },
		{ SDL_SCANCODE_KP_1, 0x4F }, { SDL_SCANCODE_KP_2, 0x50 }, { SDL_SCANCODE_KP_3, 0x51 },
		{ SDL_SCANCODE_KP_0, 0x52 }, { SDL_SCANCODE_KP_PERIOD, 0x53 }, { SDL_SCANCODE_F11, 0x57 },
		{ SDL_SCANCODE_F12, 0x58 }, { SDL_SCANCODE_KP_ENTER, 0x9C }, { SDL_SCANCODE_RCTRL, 0x9D },
		{ SDL_SCANCODE_KP_DIVIDE, 0xB5 }, { SDL_SCANCODE_RALT, 0xB8 }, { SDL_SCANCODE_HOME, 0xC7 },
		{ SDL_SCANCODE_UP, 0xC8 }, { SDL_SCANCODE_PAGEUP, 0xC9 }, { SDL_SCANCODE_LEFT, 0xCB },
		{ SDL_SCANCODE_RIGHT, 0xCD }, { SDL_SCANCODE_END, 0xCF }, { SDL_SCANCODE_DOWN, 0xD0 },
		{ SDL_SCANCODE_PAGEDOWN, 0xD1 }, { SDL_SCANCODE_INSERT, 0xD2 }, { SDL_SCANCODE_DELETE, 0xD3 },
	};
	for (u32 i = 0; i < sizeof(map) / sizeof(map[0]); i++)
		gDikTable[map[i].sc] = map[i].dik;
}

// SPIDEY_KEYS="ms:enter,ms:down,..." scripted presses (same syntax as the
// null backend, delays are relative to the previous key), merged with the
// real keyboard so automated runs can drive the menus.
struct SScriptKey { u32 atMs; u32 holdMs; u8 dik; };
static SScriptKey gScript[64];
static i32 gScriptCount = -1, gScriptNext;
static u32 gKeyDownUntil;
static u8 gScriptKeyDown;

static void parseScript(void)
{
	gScriptCount = 0;
	const char* s = getenv("SPIDEY_KEYS");
	if (!s)
		return;
	char buf[512];
	strncpy(buf, s, sizeof(buf) - 1);
	buf[sizeof(buf) - 1] = 0;
	u32 t = 0;
	for (char* tok = strtok(buf, ","); tok && gScriptCount < 64; tok = strtok(0, ","))
	{
		char* colon = strchr(tok, ':');
		if (!colon)
			continue;
		*colon = 0;
		t += (u32)atoi(tok);
		const char* name = colon + 1;
		u32 hold = 120;
		char* colon2 = strchr(name, ':');
		if (colon2)
		{
			*colon2 = 0;
			hold = (u32)atoi(colon2 + 1);
		}
		u8 dik;
		if (!strcmp(name, "enter")) dik = 0x1C;
		else if (!strcmp(name, "esc")) dik = 0x01;
		else if (!strcmp(name, "up")) dik = 0xC8;
		else if (!strcmp(name, "down")) dik = 0xD0;
		else if (!strcmp(name, "left")) dik = 0xCB;
		else if (!strcmp(name, "right")) dik = 0xCD;
		else if (!strcmp(name, "space")) dik = 0x39;
		else dik = (u8)strtol(name, 0, 0);
		gScript[gScriptCount].atMs = t;
		gScript[gScriptCount].dik = dik;
		gScript[gScriptCount].holdMs = hold;
		gScriptCount++;
	}
}

void Plat_InputPollKeyboard(u8 dikState[256])
{
	buildDikTable();
	memset(dikState, 0, 256);
	int numKeys = 0;
	const bool* keys = SDL_GetKeyboardState(&numKeys);
	for (int sc = 0; sc < numKeys && sc < SDL_SCANCODE_COUNT; sc++)
	{
		if (keys[sc] && gDikTable[sc])
			dikState[gDikTable[sc]] = 0x80;
	}

	// Modern bindings feed the current action map, so remapped actions work too.
	const char* modern = getenv("SPIDEY_MODERN_CONTROLS");
	if ((!modern || atoi(modern)) && G_MECHLIST_PLAYER &&
		!G_MECHLIST_PLAYER->field_1AC && !gWideScreen && !gPausedMenu &&
		SDL_GetKeyboardFocus() == gWindow)
	{
		const SDL_Scancode source[] = { SDL_SCANCODE_W, SDL_SCANCODE_S,
			SDL_SCANCODE_A, SDL_SCANCODE_D, SDL_SCANCODE_SPACE, SDL_SCANCODE_F,
			SDL_SCANCODE_Q, SDL_SCANCODE_E, SDL_SCANCODE_LSHIFT };
		const u32 action[] = { 1, 2, 4, 8, 0x10, 0x20, 0x100, 0x200, 0x400 };
		for (i32 j = 0; j < 9; ++j)
		{
			u32 key;
			PCINPUT_GetKeyboardMappingForAction(action[j], &key);
			if (keys[source[j]] && key < 256)
				dikState[key] = 0x80;
		}
		SDL_MouseButtonFlags mouse = SDL_GetMouseState(0, 0);
		u32 punch, web;
		PCINPUT_GetKeyboardMappingForAction(0x40, &punch);
		PCINPUT_GetKeyboardMappingForAction(0x80, &web);
		if ((mouse & SDL_BUTTON_LMASK) && punch < 256) dikState[punch] = 0x80;
		if ((mouse & SDL_BUTTON_RMASK) && web < 256) dikState[web] = 0x80;
	}

	if (gScriptCount < 0)
		parseScript();
	u32 now = (u32)SDL_GetTicks();
	if (gScriptKeyDown && now < gKeyDownUntil)
	{
		dikState[gScriptKeyDown] = 0x80;
		Plat_InputMapCutsceneSkip(dikState);
		return;
	}
	gScriptKeyDown = 0;
	if (gScriptNext < gScriptCount && now >= gScript[gScriptNext].atMs)
	{
		gScriptKeyDown = gScript[gScriptNext].dik;
		gKeyDownUntil = now + gScript[gScriptNext].holdMs;
		gScriptNext++;
		dikState[gScriptKeyDown] = 0x80;
	}
	Plat_InputMapCutsceneSkip(dikState);
}

void Plat_InputPollMouse(i32* dx, i32* dy, u8 buttons[3])
{
	float fx = 0.0f, fy = 0.0f;
	SDL_MouseButtonFlags b = SDL_GetRelativeMouseState(&fx, &fy);
	const char* modern = getenv("SPIDEY_MODERN_CONTROLS");
	CCamera* camera = G_CAMERA_LIST;
	if ((!modern || atoi(modern)) && camera && G_MECHLIST_PLAYER &&
		!G_MECHLIST_PLAYER->field_1AC && !gWideScreen && !gPausedMenu &&
		camera->mCameraMode == CAMERAMODE_DEMO &&
		SDL_GetKeyboardFocus() == gWindow && SDL_GetWindowRelativeMouseMode(gWindow))
	{
		const char* value = getenv("SPIDEY_MOUSE_SENSITIVITY");
		f32 sensitivity = value ? (f32)atof(value) : 3.0f;
		if (sensitivity < 0.1f) sensitivity = 0.1f;
		if (sensitivity > 20.0f) sensitivity = 20.0f;
		if (fx) camera->SetCamAngle((i16)(camera->field_236 + fx * sensitivity), 0);
		if (fy)
		{
			i32 height = camera->GetCamYDistance() - (i32)(fy * sensitivity);
			if (height < -600) height = -600;
			if (height > 600) height = 600;
			camera->SetCamYDistance((i16)height, 0);
		}
	}

	*dx = (i32)fx;
	*dy = (i32)fy;
	buttons[0] = (b & SDL_BUTTON_LMASK) ? 0x80 : 0;
	buttons[1] = (b & SDL_BUTTON_RMASK) ? 0x80 : 0;
	buttons[2] = (b & SDL_BUTTON_MMASK) ? 0x80 : 0;
}

static SDL_Gamepad* gPad;
static i32 gPadChecked;

static SDL_Gamepad* openPad(void)
{
	if (gPad)
		return gPad;
	if (gPadChecked++ % 120)   // rescan every couple of seconds only
		return 0;
	int count = 0;
	SDL_JoystickID* ids = SDL_GetGamepads(&count);
	if (ids)
	{
		if (count > 0)
			gPad = SDL_OpenGamepad(ids[0]);
		SDL_free(ids);
	}
	if (gPad)
		printf("Plat(sdl3): gamepad %s\n", SDL_GetGamepadName(gPad));
	return gPad;
}

// DirectInput gave the game X/Y in -1000..1000 (DIPROP_RANGE in
// DXINPUT_SetupController) and a POV hat in hundredths of degrees.
i32 Plat_InputPollController(i32* x, i32* y, u32* pov, u8 buttons[32], i32* numButtons)
{
	SDL_Gamepad* pad = openPad();
	if (!pad)
	{
		*numButtons = 0;
		return 0;
	}
	if (!SDL_GamepadConnected(pad))
	{
		SDL_CloseGamepad(pad);
		gPad = 0;
		*numButtons = 0;
		return 0;
	}

	*x = SDL_GetGamepadAxis(pad, SDL_GAMEPAD_AXIS_LEFTX) * 1000 / 32767;
	*y = SDL_GetGamepadAxis(pad, SDL_GAMEPAD_AXIS_LEFTY) * 1000 / 32767;

	// PlayStation layout the game was written for: 0 triangle, 1 circle,
	// 2 cross, 3 square, 4 L1, 5 R1, 6 L2, 7 R2, 8 select, 9 start
	static const SDL_GamepadButton order[] = {
		SDL_GAMEPAD_BUTTON_NORTH, SDL_GAMEPAD_BUTTON_EAST, SDL_GAMEPAD_BUTTON_SOUTH,
		SDL_GAMEPAD_BUTTON_WEST, SDL_GAMEPAD_BUTTON_LEFT_SHOULDER, SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER,
		SDL_GAMEPAD_BUTTON_LEFT_STICK, SDL_GAMEPAD_BUTTON_RIGHT_STICK,
		SDL_GAMEPAD_BUTTON_BACK, SDL_GAMEPAD_BUTTON_START,
	};
	memset(buttons, 0, 32);
	for (u32 i = 0; i < sizeof(order) / sizeof(order[0]); i++)
		buttons[i] = SDL_GetGamepadButton(pad, order[i]) ? 0x80 : 0;
	buttons[6] |= SDL_GetGamepadAxis(pad, SDL_GAMEPAD_AXIS_LEFT_TRIGGER) > 16000 ? 0x80 : 0;
	buttons[7] |= SDL_GetGamepadAxis(pad, SDL_GAMEPAD_AXIS_RIGHT_TRIGGER) > 16000 ? 0x80 : 0;
	*numButtons = 10;

	i32 up = SDL_GetGamepadButton(pad, SDL_GAMEPAD_BUTTON_DPAD_UP);
	i32 down = SDL_GetGamepadButton(pad, SDL_GAMEPAD_BUTTON_DPAD_DOWN);
	i32 left = SDL_GetGamepadButton(pad, SDL_GAMEPAD_BUTTON_DPAD_LEFT);
	i32 right = SDL_GetGamepadButton(pad, SDL_GAMEPAD_BUTTON_DPAD_RIGHT);
	if (up && right) *pov = 4500;
	else if (right && down) *pov = 13500;
	else if (down && left) *pov = 22500;
	else if (left && up) *pov = 31500;
	else if (up) *pov = 0;
	else if (right) *pov = 9000;
	else if (down) *pov = 18000;
	else if (left) *pov = 27000;
	else *pov = 0xFFFFFFFF;
	return 1;
}

void Plat_InputRumble(i32 on, f32 strength)
{
	if (!gPad)
		return;
	u16 s = on ? (u16)(strength * 65535.0f) : 0;
	SDL_RumbleGamepad(gPad, s, s, on ? 60000 : 0);
}

// ----------------------------------------------------------------- sound

#define MIX_RATE 44100
#define MAX_VOICES 64

struct PlatSoundBuffer
{
	f32* samples;    // interleaved stereo, -1..1
	i32 frames;
	i32 rate;
};

struct PlatSoundVoice
{
	PlatSoundBuffer* buf;
	f64 pos;         // in source frames
	i32 rate;        // current playback rate in Hz
	i32 playing;
	i32 loop;
	f32 gain;        // linear
	f32 pan;         // -1..1
};

static SDL_AudioStream* gStream;
static PlatSoundVoice* gVoices[MAX_VOICES];
static SDL_Mutex* gMixLock;

static void SDLCALL mixCallback(void*, SDL_AudioStream* stream, int additional, int)
{
	i32 frames = additional / (i32)(2 * sizeof(f32));
	if (frames <= 0)
		return;
	f32* out = (f32*)calloc(frames * 2, sizeof(f32));

	SDL_LockMutex(gMixLock);
	for (i32 v = 0; v < MAX_VOICES; v++)
	{
		PlatSoundVoice* voice = gVoices[v];
		if (!voice || !voice->playing || !voice->buf)
			continue;
		PlatSoundBuffer* b = voice->buf;
		f64 step = (f64)voice->rate / MIX_RATE;
		f32 gl = voice->gain * (voice->pan <= 0.0f ? 1.0f : 1.0f - voice->pan);
		f32 gr = voice->gain * (voice->pan >= 0.0f ? 1.0f : 1.0f + voice->pan);
		for (i32 i = 0; i < frames; i++)
		{
			i32 p = (i32)voice->pos;
			if (p >= b->frames)
			{
				if (!voice->loop)
				{
					voice->playing = 0;
					break;
				}
				voice->pos -= b->frames;
				p = (i32)voice->pos;
				if (p >= b->frames)
					p = 0;
			}
			f32 frac = (f32)(voice->pos - p);
			i32 p1 = p + 1 < b->frames ? p + 1 : (voice->loop ? 0 : p);
			f32 l = b->samples[p * 2] + (b->samples[p1 * 2] - b->samples[p * 2]) * frac;
			f32 r = b->samples[p * 2 + 1] + (b->samples[p1 * 2 + 1] - b->samples[p * 2 + 1]) * frac;
			out[i * 2] += l * gl;
			out[i * 2 + 1] += r * gr;
			voice->pos += step;
		}
	}
	SDL_UnlockMutex(gMixLock);

	SDL_PutAudioStreamData(stream, out, frames * 2 * sizeof(f32));
	free(out);
}

// @Bogus
i32 Plat_SndInit(void)
{
	SDL_AudioSpec spec;
	spec.format = SDL_AUDIO_F32;
	spec.channels = 2;
	spec.freq = MIX_RATE;
	gMixLock = SDL_CreateMutex();
	gStream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, mixCallback, 0);
	if (!gStream)
	{
		printf("Plat(sdl3): no audio device: %s\n", SDL_GetError());
		return 0;
	}
	SDL_SetAudioStreamGain(gStream, Plat_AudioGain("SPIDEY_SFX_VOLUME"));
	SDL_ResumeAudioStreamDevice(gStream);
	return 1;
}

void Plat_SndShutdown(void)
{
	if (gStream)
		SDL_DestroyAudioStream(gStream);
	gStream = 0;
}

PlatSoundBuffer* Plat_SndCreateBuffer(const void* pcm, i32 bytes, i32 rate, i32 channels, i32 bits)
{
	i32 bps = (bits / 8) * channels;
	if (bps <= 0)
		return 0;
	PlatSoundBuffer* b = new PlatSoundBuffer;
	b->frames = bytes / bps;
	b->rate = rate;
	b->samples = (f32*)malloc(b->frames * 2 * sizeof(f32));
	const u8* p8 = (const u8*)pcm;
	const i16* p16 = (const i16*)pcm;
	for (i32 i = 0; i < b->frames; i++)
	{
		f32 l, r;
		if (bits == 8)
		{
			l = (p8[i * channels] - 128) / 128.0f;
			r = channels > 1 ? (p8[i * channels + 1] - 128) / 128.0f : l;
		}
		else
		{
			l = p16[i * channels] / 32768.0f;
			r = channels > 1 ? p16[i * channels + 1] / 32768.0f : l;
		}
		b->samples[i * 2] = l;
		b->samples[i * 2 + 1] = r;
	}
	return b;
}

void Plat_SndDestroyBuffer(PlatSoundBuffer* b)
{
	if (!b)
		return;
	SDL_LockMutex(gMixLock);
	for (i32 v = 0; v < MAX_VOICES; v++)
		if (gVoices[v] && gVoices[v]->buf == b)
			gVoices[v]->buf = 0;
	SDL_UnlockMutex(gMixLock);
	free(b->samples);
	delete b;
}

PlatSoundVoice* Plat_SndCreateVoice(PlatSoundBuffer* b)
{
	PlatSoundVoice* v = new PlatSoundVoice;
	v->buf = b;
	v->pos = 0.0;
	v->rate = b->rate;
	v->playing = 0;
	v->loop = 0;
	v->gain = 1.0f;
	v->pan = 0.0f;
	SDL_LockMutex(gMixLock);
	for (i32 i = 0; i < MAX_VOICES; i++)
	{
		if (!gVoices[i])
		{
			gVoices[i] = v;
			break;
		}
	}
	SDL_UnlockMutex(gMixLock);
	return v;
}

void Plat_SndDestroyVoice(PlatSoundVoice* v)
{
	if (!v)
		return;
	SDL_LockMutex(gMixLock);
	for (i32 i = 0; i < MAX_VOICES; i++)
		if (gVoices[i] == v)
			gVoices[i] = 0;
	SDL_UnlockMutex(gMixLock);
	delete v;
}

void Plat_SndPlay(PlatSoundVoice* v, i32 loop)
{
	SDL_LockMutex(gMixLock);
	v->pos = 0.0;
	v->loop = loop;
	v->playing = 1;
	SDL_UnlockMutex(gMixLock);
}

void Plat_SndStop(PlatSoundVoice* v)
{
	v->playing = 0;
}

i32 Plat_SndIsPlaying(PlatSoundVoice* v)
{
	return v->playing;
}

void Plat_SndSetVolume(PlatSoundVoice* v, i32 hundredthsDb)
{
	if (hundredthsDb > 0) hundredthsDb = 0;
	if (hundredthsDb < -10000) hundredthsDb = -10000;
	v->gain = powf(10.0f, hundredthsDb / 2000.0f);
}

void Plat_SndSetPan(PlatSoundVoice* v, i32 hundredthsDb)
{
	if (hundredthsDb < -10000) hundredthsDb = -10000;
	if (hundredthsDb > 10000) hundredthsDb = 10000;
	v->pan = hundredthsDb / 10000.0f;
}

void Plat_SndSetFrequency(PlatSoundVoice* v, i32 hz)
{
	SDL_LockMutex(gMixLock);
	// DirectSound uses zero to restore the buffer's original sample rate.
	if (hz == 0 && v->buf)
		hz = v->buf->rate;
	if (hz < 100) hz = 100;
	if (hz > 200000) hz = 200000;
	v->rate = hz;
	SDL_UnlockMutex(gMixLock);
}

i32 Plat_SndGetFrequency(PlatSoundVoice* v)
{
	return v->rate;
}

i32 Plat_SndBufferRate(PlatSoundBuffer* b)
{
	return b->rate;
}
