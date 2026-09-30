// Headless backend: no window, no rendering, no sound. Input can be scripted
// through the SPIDEY_KEYS environment variable so logic smoke tests can walk
// the menus, e.g. SPIDEY_KEYS="3000:enter,2000:enter,2000:enter" presses
// Enter 3 s after start, then 2 s later, then 2 s later.

#include "../plat.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <ctime>
#include <pthread.h>
#include <unistd.h>

// ---------------------------------------------------------------- window

static u32 gStartMs;

static u32 nowMs(void)
{
	struct timespec ts;
	clock_gettime(CLOCK_MONOTONIC, &ts);
	return (u32)(ts.tv_sec * 1000 + ts.tv_nsec / 1000000);
}

i32 Plat_Init(i32 width, i32 height, i32)
{
	gStartMs = nowMs();
	printf("Plat(null): headless %dx%d\n", width, height);
	return 1;
}

void Plat_Shutdown(void)
{
	Plat_MovieStop();
}

// SPIDEY_QUIT_MS: stop yielding after this many ms (default: never).
i32 Plat_Yield(void)
{
	static i32 quitMs = -2;
	if (quitMs == -2)
	{
		const char* q = getenv("SPIDEY_QUIT_MS");
		quitMs = q ? atoi(q) : -1;
	}
	if (quitMs >= 0 && (i32)(nowMs() - gStartMs) > quitMs)
		return 0;
	return 1;
}

u32 Plat_Ticks(void)
{
	// milliseconds since Plat_Init, the same clock SPIDEY_KEYS and
	// SPIDEY_QUIT_MS use, so SPIDEY_DUMPPOLYS_AT / SPIDEY_SHOTS values are
	// run-relative too (an absolute clock made every "_AT" fire at once)
	return nowMs() - gStartMs;
}

void Plat_Sleep(u32 ms)
{
	usleep(ms * 1000);
}

// ----------------------------------------------------------------- timer

struct SNullTimer
{
	pthread_t thread;
	u32 periodMs;
	PlatTimerFn fn;
	void* user;
	volatile i32 stop;
};

static SNullTimer gTimers[4];

static void* timerThread(void* arg)
{
	SNullTimer* t = (SNullTimer*)arg;
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
	SNullTimer* t = &gTimers[handle - 1];
	t->stop = 1;
	pthread_join(t->thread, 0);
	t->fn = 0;
}

// -------------------------------------------------------------- graphics

struct PlatTexture
{
	i32 width, height;
};

static i32 gTexCount, gFanCount, gFrameCount;

PlatTexture* Plat_TexCreate(i32 width, i32 height, PlatTexFormat)
{
	PlatTexture* t = new PlatTexture;
	t->width = width;
	t->height = height;
	gTexCount++;
	return t;
}

void Plat_TexUpload(PlatTexture* t, const void* pixels, i32 pitch)
{
	// SPIDEY_DUMPTEX=1: print what the game uploads (test aid)
	static i32 dump = -1;
	if (dump < 0)
		dump = getenv("SPIDEY_DUMPTEX") ? 1 : 0;
	if (dump)
	{
		const u8* p = (const u8*)pixels;
		i32 nonzero = 0, total = pitch * t->height;
		for (i32 i = 0; i < total; i++)
			nonzero += p[i] != 0;
		printf("TEX %dx%d fmt=%d pitch=%d nonzero=%d/%d first=%02x%02x%02x%02x%02x%02x%02x%02x\n",
				t->width, t->height, 0, pitch, nonzero, total, p[0], p[1], p[2], p[3], p[4], p[5], p[6], p[7]);
	}
}

void Plat_TexDestroy(PlatTexture* t)
{
	gTexCount--;
	delete t;
}

void Plat_GfxBeginScene(u32, i32)
{
	gFanCount = 0;
}

void Plat_GfxEndScene(void)
{
}

void Plat_GfxFlip(void)
{
	gFrameCount++;
	if ((gFrameCount % 300) == 0)
		printf("Plat(null): frame %d, %d fans this frame, %d textures alive\n",
				gFrameCount, gFanCount, gTexCount);

	// a real Flip waits for the vertical blank, so do not spin faster than
	// 60 frames per second here either
	static u32 lastFlip;
	u32 now = nowMs();
	if (now - lastFlip < 16)
		usleep((16 - (now - lastFlip)) * 1000);
	lastFlip = nowMs();
}

void Plat_GfxSetBlendMode(u32) {}
void Plat_GfxSetDepthTest(i32) {}
void Plat_GfxSetDepthWrite(i32) {}
void Plat_GfxSetDepthFunc(u32) {}
void Plat_GfxSetFilter(i32) {}
void Plat_GfxSetAddress(u32, u32) {}
void Plat_GfxSetTexAlpha(i32) {}
void Plat_GfxSetTexture(PlatTexture*) {}
void Plat_GfxSetFog(i32, u32, f32, f32) {}

void Plat_GfxDrawFan(const SDXPolyField*, i32)
{
	gFanCount++;
}

i32 Plat_GfxReadPixels(u8*, i32, i32)
{
	return 0;
}

// ----------------------------------------------------------------- input

// DIK codes for the scripted keys
#define DIK_RETURN 0x1C
#define DIK_ESCAPE 0x01
#define DIK_UP 0xC8
#define DIK_DOWN 0xD0
#define DIK_LEFT 0xCB
#define DIK_RIGHT 0xCD
#define DIK_SPACE 0x39

struct SScriptKey
{
	u32 atMs;
	u8 dik;
};

static SScriptKey gScript[64];
static i32 gScriptCount = -1;
static i32 gScriptNext;
static u32 gKeyDownUntil;
static u8 gKeyDown;

static void parseScript(void)
{
	gScriptCount = 0;
	const char* s = getenv("SPIDEY_KEYS");
	if (!s)
		return;

	u32 t = 0;
	char buf[512];
	strncpy(buf, s, sizeof(buf) - 1);
	buf[sizeof(buf) - 1] = 0;

	for (char* tok = strtok(buf, ","); tok && gScriptCount < 64; tok = strtok(0, ","))
	{
		char* colon = strchr(tok, ':');
		if (!colon)
			continue;
		*colon = 0;
		t += atoi(tok);
		const char* name = colon + 1;
		u8 dik = 0;
		if (!strcmp(name, "enter")) dik = DIK_RETURN;
		else if (!strcmp(name, "esc")) dik = DIK_ESCAPE;
		else if (!strcmp(name, "up")) dik = DIK_UP;
		else if (!strcmp(name, "down")) dik = DIK_DOWN;
		else if (!strcmp(name, "left")) dik = DIK_LEFT;
		else if (!strcmp(name, "right")) dik = DIK_RIGHT;
		else if (!strcmp(name, "space")) dik = DIK_SPACE;
		else dik = (u8)strtol(name, 0, 0);
		gScript[gScriptCount].atMs = t;
		gScript[gScriptCount].dik = dik;
		gScriptCount++;
	}
	printf("Plat(null): %d scripted keys\n", gScriptCount);
}

void Plat_InputPollKeyboard(u8 dikState[256])
{
	if (gScriptCount < 0)
		parseScript();

	memset(dikState, 0, 256);
	u32 now = nowMs() - gStartMs;

	if (gKeyDown && now < gKeyDownUntil)
	{
		dikState[gKeyDown] = 0x80;
		Plat_InputMapCutsceneSkip(dikState);
		return;
	}
	gKeyDown = 0;

	if (gScriptNext < gScriptCount && now >= gScript[gScriptNext].atMs)
	{
		gKeyDown = gScript[gScriptNext].dik;
		gKeyDownUntil = now + 120;   // hold for a few frames
		gScriptNext++;
		printf("Plat(null): key %#x at %u ms\n", gKeyDown, now);
		dikState[gKeyDown] = 0x80;
	}
	Plat_InputMapCutsceneSkip(dikState);
}

void Plat_InputPollMouse(i32* dx, i32* dy, u8 buttons[3])
{
	*dx = 0;
	*dy = 0;
	memset(buttons, 0, 3);
}

i32 Plat_InputPollController(i32*, i32*, u32*, u8*, i32* numButtons)
{
	*numButtons = 0;
	return 0;
}

void Plat_InputRumble(i32, f32)
{
}

// ----------------------------------------------------------------- sound

struct PlatSoundBuffer
{
	i32 rate;
};

struct PlatSoundVoice
{
	i32 rate;
	i32 playing;
};

i32 Plat_SndInit(void)
{
	return 1;
}

void Plat_SndShutdown(void)
{
}

PlatSoundBuffer* Plat_SndCreateBuffer(const void*, i32, i32 rate, i32, i32)
{
	PlatSoundBuffer* b = new PlatSoundBuffer;
	b->rate = rate;
	return b;
}

void Plat_SndDestroyBuffer(PlatSoundBuffer* b)
{
	delete b;
}

PlatSoundVoice* Plat_SndCreateVoice(PlatSoundBuffer* b)
{
	PlatSoundVoice* v = new PlatSoundVoice;
	v->rate = b->rate;
	v->playing = 0;
	return v;
}

void Plat_SndDestroyVoice(PlatSoundVoice* v)
{
	delete v;
}

void Plat_SndPlay(PlatSoundVoice* v, i32 loop)
{
	// a one shot sample "finishes" right away, a loop stays playing
	v->playing = loop;
}

void Plat_SndStop(PlatSoundVoice* v)
{
	v->playing = 0;
}

i32 Plat_SndIsPlaying(PlatSoundVoice* v)
{
	return v->playing;
}

void Plat_SndSetVolume(PlatSoundVoice*, i32) {}
void Plat_SndSetPan(PlatSoundVoice*, i32) {}

void Plat_SndSetFrequency(PlatSoundVoice* v, i32 hz)
{
	v->rate = hz;
}

i32 Plat_SndGetFrequency(PlatSoundVoice* v)
{
	return v->rate;
}

i32 Plat_SndBufferRate(PlatSoundBuffer* b)
{
	return b->rate;
}
