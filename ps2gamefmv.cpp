#include "ps2gamefmv.h"
#include "ps2redbook.h"
#include "PCMovie.h"
#include "spidey.h"
#include "front.h"
#include "ps2pad.h"
#include "PCShell.h"
#include "tweak.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif

#include "validate.h"

#ifndef SPIDEY_STANDALONE
EXPORT i32 GameFMV_Active;
#else
extern i32 GameFMV_Active;
#endif
EXPORT u8 GameFMV_CurrentTrack;

EXPORT i32 gGameFmvOne;
EXPORT u8 gGameFmvTwo;

EXPORT u16 GameFMV_Width;
EXPORT u16 GameFMV_Height;
EXPORT i32 GameFMV_EndFrame;

#ifndef SPIDEY_STANDALONE
EXPORT i32 gGameFmvPad;
#else
extern i32 gGameFmvPad;
#endif


#define NUM_MOVIES 27

// @Ok
// Data table read directly from the binary at 0x54F2E8 (27 entries,
// verified via IDA). name is the bink file to play, width/height are
// the movie's pixel size, endframe is its last frame. field_10 and
// field_14 are always 1 for every entry in the binary; they land in
// gGameFmvTwo (u8) and gGameFmvOne (i32) in GameFMV_SetStartTrack, but
// their real meaning was not determined (both fields are constant 1
// in every original entry, so no behavior difference is visible here).
#ifndef SPIDEY_STANDALONE
SMovieDetails movieDetails[NUM_MOVIES] =
{
	{ "ATVILOGO.bik",  320, 240,  208, {0}, 1, 1 },
	{ "NEVERSOFT.bik", 320, 192,  239, {0}, 1, 1 },
	{ "TREYARCH.bik",  320, 240,  119, {0}, 1, 1 },
	{ "GRAYMATT.bik",  320, 192,  239, {0}, 1, 1 },
	{ "L1M1.bik",      320, 192, 2787, {0}, 1, 1 },
	{ "L1M2.bik",      320, 192,  604, {0}, 1, 1 },
	{ "L2M1.bik",      320, 192,  218, {0}, 1, 1 },
	{ "L2M2.bik",      320, 192,  432, {0}, 1, 1 },
	{ "L2M3.bik",      320, 192, 1009, {0}, 1, 1 },
	{ "L3M1.bik",      320, 192, 1950, {0}, 1, 1 },
	{ "L4M1.bik",      320, 192,  334, {0}, 1, 1 },
	{ "L4M2.bik",      320, 192,  685, {0}, 1, 1 },
	{ "L5M1.bik",      320, 192,  670, {0}, 1, 1 },
	{ "L5M2.bik",      320, 192,  182, {0}, 1, 1 },
	{ "L5M3.bik",      320, 192,  226, {0}, 1, 1 },
	{ "L5M4.bik",      320, 192,  185, {0}, 1, 1 },
	{ "L6M1.bik",      320, 192, 1629, {0}, 1, 1 },
	{ "L7M1.bik",      320, 192,  502, {0}, 1, 1 },
	{ "L7M2.bik",      320, 192,  560, {0}, 1, 1 },
	{ "L7M3.bik",      320, 192,  935, {0}, 1, 1 },
	{ "L8M1.bik",      320, 192,  611, {0}, 1, 1 },
	{ "L8M2.bik",      320, 192,  942, {0}, 1, 1 },
	{ "L8M3.bik",      320, 192,  395, {0}, 1, 1 },
	{ "L8M4.bik",      320, 192,  369, {0}, 1, 1 },
	{ "L8M5.bik",      320, 192, 2378, {0}, 1, 1 },
	{ "SOFDEC.bik",       0,   0,    0, {0}, 1, 1 },
	{ "LEGAL.bik",        0,   0,    0, {0}, 1, 1 },
};
#else
extern SMovieDetails movieDetails[NUM_MOVIES];
#endif

// @Ok
// @Matching
u8 GameFMV_PlayMovie(
		u8 a1,
		bool a2,
		bool a3,
		f32 a4)
{
	Redbook_XAStop();
	G_SAVE_GAME.field_88 |= 1 << a1;
	if (G_MECHLIST_PLAYER)
		G_MECHLIST_PLAYER->StopAlertMusic();

	GameFMV_StopFMV();

	gGameFmvPad = 0;

	if (PCMOVIE_Play(movieDetails[a1].name, a3))
	{
		G_GAME_FMV_ACTIVE = 1;
		Front_ClearScreen();

		i32 v4 = gGameState[11] * a4;
		if (v4 > 255)
			v4 = 255;
		PCMOVIE_SetVolume(v4);
		Pad_ClearTriggers(G_SCONTROL);
#ifdef SPIDEY_STANDALONE
		u8 keys[256];
		Plat_InputPollKeyboard(keys);
		bool skipReady = (keys[1] & 0x80) == 0;
#endif

		while (PCMOVIE_NextFrame())
		{
#ifdef SPIDEY_STANDALONE
			Plat_InputPollKeyboard(keys);
			if (!(keys[1] & 0x80))
				skipReady = true;
			else if (skipReady)
				break;
#endif
			gGameFmvPad++;
			if (Pad_Update() ||
					a2 &&
					gGameFmvPad >= 60 &&
					PCSHELL_CheckTriggers(0x7000000, 1, 1))
			{
				G_SCONTROL[0].Start.Triggered = 0;
				break;
			}
		}


		PCMOVIE_Stop();
		Pad_ActuatorOff(0, 0);
		Pad_ActuatorOff(0, 1);
		Pad_ClearTriggers(G_SCONTROL);
		G_GAME_FMV_ACTIVE = 0;

		Front_ClearScreen();
		return 1;
	}

	return 0;
}

// @Ok
int GameFMV_GetNumMovies(void)
{
	return NUM_MOVIES;
}

// @Ok
int PShell_GetNumCostumePSXs(void)
{
	return 10;
}

// @Ok
// @Matching
void GameFMV_Init(void)
{
	PCMOVIE_Init();
}

// @Ok
// @Matching
void GameFMV_SetStartTrack(u8 track)
{
	if ( !G_GAME_FMV_ACTIVE )
	{
		print_if_false(track < 27u, "Bad track");
		print_if_false(G_GAME_FMV_ACTIVE == 0, "Track change when active");

		GameFMV_CurrentTrack = track;

		GameFMV_Width = movieDetails[track].width;
		GameFMV_Height = movieDetails[track].height;

		GameFMV_EndFrame = movieDetails[track].endframe;

		gGameFmvTwo = movieDetails[track].field_10;
		gGameFmvOne = movieDetails[track].field_14;
	}
}

// @Ok
// @Matching
INLINE void GameFMV_StopFMV(void)
{
	if (G_GAME_FMV_ACTIVE)
	{
		PCMOVIE_Stop();
		G_GAME_FMV_ACTIVE = 0;
	}
}

void validate_SMovieDetails(void)
{
	VALIDATE_SIZE(SMovieDetails, 0x18);

	VALIDATE(SMovieDetails, name, 0x0);

	VALIDATE(SMovieDetails, width, 0x4);
	VALIDATE(SMovieDetails, height, 0x6);
	VALIDATE(SMovieDetails, endframe, 0x8);

	VALIDATE(SMovieDetails, field_10, 0x10);
	VALIDATE(SMovieDetails, field_14, 0x14);
}
