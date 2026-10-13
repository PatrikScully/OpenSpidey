#include "engine/audio/music.h"

#include "engine/audio/ps2lowsfx.h"
#include "game/player/spidey.h"
#include "engine/world/trig.h"
#include "core/utils.h"

// 0x0060CFB0, the same flag main.cpp and pshell.cpp call gEndTrainingFlag.
// Music is silent while it is set.
static i32 * const gEndTrainingFlag = reinterpret_cast<i32*>(0x0060CFB0);

// The per level music tables, read straight out of the original at
// 0x0054D86C..0x0054DEDC. They come in pairs: a list of SFX ids (played with
// bit 0xC000 set) and a list of vblank durations, one duration per id.

// 0x0054D86C / 0x0054D878, the fight music intro used on level 0x506 while
// gFightMusicVariant is 0.
static const i32 gFightIntroIdsA[3] = { 0x4, 0x1E2, 0x1E3 };
static const i32 gFightIntroTimesA[3] = { 0xD0, 0xCE, 0x41 };

// 0x0054D884 / 0x0054D890, the same for gFightMusicVariant 1.
static const i32 gFightIntroIdsB[3] = { 0x5, 0x1E4, 0x1E5 };
static const i32 gFightIntroTimesB[3] = { 0xB2, 0xB2, 0x74 };

// 0x0054D89C / 0x0054D8B0, level 0x201.
static const i32 gMusicIds0201[5] = { 0x12C, 0x12D, 0x12E, 0x12F, 0x130 };
static const i32 gMusicTimes0201[5] = { 0xCA, 0xCA, 0xCA, 0xCA, 0xCA };

// 0x0054D8C4 / 0x0054D8C8, level 0x202 with gMusicPhase 1.
static const i32 gMusicIds0202b[1] = { 0x12C };
static const i32 gMusicTimes0202b[1] = { 0xCA };

// 0x0054D8CC / 0x0054D8D4, level 0x202 with gMusicPhase 0.
static const i32 gMusicIds0202a[2] = { 0x12D, 0x12E };
static const i32 gMusicTimes0202a[2] = { 0xCA, 0xCA };

// 0x0054D8DC / 0x0054D91C, level 0x302.
static const i32 gMusicIds0302[16] = {
	0x5, 0x5, 0x12C, 0x12C, 0x5, 0x5, 0x12D, 0x12E,
	0x12F, 0x5, 0x5, 0x12C, 0x12C, 0x12D, 0x12E, 0x12F
};
static const i32 gMusicTimes0302[16] = {
	0x73, 0x73, 0xE6, 0xE6, 0x73, 0x73, 0xCA, 0xCA,
	0x32, 0x73, 0x73, 0xE6, 0xE6, 0xCA, 0xCA, 0x32
};

// 0x0054D95C / 0x0054D970, level 0x401.
static const i32 gMusicIds0401[5] = { 0x12C, 0x12D, 0x12E, 0x12F, 0x130 };
static const i32 gMusicTimes0401[5] = { 0x76, 0x76, 0x76, 0xEE, 0xEE };

// 0x0054D984 / 0x0054D990, level 0x501.
static const i32 gMusicIds0501[3] = { 0x12C, 0x12D, 0x12E };
static const i32 gMusicTimes0501[3] = { 0x6C, 0x6C, 0x6C };

// 0x0054D99C / 0x0054D9A8, level 0x502.
static const i32 gMusicIds0502[3] = { 0x12C, 0x12D, 0x12E };
static const i32 gMusicTimes0502[3] = { 0x73, 0x73, 0x73 };

// 0x0054D9B4 / 0x0054D9C0, level 0x604.
static const i32 gMusicIds0604[3] = { 0x12C, 0x12D, 0x12E };
static const i32 gMusicTimes0604[3] = { 0x11F, 0x11F, 0x11F };

// 0x0054D9CC / 0x0054D9F4, level 0x701.
static const i32 gMusicIds0701[10] = {
	0x5, 0x5, 0x12D, 0x12D, 0x12C, 0x12C, 0x4, 0x4, 0x12C, 0x12C
};
static const i32 gMusicTimes0701[10] = {
	0x9E, 0x9E, 0x9E, 0x9E, 0x9E, 0x9E, 0xA1, 0xA1, 0x9E, 0x9E
};

// 0x0054DA1C / 0x0054DA40, level 0x702.
static const i32 gMusicIds0702[9] = {
	0x12C, 0x12C, 0x12E, 0x12E, 0x12C, 0x12C, 0x12D, 0x12E, 0x12E
};
static const i32 gMusicTimes0702[9] = {
	0x77, 0x77, 0x77, 0x77, 0x77, 0x77, 0x77, 0x77, 0x77
};

// 0x0054DA64 / 0x0054DA8C, level 0x704.
static const i32 gMusicIds0704[10] = {
	0x12C, 0x12C, 0x12D, 0x12D, 0x12E, 0x12E, 0x12D, 0x12D, 0x12E, 0x12E
};
static const i32 gMusicTimes0704[10] = {
	0x82, 0x82, 0x82, 0x82, 0x82, 0x82, 0x82, 0x82, 0x82, 0x82
};

// 0x0054DAB4 / 0x0054DAFC, level 0x705 with gMusicPhase 0.
static const i32 gMusicIds0705a[18] = {
	0x12C, 0x12C, 0x12D, 0x12D, 0x12D, 0x12D, 0x4, 0x4, 0x5,
	0x5, 0x5, 0x5, 0x4, 0x4, 0x12D, 0x12D, 0x12D, 0x12D
};
static const i32 gMusicTimes0705a[18] = {
	0x73, 0x73, 0xE4, 0xE4, 0xE4, 0xE4, 0x73, 0x73, 0xE4,
	0xE4, 0xE4, 0xE4, 0x73, 0x73, 0xE4, 0xE4, 0xE4, 0xE4
};

// 0x0054DB44 / 0x0054DB6C, level 0x705 with gMusicPhase 1.
static const i32 gMusicIds0705b[10] = {
	0x130, 0x130, 0x130, 0x130, 0x12F, 0x12E, 0x12F, 0x12E, 0x4, 0x4
};
static const i32 gMusicTimes0705b[10] = {
	0xE4, 0xE4, 0xE4, 0xE4, 0xE4, 0xE4, 0xE4, 0xE4, 0x73, 0x73
};

// 0x0054DB94 / 0x0054DC38, level 0x802.
static const i32 gMusicIds0802[41] = {
	0x12C, 0x12C, 0x12D, 0x12E, 0x12D, 0x12E, 0x12F, 0x130,
	0x12F, 0x130, 0x12C, 0x131, 0x132, 0x131, 0x132, 0x131,
	0x132, 0x131, 0x132, 0x12F, 0x130, 0x131, 0x132, 0x131,
	0x132, 0x131, 0x132, 0x12D, 0x12E, 0x131, 0x132, 0x131,
	0x132, 0x12F, 0x130, 0x131, 0x132, 0x131, 0x132, 0x131,
	0x132
};
static const i32 gMusicTimes0802[41] = {
	0x80, 0x80, 0x7F, 0x81, 0x7F, 0x81, 0x7F, 0x81,
	0x7F, 0x81, 0x80, 0x7F, 0x81, 0x7F, 0x81, 0x7F,
	0x81, 0x7F, 0x81, 0x7F, 0x81, 0x7F, 0x81, 0x7F,
	0x81, 0x7F, 0x81, 0x7F, 0x81, 0x7F, 0x81, 0x7F,
	0x81, 0x7F, 0x81, 0x7F, 0x81, 0x7F, 0x81, 0x7F,
	0x81
};

// 0x0054DCDC / 0x0054DCE8, level 0x805.
static const i32 gMusicIds0805[3] = { 0x12C, 0x12D, 0x12E };
static const i32 gMusicTimes0805[3] = { 0xE6, 0xE6, 0xE6 };

// 0x0054DCF4 / 0x0054DD0C, levels 0x901 to 0x904.
static const i32 gMusicIds0901[6] = { 0x4, 0x4, 0x5, 0x5, 0x5, 0x5 };
static const i32 gMusicTimes0901[6] = { 0xCC, 0xCC, 0xCC, 0xCC, 0xCC, 0xCC };

// 0x0054DD24 / 0x0054DD6C, levels 0x1101 to 0x1104.
static const i32 gMusicIds1101[18] = {
	0x4, 0x4, 0x4, 0x4, 0x5, 0x5, 0x5, 0x5, 0x12C,
	0x12D, 0x12C, 0x12D, 0x5, 0x5, 0x12C, 0x12D, 0x12C, 0x12D
};
static const i32 gMusicTimes1101[18] = {
	0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x88, 0x87,
	0x89, 0x87, 0x89, 0x88, 0x88, 0x87, 0x89, 0x87, 0x89
};

// 0x0054DDB4 / 0x0054DDD0, level 0x1302.
static const i32 gMusicIds1302[7] = {
	0x12C, 0x12D, 0x12E, 0x12F, 0x130, 0x131, 0x132
};
static const i32 gMusicTimes1302[7] = {
	0xC2, 0xBC, 0xAB, 0xB3, 0xB4, 0xBC, 0xBA
};

// 0x0054DDEC / 0x0054DDFC, level 0x1303.
static const i32 gMusicIds1303[4] = { 0x12C, 0x12D, 0x12E, 0x12F };
static const i32 gMusicTimes1303[4] = { 0x6E, 0x6E, 0x6E, 0x6E };

// 0x0054DE0C / 0x0054DE74, level 0x1701. The only list that does not loop back
// to entry 0: once it runs out it restarts at entry 3.
static const i32 gMusicIds1701[26] = {
	0x12C, 0x12D, 0x12C, 0x12D, 0x130, 0x131, 0x130, 0x131,
	0x12E, 0x12F, 0x132, 0x133, 0x132, 0x133, 0x130, 0x131,
	0x132, 0x133, 0x132, 0x133, 0x130, 0x131, 0x132, 0x133,
	0x132, 0x133
};
static const i32 gMusicTimes1701[26] = {
	0x9B, 0xA3, 0x9B, 0xA3, 0x9F, 0x9F, 0x9F, 0x9F,
	0x9C, 0xA2, 0x94, 0xAA, 0x94, 0xAA, 0x9F, 0x9F,
	0x94, 0xAA, 0x94, 0xAA, 0x9F, 0x9F, 0x94, 0xAA,
	0x94, 0xAA
};

// 0x0054D868, initialised to 1. While it is set the fight music intro list is
// not walked, so the loop track at 0xC004/0xC005 is what plays. Cleared every
// time that loop track is (re)started, which replays the intro.
static u8 gFightMusicIntroDone = 1;

// 0x0060D870, index into the current level's track list.
static i32 gMusicTrackIndex;

// 0x0060D874.
EXPORT u8 gMusicTrackEnding;

// 0x0060D878, vblank accumulator for the fight music intro list.
static i32 gFightMusicTimer;

// 0x0060D87C, index into the fight music intro list.
static i32 gFightMusicIntroIndex;

// 0x0060D880.
EXPORT i32 gMusicPhase;

// 0x0060D884, the vblank count this function last ran on.
static u32 gMusicLastVblank;

// 0x0060D888 / 0x0060D88C, the fight music intro list picked last time.
static const i32 *gFightMusicIntroIds;
static const i32 *gFightMusicIntroTimes;

// 0x0060D890, which of the two fight music intros to use next. Follows
// CPlayer::field_534.
static i32 gFightMusicVariant;

// @Ok
// 0x00459650, 1696 bytes. This translation unit holds exactly one function:
// the Mac symbol table has a music.cpp TU with Music_MusicUpdate(void) as its
// only entry, and tools/prototypes.json lists it under "music".
void Music_MusicUpdate(void)
{
	gMusicTrackEnding = 0;

	if (G_POST_WATER_EFFECT != 0 || *gEndTrainingFlag != 0)
	{
		gMusicLastVblank = G_VBLANKS;
		return;
	}

	if (gMusicLastVblank == G_VBLANKS)
		return;

	i32 delta;
	if (G_VBLANKS > gMusicLastVblank)
	{
		delta = static_cast<i32>(G_VBLANKS - gMusicLastVblank);
	}
	else
	{
		delta = 1;
		G_SFX_GLOBAL = 6666;
		gMusicTrackIndex = 0;
	}

	const i32 *pIds = 0;
	const i32 *pTimes = 0;
	u32 count = 0;

	// level 0x1701 restarts at entry 3 instead of entry 0
	i32 wrapIndex = 0;

	// the original tests the level id with a chain of unsigned compares, so
	// every id not listed here (and every id inside a listed range) falls
	// through and does nothing
	i32 useFightMusic = 0;
	i32 levelId = Trig_GetLevelId();

	switch (levelId)
	{
		case 0x201:
			pIds = gMusicIds0201;
			pTimes = gMusicTimes0201;
			count = 5;
			break;

		case 0x202:
			if (gMusicPhase == 0)
			{
				pIds = gMusicIds0202a;
				pTimes = gMusicTimes0202a;
				count = 2;
			}
			else if (gMusicPhase == 1)
			{
				pIds = gMusicIds0202b;
				pTimes = gMusicTimes0202b;
				count = 1;
			}
			break;

		case 0x302:
			pIds = gMusicIds0302;
			pTimes = gMusicTimes0302;
			count = 16;
			break;

		case 0x401:
			pIds = gMusicIds0401;
			pTimes = gMusicTimes0401;
			count = 5;
			break;

		case 0x501:
			pIds = gMusicIds0501;
			pTimes = gMusicTimes0501;
			count = 3;
			break;

		case 0x502:
			pIds = gMusicIds0502;
			pTimes = gMusicTimes0502;
			count = 3;
			break;

		case 0x604:
			pIds = gMusicIds0604;
			pTimes = gMusicTimes0604;
			count = 3;
			break;

		case 0x701:
			pIds = gMusicIds0701;
			pTimes = gMusicTimes0701;
			count = 10;
			break;

		case 0x702:
			pIds = gMusicIds0702;
			pTimes = gMusicTimes0702;
			count = 9;
			break;

		case 0x704:
			pIds = gMusicIds0704;
			pTimes = gMusicTimes0704;
			count = 10;
			break;

		case 0x705:
			if (gMusicPhase == 1)
			{
				pIds = gMusicIds0705b;
				pTimes = gMusicTimes0705b;
				count = 10;
			}
			else if (gMusicPhase == 0)
			{
				pIds = gMusicIds0705a;
				pTimes = gMusicTimes0705a;
				count = 18;
			}
			break;

		case 0x802:
			pIds = gMusicIds0802;
			pTimes = gMusicTimes0802;
			count = 41;
			break;

		case 0x805:
			pIds = gMusicIds0805;
			pTimes = gMusicTimes0805;
			count = 3;
			break;

		case 0x901:
		case 0x902:
		case 0x903:
		case 0x904:
			pIds = gMusicIds0901;
			pTimes = gMusicTimes0901;
			count = 6;
			break;

		case 0x1101:
		case 0x1102:
		case 0x1103:
		case 0x1104:
			pIds = gMusicIds1101;
			pTimes = gMusicTimes1101;
			count = 18;
			break;

		case 0x1302:
			pIds = gMusicIds1302;
			pTimes = gMusicTimes1302;
			count = 7;
			break;

		case 0x1303:
			pIds = gMusicIds1303;
			pTimes = gMusicTimes1303;
			count = 4;
			break;

		case 0x1701:
			wrapIndex = 3;
			pIds = gMusicIds1701;
			pTimes = gMusicTimes1701;
			count = 26;
			break;

		// the boss levels have no track list, they run the fight music instead
		case 0x101:
		case 0x102:
		case 0x103:
		case 0x104:
		case 0x301:
		case 0x303:
		case 0x304:
		case 0x305:
		case 0x503:
		case 0x504:
		case 0x506:
		case 0x507:
		case 0x703:
		case 0x801:
		case 0x803:
			useFightMusic = 1;
			break;

		default:
			break;
	}

	if (useFightMusic)
	{
		CPlayer *pPlayer = G_MECHLIST_PLAYER;
		if (pPlayer != 0)
		{
			pPlayer->field_530 += delta;
			gFightMusicTimer += delta;

			if (pPlayer->field_C5C != 0
					&& (pPlayer->field_528 != 0 || pPlayer->field_52C != 0))
			{
				if (levelId != 0x506)
				{
					// every other boss level goes straight to the loop track
					gFightMusicIntroIds = 0;
					gFightMusicIntroTimes = 0;
					gFightMusicIntroDone = 1;
				}
				else
				{
					const i32 *pIntroIds;
					if (gFightMusicVariant == 0)
					{
						pIntroIds = gFightIntroIdsA;
						gFightMusicIntroTimes = gFightIntroTimesA;
						gFightMusicIntroIds = gFightIntroIdsA;
					}
					else if (gFightMusicVariant == 1)
					{
						pIntroIds = gFightIntroIdsB;
						gFightMusicIntroTimes = gFightIntroTimesB;
						gFightMusicIntroIds = gFightIntroIdsB;
					}
					else
					{
						pIntroIds = gFightMusicIntroIds;
					}

					if (pIntroIds != 0 && gFightMusicIntroDone == 0
							&& static_cast<u32>(gFightMusicTimer)
								> static_cast<u32>(gFightMusicIntroTimes[gFightMusicIntroIndex]))
					{
						i32 next = gFightMusicIntroIndex + 1;
						gFightMusicTimer = 0;
						gFightMusicIntroIndex = next;
						if (static_cast<u32>(next) >= 3)
						{
							pPlayer->field_530 = pPlayer->field_C60 + 1;
							gFightMusicIntroDone = 1;
						}
						else
						{
							pPlayer->field_538 = SFX_Play(
									static_cast<u32>(pIntroIds[next] | 0xC000),
									static_cast<i16>(pPlayer->field_52C),
									0);
						}
					}
				}

				if (pPlayer->field_530 > pPlayer->field_C60
						&& gFightMusicIntroDone != 0)
				{
					gFightMusicTimer = 0;
					if (pPlayer->field_528 != 0)
					{
						i32 vol = (pPlayer->field_528 + 11) << 10;
						pPlayer->field_52C = vol;
						if (vol > 0x3FFF)
							pPlayer->field_52C = 0x3FFF;
					}
					else if (pPlayer->field_52C > 0x3000)
					{
						pPlayer->field_52C = pPlayer->field_52C - 0x400;
					}
					else if (pPlayer->field_52C > 0x800)
					{
						pPlayer->field_52C = pPlayer->field_52C - 0x800;
					}
					else
					{
						pPlayer->field_52C = 0;
					}

					if (pPlayer->field_52C != 0)
					{
						pPlayer->field_538 = SFX_Play(
								0xC004 + (pPlayer->field_534 != 0 ? 1 : 0),
								static_cast<i16>(pPlayer->field_52C),
								0);
						gFightMusicIntroDone = 0;
						gFightMusicVariant = (pPlayer->field_534 != 0);
						gFightMusicIntroIndex = 0;
						pPlayer->field_530 = 0;
					}
				}

				if (pPlayer->field_C69 != 0)
				{
					SFX_SetVoiceVolume(pPlayer->field_538,
							static_cast<i16>((pPlayer->field_52C * pPlayer->field_C64) >> 12));
					if (pPlayer->field_C64 >= 0x1000)
						pPlayer->field_C69 = 0;
				}
				else if (pPlayer->field_C68 != 0)
				{
					SFX_SetVoiceVolume(pPlayer->field_538,
							static_cast<i16>((pPlayer->field_C64 * pPlayer->field_52C) >> 12));
				}
			}
		}
	}
	else if (pIds != 0)
	{
		i32 timer = G_SFX_GLOBAL;
		i32 wasIdle = (timer == 6666);
		u32 index = static_cast<u32>(gMusicTrackIndex);

		timer += delta;
		G_SFX_GLOBAL = timer;

		if (!wasIdle
				&& static_cast<u32>(timer) <= static_cast<u32>(pTimes[index]))
		{
			// within 3 vblanks of the end, let the bosses switch phase
			if (static_cast<u32>(pTimes[index]) - static_cast<u32>(timer) <= 3)
				gMusicTrackEnding = 1;
		}
		else
		{
			index++;
			gMusicTrackIndex = static_cast<i32>(index);
			if (index >= count)
			{
				index = static_cast<u32>(wrapIndex);
				gMusicTrackIndex = wrapIndex;
			}

			SFX_Play(static_cast<u32>(pIds[index] | 0xC000), 0x2000, 0);
			G_SFX_GLOBAL = 0;
		}
	}

	gMusicLastVblank = G_VBLANKS;
}
