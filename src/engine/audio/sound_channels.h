#pragma once

#include "compatibility/export.h"
#include "compatibility/non_win32.h"
#include "compatibility/my_dx.h"

namespace spidey {
namespace audio {

struct SoundChannelRecord
{
	LPDIRECTSOUNDBUFFER pDSB;

	i32 mFrequency;

	// Forces IsPlaying to report active, original status byte at offset 8.
	u8 forcePlayingStatus;
	u8 isLooping; // original loop flag at offset 9

	PADDING(2);
};

} // namespace audio
} // namespace spidey

typedef spidey::audio::SoundChannelRecord SDDXSoundHolder;

EXPORT void DXSOUND_Close(i32);
EXPORT void DXSOUND_CreateDSBuffer(char *,i32);
EXPORT void DXSOUND_Init(void);
EXPORT i32 DXSOUND_IsPlaying(i32);
EXPORT void DXSOUND_Load(char *);
EXPORT void DXSOUND_Open(i32,i32,i32);
EXPORT void DXSOUND_Play(i32,i32);
EXPORT void DXSOUND_SetPan(i32,i32);
EXPORT void DXSOUND_SetPitch(i32,i32);
EXPORT void DXSOUND_SetVolume(i32,i32);
EXPORT void DXSOUND_ShutDown(void);
EXPORT void DXSOUND_Stop(i32);
EXPORT void DXSOUND_Unload(char *,i32);
EXPORT void ParseWavHeader(char *,tWAVEFORMATEX **,long *,u8 **);
EXPORT u8* loadWAV(char *,tWAVEFORMATEX *,long *);

void validate_SDXSoundHolder();
void validate_SDxSomething();
void validate_DSBUFFERDESC();
