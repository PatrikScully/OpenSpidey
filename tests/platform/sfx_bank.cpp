// Compile with -m32 -DSPIDEY_STANDALONE -ffunction-sections -fdata-sections
// and link with -Wl,--gc-sections. File and audio mocks exercise bank lifetime.
#include "../../ps2lowsfx.cpp"
#include <cassert>
#include <sys/mman.h>

static bool bankExists = true;
static bool audioAvailable = true;
static bool waveAvailable = true;
static int waveAsset;
static bool truncatedHeader;
static int loads;
static int opens;
static bool playing[32];
IDirectSoundBuffer* gDxSoundBuffers[0x80];
u8 gSfxVolArr[256];

// @Bogus
u8 FileIO_FileExists(const char*) { return bankExists; }
// @Bogus
i32 FileIO_Open(const char*) { return sizeof(i32) + sizeof(SSfxAsset) * (truncatedHeader ? 1 : 2); }
// @Bogus
void FileIO_Load(void* memory)
{
	memset(memory, 0, FileIO_Open(0));
	*static_cast<i32*>(memory) = 2;
	SSfxAsset* asset = reinterpret_cast<SSfxAsset*>(static_cast<i32*>(memory) + 1);
	asset->field_C = 22050;
	asset->field_14 = 16;
	if (!truncatedHeader)
		asset[1] = asset[0];
}
// @Bogus
void FileIO_Sync(void) {}
// @Bogus
void* DCMem_New(u32 size, i32, i32, void*, bool) { return malloc(size); }
// @Bogus
void Mem_AlignedDelete(void* memory) { free(memory); }
// @Bogus
void DebugPrintfX(char*, ...) {}
// @Bogus
void DoAssert(u8, const char*, ...) {}
// @Bogus
void gsub_430880(void) {}
// @Bogus
i32 amHeapAlloc(u32** output, i32, i32, i32, i32)
{
	*output = 0;
	return 0;
}
// @Bogus
i32 acG2Write(void*, void*, i32) { return 0; }
// @Bogus
i32 amHeapFree(i32) { return 0; }
// @Bogus
i32 AUDIOGROUPS_GetGroup(char* name) { return name[1] == 'p' ? 1 : 2; }
// @Bogus
void DXSOUND_Load(char* name)
{
	loads++;
	if (audioAvailable && waveAvailable)
		gDxSoundBuffers[(AUDIOGROUPS_GetGroup(name) == 1 ? 0 : 64) + waveAsset] =
			reinterpret_cast<IDirectSoundBuffer*>(1);
}
// @Bogus
void DXSOUND_Unload(char* name, i32)
{
	i32 first = AUDIOGROUPS_GetGroup(name) == 1 ? 0 : 64;
	memset(gDxSoundBuffers + first, 0, sizeof(gDxSoundBuffers[0]) * 64);
}
// @Bogus
void DXSOUND_Open(i32 voice, i32 asset, i32 level)
{
	assert(gDxSoundBuffers[asset + (level ? 64 : 0)]);
	opens++;
	playing[voice] = false;
}
// @Bogus
i32 DXSOUND_IsPlaying(i32 voice) { return playing[voice]; }
// @Bogus
void DXSOUND_Play(i32 voice, i32) { playing[voice] = true; }
// @Bogus
void DXSOUND_Stop(i32 voice) { playing[voice] = false; }
// @Bogus
void DXSOUND_Close(i32 voice) { playing[voice] = false; }
// @Bogus
void DXSOUND_SetVolume(i32, i32) {}
// @Bogus
void DXSOUND_SetPan(i32, i32) {}
// @Bogus
void DXSOUND_SetPitch(i32, i32) {}

// @Bogus
int main(void)
{
	void* memory = mmap(reinterpret_cast<void*>(0x00530000), 0x190000,
		PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANONYMOUS | MAP_FIXED, -1, 0);
	assert(memory == reinterpret_cast<void*>(0x00530000));
	G_GAMESTATE[12] = 0x4000;
	G_SFX_ARRAY_ONE[0] = 1;
	G_SFX_ARRAY_ONE[1] = 0x1000003C;
	G_SFX_ARRAY_ONE[2] = 0x1000;

	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(SFX_Play(1, 0x2000, 0) != 0xFFFFFFFF && opens == 1);
	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(loads == 1);
	SFX_LoadBank("l1a1.kat", &G_SFX_RELATED_OUT_LEVEL);
	assert(playSFX(0x40000001, 60, 0x2000, 0x2000, 0, 0x1000) != 0xFFFFFFFF && opens == 2);
	SFX_CloseBank(&G_SOUND_BANK);
	assert(!G_SOUND_BANK.field_4 && !G_SFX_RELATED_OUT_LEVEL.field_4);
	assert(!G_SOUND_BANK.mNumAssets && !G_SFX_RELATED_OUT_LEVEL.mNumAssets);
	assert(!gDxSoundBuffers[0] && !gDxSoundBuffers[64]);
	assert(SFX_Play(1, 0x2000, 0) == 0xFFFFFFFF && opens == 2);

	bankExists = false;
	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(SFX_Play(1, 0x2000, 0) == 0xFFFFFFFF && loads == 2 && opens == 2);
	bankExists = true;
	truncatedHeader = true;
	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(SFX_Play(1, 0x2000, 0) == 0xFFFFFFFF && loads == 2 && opens == 2);
	truncatedHeader = false;
	audioAvailable = false;
	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(SFX_Play(1, 0x2000, 0) == 0xFFFFFFFF && opens == 2);
	audioAvailable = true;
	waveAvailable = false;
	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(SFX_Play(1, 0x2000, 0) == 0xFFFFFFFF && opens == 2);
	waveAvailable = true;
	waveAsset = 1;
	G_SFX_ARRAY_ONE[0] = 0x10001;
	SFX_LoadBank("spidey.kat", &G_SOUND_BANK);
	assert(!gDxSoundBuffers[0] && gDxSoundBuffers[1]);
	assert(SFX_Play(1, 0x2000, 0) != 0xFFFFFFFF && opens == 3);
	SFX_CloseBank(&G_SOUND_BANK);
	assert(!gDxSoundBuffers[0] && !gDxSoundBuffers[1]);
	printf("PASS partial-bank playback, cache, recursive unload, missing/truncated/audio/WAV failure and retry\n");
	return 0;
}
