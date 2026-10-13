#include "game/levels/l5a5lsc.h"

#include "game/player/spidey.h"
#include "game/objects/level_one_bomb.h"
#include "game/ui/panel.h"
#include "engine/world/trig.h"
#include "engine/assets/spool.h"
#include "engine/rendering/scene_renderer.h"
#include "game/actors/baddy.h"
#include "game/objects/powerup.h"
#include "core/utils.h"

EXPORT u32 gL5A5RelatedTwo;

#define LEN_L5A5_TEXS 4
EXPORT Texture *gL5A5Texs[LEN_L5A5_TEXS];

// tentative name/guess: fixed game address 0x60F76C, the 4 bytes right after
// gAnimWebcart's SAnimFrame* (0x60F760, panel.cpp, @Ok @Matching Panel_Init) and
// before gBombDieRelatedOne (0x60F771, l1a3bomb.h). Read-only here, used as a
// vertical scroll offset for the subway progress bar. Not in idb_globals.txt.
static i32 * const gAnimWebcartYOffset = (i32*)0x0060F76C;

struct SL5A5BarRect
{
	i32 x, y, w, h;
};

// read from the original .rdata at 0x54B43C..0x54B47B (SpideyPC.exe), 4 rects of
// {x, y, w, h}, one per bar piece drawn below (Car, Track, LeftEnd, RightEnd).
static const SL5A5BarRect gL5A5BarRects[4] =
{
	{ -2, 0, 26, 10 },
	{  6, 0, 19, 10 },
	{  7, 2, 16, 10 },
	{  6, 2, 16, 10 },
};

// @Ok
// @Matching
void L5A5LSC_DisplayProgressBar(u32 const *,u32 *)
{
	Texture *pTex;

	pTex = gL5A5Texs[0];
	if (!pTex)
	{
		pTex = gL5A5Texs[0] = Spool_FindTextureEntry("SubwayPanel_Track");
	}
	print_if_false(pTex != 0, "Could not find texture");

	pTex = gL5A5Texs[1];
	if (!pTex)
	{
		pTex = gL5A5Texs[1] = Spool_FindTextureEntry("SubwayPanel_RightEnd");
	}
	print_if_false(pTex != 0, "Could not find texture");

	pTex = gL5A5Texs[2];
	if (!pTex)
	{
		pTex = gL5A5Texs[2] = Spool_FindTextureEntry("SubwayPanel_LeftEnd");
	}
	print_if_false(pTex != 0, "Could not find texture");

	pTex = gL5A5Texs[3];
	if (!pTex)
	{
		pTex = gL5A5Texs[3] = Spool_FindTextureEntry("SubwayPanel_Car");
	}
	print_if_false(pTex != 0, "Could not find texture");

	u8 wasArmed = gBombDieRelatedTwo;
	gBombRelated = 4096;

	if (!wasArmed)
	{
		gBombDieRelatedTwo = 1;
		gBombAIRelated = 9000;
		gBombDieTimerRelated = G_TIMER_RELATED;

		if (gWideScreen > 0)
		{
			return;
		}
	}
	else if (!gBombAIRelated)
	{
		Trig_SendPulseToNode(EndLevelNode);
		return;
	}

	POLY_FT4 *poly;
	i32 carOffset = ((9000u - gBombAIRelated) * 176 >> 12) + 0x26;

	poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL5A5Texs[3], 0));
	Panel_SetStretchedScreenCoords(
		gL5A5BarRects[0].x + carOffset,
		gL5A5BarRects[0].y - *gAnimWebcartYOffset + 0xD3,
		poly, gL5A5Texs[3], gL5A5BarRects[0].w, gL5A5BarRects[0].h);
	DCPanel_DrawTexturedPoly(1.0f, poly, gL5A5Texs[3], 0);

	for (i32 i = 0; i < 0x1A4; i += 0x1E)
	{
		poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL5A5Texs[0], 0));
		Panel_SetStretchedScreenCoords(
			i + gL5A5BarRects[1].x + 0x26,
			gL5A5BarRects[1].y - *gAnimWebcartYOffset + 0xD9,
			poly, gL5A5Texs[0], gL5A5BarRects[1].w, gL5A5BarRects[1].h);
		DCPanel_DrawTexturedPoly(4.0f, poly, gL5A5Texs[0], 0);
	}

	poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL5A5Texs[2], 0));
	Panel_SetStretchedScreenCoords(
		gL5A5BarRects[2].x + 0xB,
		gL5A5BarRects[2].y - *gAnimWebcartYOffset + 0xD7,
		poly, gL5A5Texs[2], gL5A5BarRects[2].w, gL5A5BarRects[2].h);
	DCPanel_DrawTexturedPoly(3.0f, poly, gL5A5Texs[2], 0);

	poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(gL5A5Texs[1], 0));
	Panel_SetStretchedScreenCoords(
		gL5A5BarRects[3].x + 0x1CA,
		gL5A5BarRects[3].y - *gAnimWebcartYOffset + 0xD7,
		poly, gL5A5Texs[1], gL5A5BarRects[3].w, gL5A5BarRects[3].h);
	DCPanel_DrawTexturedPoly(3.0f, poly, gL5A5Texs[1], 0);
}

// @Ok
// @AlmostMatching: reorder assignements
void L5A5LSC_RelocatableModuleClear(void)
{
	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;

	for (i32 i = 0; i < LEN_L5A5_TEXS; i++)
	{
		gL5A5Texs[i] = 0;
	}
}

// @Ok
// @AlmostMatching: assignemtn to dietimerelated is slightly off
void L5A5LSC_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = L5A5LSC_RelocatableModuleClear;
	pMod->field_C[0] = L5A5LSC_WiggleSubwayCars;
	pMod->field_C[1] = L5A5LSC_DisplayProgressBar;
	Spidey_SetUserFunction("l5a5lsc", 1u);

	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;
	gL5A5RelatedTwo = 0;
}

// tentative name/address: per-slot Z velocity for the EnviroList wiggle spring
// below (8 slots, one per animated subway car segment). Mutable game data at
// 0x5FB594, not in idb_globals.txt.
#define GL5A5_VEL(i) (*reinterpret_cast<volatile i32*>(0x005FB594 + (i) * 4))

// tentative name/address: per-slot clamp-direction flag for the wiggle spring
// above (8 slots). Sits right before the bar-rect table read in
// L5A5LSC_DisplayProgressBar (0x54B43C); not in idb_globals.txt. Written at
// runtime even though the PE section table puts it in .rdata (SpideyPC.exe
// .rdata VA 0x53B000..0x545FE8).
#define GL5A5_FLAG(i) (*reinterpret_cast<volatile i32*>(0x0054B41C + (i) * 4))

// @Ok
// @AlmostMatching: one instruction, cmpsum 2 mnemonic diffs (the second is a
// decode artifact of the first). MSVC6 folds the EnviroList wiggle loop
// counter into a pointer relative to GL5A5_FLAG's base, so its zero-init
// compiles as "mov edi,54B41Ch" (5 bytes) instead of the original's
// "xor edi,edi" (2 bytes); everything else, including every other
// instruction in this 513-byte function, matches. 15 hypotheses tried:
// (1) initial straight translation, 114 diffs; (2) fixed MechList/EnviroList
// field offset (mPos.vy, not vz, CItem has a vtable pointer so mPos starts
// at offset 8 not 4); (3) hoisted the two node-search magic constants into
// named locals, no change; (4) changed the node-search "found" flag from i32
// to u8, matching its byte-sized stores, 114 -> 9 diffs; (5) rewrote the
// wiggle loop over raw byte-offset pointers instead of array indices,
// 9 -> 29 diffs, reverted; (6) made GL5A5_VEL/GL5A5_FLAG volatile,
// 9 -> 8 diffs; (7) swapped the two globals from "i32 * const" locals to
// function-like macros, no change; (8) split the loop counter into its own
// local declared before a for(;;) with the store-then-bound-check order
// matching the disassembly, 8 -> 5 diffs; (9) moved the counter increment
// before the two array stores (matching the original's post-increment
// addressing), 5 -> 2 diffs; (10) stored through an extra "i32 j = i;" copy
// instead of "i - 1", 2 -> 38 diffs, reverted; (11) swapped GL5A5_FLAG/
// GL5A5_VEL store order, no change; (12) moved the loop counter declaration
// out to function scope, no change; (13) switched the macros to take a
// pre-multiplied byte offset instead of an index, 2 -> 29 diffs, reverted;
// (14) made the loop counter itself volatile, 2 -> 118 diffs, reverted;
// (15) dropped volatile from the two macros, 2 -> 4 diffs, reverted. Settled
// on the (6)+(8)+(9) combination (2 diffs) as the best found.
void L5A5LSC_WiggleSubwayCars(u32 const *,u32 *)
{
	if (G_MECHLIST_PLAYER)
	{
		if (!Rnd(4))
		{
			G_MECHLIST_PLAYER->field_570 = Rnd(0x80) + 0x20;
		}

		if (G_MECHLIST_PLAYER->mPos.vy > 0xCB2000)
		{
			u8 found = 0;
			i32 targetX = (i32)0xFEAB8000;
			i32 targetZ = (i32)0xFE330000;

			for (i32 i = 1; i < NumNodes; i++)
			{
				i16 *node = G_OFFSETLIST[i];

				if (node[0] == 1)
				{
					CVector pos(0, 0, 0);
					Trig_GetPosition(&pos, i);

					if (pos.vx == targetX && pos.vy == 0x532000 && pos.vz == targetZ)
					{
						found = 1;
						Trig_SendPulseToNode(i);
						G_MECHLIST_PLAYER->mPos = pos;
						break;
					}
				}
			}

			print_if_false(found, "No TRG_Drowning node");
		}
	}

	CItem *pEnv = EnviroList;
	i32 i = 0;

	if (pEnv)
	{
		for (;;)
		{
			i32 flag = 0;
			i32 vel = GL5A5_VEL(i);

			if (vel != flag)
			{
				pEnv->mPos.vy -= vel << 12;
			}

			flag = GL5A5_FLAG(i);

			if (!flag)
			{
				vel -= 3 + Rnd(3);
			}
			else
			{
				vel += Rnd(3) + 3;
			}

			if (vel > 11)
			{
				vel = 11;
				flag = 0;
			}
			else if (vel < -11)
			{
				vel = -11;
				flag = 1;
			}

			pEnv->mPos.vy += vel << 12;

			i++;
			GL5A5_VEL(i - 1) = vel;
			GL5A5_FLAG(i - 1) = flag;

			if (i == 8)
			{
				break;
			}

			pEnv = pEnv->mNextItem;
			if (!pEnv)
			{
				break;
			}
		}
	}

	CBaddy *pBaddy = BaddyList;
	while (pBaddy)
	{
		if (pBaddy->mType == 0x13D && pBaddy->field_31C.bothFlags == 0x12)
		{
			pBaddy->field_31C.bothFlags = 0x18;
			pBaddy->dumbAssPad = 0;
		}

		pBaddy = static_cast<CBaddy*>(pBaddy->mNextItem);
	}

	CBody *pPower = PowerUpList;
	while (pPower)
	{
		static_cast<CPowerUp*>(pPower)->field_110.vz += -0x40000;
		pPower = static_cast<CBody*>(pPower->mNextItem);
	}
}
