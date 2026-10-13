#pragma once

#ifndef PS2GAMEFMV_H
#define PS2GAMEFMV_H

#include "compatibility/export.h"

struct SMovieDetails
{
	char *name;
	u16 width;
	u16 height;
	u16 endframe;

	PADDING(0x10-0x8-2);

	i32 field_10;
	i32 field_14;
};

EXPORT int GameFMV_GetNumMovies(void);
EXPORT u8 GameFMV_PlayMovie(u8, bool, bool, f32);

EXPORT void GameFMV_Init(void);
EXPORT void GameFMV_SetStartTrack(u8);
EXPORT void GameFMV_StopFMV(void);

// 0x006150F4. Written by GameFMV_PlayMovie, which is not hooked, and read by
// CBaddy::GetVariable (script variable 0x2136).
EXPORT extern i32 gGameFmvPad;
//#define G_GAME_FMV_PAD (gGameFmvPad)
#define G_GAME_FMV_PAD (*reinterpret_cast<i32*>(0x006150F4))

//#define G_GAME_FMV_ACTIVE (GameFMV_Active)
#define G_GAME_FMV_ACTIVE (*reinterpret_cast<i32*>(0x006151F8))

void validate_SMovieDetails(void);

#endif
