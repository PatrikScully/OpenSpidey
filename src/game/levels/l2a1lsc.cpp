#include "game/levels/l2a1lsc.h"
#include "game/player/spidey.h"

#include "game/objects/level_one_bomb.h"
#include "game/levels/l5a5lsc.h"
#include "core/utils.h"
#include "engine/world/trig.h"
#include "game/ui/panel.h"
#include "engine/assets/spool.h"
#include "engine/rendering/PCGfx.h"
#include "engine/audio/ps2redbook.h"
#include "game/application/SpideyDX.h"
#include "engine/rendering/m3dinit.h"

#define LEN_L2_TEXS 5
EXPORT Texture *gL2a1Texs[LEN_L2_TEXS];

EXPORT u8 gl2a1ArrOne[5];
EXPORT u8 gl2a1ArrTwo[5];

EXPORT i32 gL2A1DifficultyArr[4] = { 0x3F84, 0x3F84, 0x3174, 0x2364 };

// tentative name/address: parallel array right after gL2A1DifficultyArr in
// the original .rdata (0x54B34C, indexed by DifficultyLevel same as
// gL2A1DifficultyArr), values {59, 59, 46, 33}. Used only as the divisor
// that turns "time left" into a bar-fill pixel count in
// L2A1LSC_DisplayProgressBar. Not in idb_globals.txt.
static const i32 gL2A1DifficultyDivisorArr[4] = { 59, 59, 46, 33 };

// tentative name/address: read-only game-memory Y anchor for the l2a1
// "scorpion approach" bar and (later) the training on-screen text, right
// before gBombDieRelatedOne (0x60F771) in the same state block. Not in
// idb_globals.txt.
static i32 * const gL2A1TrainingBarBaseY = (i32*)0x0060F76C;

static const char *const gL2A1TexNames[LEN_L2_TEXS] =
{
	"scorpion_approach",
	"jonah_approach",
	"scorpion_approach_bar",
	"scorpion_approach_bar_leftend",
	"scorpion_approach_bar_rightend",
};

// draws poly with the flat color baked into it by Panel_DrawTexturedPoly
// (r0/g0/b0), the same way DCPanel_DrawTexturedPoly's flags=0 path does,
// but through blend mode 1 (DCGfx_BlendingMode_1) like the original bar
// code uses here, not DCPanel_DrawTexturedPoly's hardcoded mode 0.
// Simplification: the original draws each bar piece twice, with a second
// pass using a mirrored UV/color combination for a corner-to-corner shine;
// that second pass is purely cosmetic (no game state depends on it) and is
// not reproduced here.
// local helper, not a standalone function in the original binary (the
// original repeats this draw sequence inline at each of the 5 call sites
// in L2A1LSC_DisplayProgressBar).
// @Bogus
static void L2A1LSC_DrawBarPiece(Texture *tex, POLY_FT4 *poly, f32 zOffset)
{
	PCGfx_UseTexture(tex->clut, DCGfx_BlendingMode_1);

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	u32 color = 0xFF000000 | (poly->r0 << 16) | (poly->g0 << 8) | poly->b0;

	PCGfx_DrawQPoly2D(
			poly->x0 * scaleX, poly->y0 * scaleY, 0.0f, 0.0f, color,
			poly->x1 * scaleX, poly->y1 * scaleY, 1.0f, 0.0f, color,
			poly->x2 * scaleX, poly->y2 * scaleY, 0.0f, 1.0f, color,
			poly->x3 * scaleX, poly->y3 * scaleY, 1.0f, 1.0f, color,
			zOffset);
}

// @Ok
// address 0x4477f0 (IDA, not in names.json/tools/functions). Functional
// decomp, not byte matched: fires the "scorpion is approaching" countdown
// warning lines over Redbook/XA at 3/4, 1/2, 1/4 and 1/8 of the way through
// gL2A1DifficultyArr[DifficultyLevel] (gWhatIf picks an alternate track
// set), then draws the on-screen bar: a sliding "scorpion" portrait icon
// whose X position tracks how much time is left, a fixed "jonah" portrait,
// a tiled bar track, and a left/right end cap.
// Two purely cosmetic pieces of the original are not reproduced (no game
// state depends on either): a per-portrait "reveal window" pulse driven by
// whether the matching Redbook line (group 8) is currently playing, and,
// per bar piece, a second draw pass with a mirrored UV/color combination
// for a corner-to-corner shine (see L2A1LSC_DrawBarPiece).
void L2A1LSC_DisplayProgressBar(const u32 *,u32 *)
{
	for (i32 t = 0; t < LEN_L2_TEXS; t++)
	{
		if (!gL2a1Texs[t])
			gL2a1Texs[t] = Spool_FindTextureEntry(const_cast<char*>(gL2A1TexNames[t]));
	}

	if (gl2a1ArrOne[0])
	{
		if (!gl2a1ArrTwo[0] && gCarnageXaRelated)
		{
			gl2a1ArrTwo[0] = 1;
			Redbook_XAPlay(8, gWhatIf ? 15 : 9, 0);
		}
	}
	else if ((u32)gBombAIRelated < (u32)(3 * gL2A1DifficultyArr[DifficultyLevel] / 4))
	{
		gl2a1ArrOne[0] = 1;
		Redbook_XAPlay(8, gWhatIf ? 14 : 10, 0);
	}

	if (gl2a1ArrOne[1])
	{
		if (!gl2a1ArrTwo[1] && gCarnageXaRelated)
		{
			gl2a1ArrTwo[1] = 1;
			if (gWhatIf)
				Redbook_XAPlay(8, 13, 0);
		}
	}
	else if ((u32)gBombAIRelated < (u32)(gL2A1DifficultyArr[DifficultyLevel] / 2))
	{
		gl2a1ArrOne[1] = 1;
		Redbook_XAPlay(8, gWhatIf ? 11 : 8, 0);
	}

	if (gl2a1ArrOne[2])
	{
		if (!gl2a1ArrTwo[2] && gCarnageXaRelated)
		{
			gl2a1ArrTwo[2] = 1;
			if (!gWhatIf)
				Redbook_XAPlay(8, 7, 0);
		}
	}
	else if ((u32)gBombAIRelated < (u32)(gL2A1DifficultyArr[DifficultyLevel] / 4))
	{
		gl2a1ArrOne[2] = 1;
		Redbook_XAPlay(8, gWhatIf ? 12 : 6, 0);
	}

	if (gl2a1ArrOne[3])
	{
		if (!gl2a1ArrTwo[3] && gCarnageXaRelated)
		{
			gl2a1ArrTwo[3] = 1;
			Redbook_XAPlay(8, gWhatIf ? 15 : 10, 0);
		}
	}
	else if ((u32)gBombAIRelated < (u32)(gL2A1DifficultyArr[DifficultyLevel] / 8))
	{
		gl2a1ArrOne[3] = 1;
		Redbook_XAPlay(8, gWhatIf ? 14 : 9, 0);
	}

	i32 ramp = 0;
	if ((u32)gBombAIRelated < 0x20)
		ramp = 32 - gBombAIRelated;

	i32 scorpionX = (gL2A1DifficultyArr[DifficultyLevel] - gBombAIRelated) /
			(u32)gL2A1DifficultyDivisorArr[DifficultyLevel] + ramp + 144;

	if (gL2a1Texs[0])
	{
		POLY_FT4 *poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL2a1Texs[0], 0));
		Panel_SetStretchedScreenCoords(scorpionX, *gL2A1TrainingBarBaseY + 11, poly, gL2a1Texs[0], 20, 26);
		L2A1LSC_DrawBarPiece(gL2a1Texs[0], poly, 1.0f);
	}

	if (gL2a1Texs[1])
	{
		POLY_FT4 *poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL2a1Texs[1], 0));
		Panel_SetStretchedScreenCoords(446, *gL2A1TrainingBarBaseY + 11, poly, gL2a1Texs[1], 20, 26);
		L2A1LSC_DrawBarPiece(gL2a1Texs[1], poly, 2.0f);
	}

	if (gL2a1Texs[2])
	{
		for (i32 i = 138; i < 480; i += 18)
		{
			POLY_FT4 *poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL2a1Texs[2], 0));
			Panel_SetStretchedScreenCoords(i, *gL2A1TrainingBarBaseY + 23, poly, gL2a1Texs[2], 12, 10);
			L2A1LSC_DrawBarPiece(gL2a1Texs[2], poly, 4.0f);
		}
	}

	if (gL2a1Texs[3])
	{
		POLY_FT4 *poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL2a1Texs[3], 0));
		Panel_SetStretchedScreenCoords(133, *gL2A1TrainingBarBaseY + 23, poly, gL2a1Texs[3], 4, 8);
		L2A1LSC_DrawBarPiece(gL2a1Texs[3], poly, 3.0f);
	}

	if (gL2a1Texs[4])
	{
		POLY_FT4 *poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL2a1Texs[4], 0));
		Panel_SetStretchedScreenCoords(480, *gL2A1TrainingBarBaseY + 23, poly, gL2a1Texs[4], 4, 8);
		L2A1LSC_DrawBarPiece(gL2a1Texs[4], poly, 3.0f);
	}
}

// @Ok
void L2A1LSC_MonitorTimer(const u32 *,u32 *)
{
	gBombRelated = 4096;
	if (!gBombDieRelatedTwo)
	{
		gBombDieRelatedOne = 1;
		gBombDieRelatedTwo = 1;
		gBombAIRelated = gL2A1DifficultyArr[DifficultyLevel];
		gBombDieTimerRelated = G_TIMER_RELATED;
		gl2a1ArrOne[0] = 0;
		gl2a1ArrOne[1] = 0;
		gl2a1ArrOne[2] = 0;
		gl2a1ArrOne[3] = 0;
		gl2a1ArrOne[4] = 0;
		gl2a1ArrTwo[0] = 0;
		gl2a1ArrTwo[1] = 0;
		gl2a1ArrTwo[2] = 0;
		gl2a1ArrTwo[3] = 0;
		gl2a1ArrTwo[4] = 0;
	}

	if (!gBombAIRelated && !gl2a1ArrOne[4])
	{
		gl2a1ArrOne[4] = 1;

		for (i32 i = 1; i < NumNodes; i++)
		{
			if (*G_OFFSETLIST[i] == 1)
			{
				CVector v18;
				Trig_GetPosition(&v18, i);

				if (v18.vx >> 12 == -419 &&
						v18.vy >> 12 == -2730 &&
						v18.vz >> 12 == -3826 )
				{
					Trig_SendPulseToNode(i);
					return;
				}
			}
		}
		print_if_false(0, "Pulse node not found");
	}
}

// @Ok
// @Matching
void L2A1LSC_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = L2A1LSC_RelocatableModuleClear;
	pMod->field_C[0] = L2A1LSC_MonitorTimer;
	pMod->field_C[1] = L2A1LSC_DisplayProgressBar;
	Spidey_SetUserFunction("l2a1lsc", 1u);
}

// @Ok
// @Note: data ordering is all over the place
void L2A1LSC_RelocatableModuleClear(void)
{
	gBombDieRelatedOne = 0;
	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;

	for (i32 i = 0; i < LEN_L2_TEXS; i++)
	{
		gL2a1Texs[i] = 0;
	}
}
