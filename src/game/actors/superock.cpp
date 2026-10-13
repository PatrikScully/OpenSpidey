#include "game/actors/superock.h"
#include "compatibility/my_patch.h"
#include "engine/rendering/scene_renderer.h"
#include "game/player/spidey.h"
#include "engine/world/trig.h"
#include "engine/audio/ps2lowsfx.h"
#include "core/utils.h"
#include "game/objects/level_one_bomb.h"
#include "engine/assets/spool.h"
#include "game/ui/panel.h"

#include <cmath>

#include "compatibility/validate.h"

EXPORT SLight M3d_SuperOckLight =
{
  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },
  0,
  { { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
  0,
  { 1200, 1200, 960 }
};


#define LEN_SUPER_OCK_TEXS 15
// The 15 boss HUD bar textures. Filled lazily by
// SuperDocOck_DisplayProgressBars and cleared by
// SuperDocOck_RelocatableModuleClear, nothing else in the exe reads them.
// Address read off SuperDocOck_RelocatableModuleClear (0x4CBD90:
// mov edi,6B30DCh / mov ecx,0Fh / rep stosd) and confirmed by the load loop
// at the top of SuperDocOck_DisplayProgressBars (0x4D0E74: mov esi,6B30DCh).
#ifndef SPIDEY_STANDALONE
EXPORT Texture *gSuperDocTexs[15];
#else
extern Texture * gSuperDocTexs[15];
#endif
//#define G_SUPER_DOC_TEXS (gSuperDocTexs)
#define G_SUPER_DOC_TEXS (reinterpret_cast<Texture**>(0x006B30DC))

// The 15 bar texture name strings, 32 bytes each, in .rdata right before the
// bar coordinate table. "VenomChase_Bar_04", "VenomChase_Bar_03" (x4),
// "VenomChase_Bar_LeftEnd", "VenomChase_Bar_RightEnd", "spider_chase",
// "SuperOc" (x6), "CoreTemp_01".
static const char * const gSuperDocOckBarNames = (const char*)0x5579bc;

// Dense table of i32 screen x/y/w/h/color offsets for the five boss HUD bar
// draws, in .rdata right after the name strings. 40 entries (0x557B9C..
// 0x557C3B), followed by the "doc_arms" string. Roles are per-draw, not
// consistent across entries (same class of table as gMenuBoxSlices).
static i32 * const gSuperDocOckBarCoords = (i32*)0x557b9c;

// Screen y-offset added to several bar draws (0x60F76C, a global i32).
static i32 * const gSuperDocOckBarScreenOffset = (i32*)0x60f76c;

// @Ok
// Re-verified line by line against the 0x4D0E70 disassembly (not just Hex-Rays) via idalib. This
// is a single-baddy HUD (FindBaddyOfType(0x135), CSuperDocOck only) - there is no second/Jameson
// bar in this function, unlike Panel_DisplayHealthBar's Scorpion+Jameson pair; every draw call
// below targets the one boss. Logic: 15-texture load loop, MechList->field_1AC bail,
// FindBaddyOfType(0x135) null checks, progress = min(Utils_XZDist, 2048) * 307 / 2048 (0 when
// field_35C set), field_328 += field_80 when !G_POST_WATER_EFFECT then the field_328 > 4
// consume-4 / field_324++ / wrap-at-5 dance, the two print_if_false field_324 range checks, and
// the field_378 > 0xC00 rcossin_tbl screen-shake. field_324/328/35C/378 already added to
// CSuperDocOck (were PADDING). The trailing f32 argument to every DCPanel_Draw*Poly call below
// is a per-call-site literal straight out of the disassembly (1.0/2.0/3.0/4.0/2.999); meaning
// unknown, reproduced as-is (same "unknown tag argument" situation documented in
// Venom_DrawBarPiece above).
//
// Fixed against the disassembly (this instance was previously @NotOk with several transcription
// bugs from an earlier pass, all confirmed wrong by re-reading the raw bytes at each call site,
// not just re-reading Hex-Rays):
// - Bar 3 and Bar 4 draw tags were 2.0f, disasm pushes 0x40400000 (3.0f) at both call sites.
// - Bar 5's draw tag was 0.49996948f (bit pattern of a stray value), disasm pushes 0x403FEF9E,
//   which is exactly the IEEE-754 encoding of the literal 2.999f, not 0.5-ish.
// - The animated-frame loop draw and the second (post-Bar-5) anim draw both had tag 2.0f, disasm
//   pushes 0x40400000 (3.0f) at both.
// - The first DCPanel_DrawFlatShadedPoly call had tag 2.0f, disasm pushes 0x40400000 (3.0f); the
//   second one was already correct at 4.0f.
// - The screen-shake magnitude used `sin ^ ((sin>>31)&0x7FFFFFFF)`, which is not a valid
//   absolute-value formula (e.g. abs(-1) computed as INT_MIN with it). The disassembly's
//   `mask=sin>>31; (sin^mask)-mask` is exactly the repo's existing `my_abs()` macro
//   (export.h), the same idiom used for this exact rcossin_tbl-magnitude pattern elsewhere
//   (e.g. lizman.cpp's roll-factor code); switched to `my_abs(sin)`.
// - `i32 c0 = ((val << 7) - val) >> 12 + 128;` has a C operator-precedence bug: `+` binds
//   tighter than `>>`, so this actually shifted by 140, not 12. Disasm does `sar ecx, 0Ch` then
//   a separate `add ecx, 80h`; added the missing parens: `(((val << 7) - val) >> 12) + 128`.
void SuperDocOck_DisplayProgressBars(const u32*, u32*)
{
	Texture** tex = G_SUPER_DOC_TEXS;
	const char* name = gSuperDocOckBarNames;
	do {
		if (*tex == 0)
			*tex = Spool_FindTextureEntry((char*)name);
		print_if_false(*tex != 0, "No texture");
		name += 32;
		tex++;
	} while (name < (const char*)0x557b9c);

	if (G_MECHLIST_PLAYER->mSynthesizedInputActive != 0)
		return;

	CBaddy* baddy = FindBaddyOfType(0x135);
	if (baddy == 0)
		return;
	if (G_MECHLIST_PLAYER == 0)
		return;

	CSuperDocOck* doc = (CSuperDocOck*)baddy;
	int progress;
	if (doc->field_35C != 0) {
		progress = 0;
	} else {
		int dist = Utils_XZDist(&G_MECHLIST_PLAYER->mPos, &baddy->mPos);
		if (dist > 2048)
			dist = 2048;
		progress = dist * 307 / 2048;
	}

	if (G_POST_WATER_EFFECT == 0)
		doc->field_328 += baddy->mElapsedTicks;
	if (doc->field_328 > 4) {
		doc->field_328 -= 4;
		doc->field_324++;
		if (doc->field_324 > 5)
			doc->field_324 = 0;
	}

	// Bar 1 (G_SUPER_DOC_TEXS[9])
	POLY_FT4* v7 = (POLY_FT4*)Panel_DrawTexturedPoly(G_SUPER_DOC_TEXS[9], 0);
	Panel_SetStretchedScreenCoords(
		gSuperDocOckBarCoords[0] - progress + 446,
		gSuperDocOckBarCoords[1] + *gSuperDocOckBarScreenOffset + 16,
		v7, G_SUPER_DOC_TEXS[9],
		gSuperDocOckBarCoords[2], gSuperDocOckBarCoords[3]);
	DCPanel_DrawTexturedPoly(1.0f, v7, G_SUPER_DOC_TEXS[9], 0);

	// Bar 2 (G_SUPER_DOC_TEXS[8])
	POLY_FT4* v8 = (POLY_FT4*)Panel_DrawTexturedPoly(G_SUPER_DOC_TEXS[8], 0);
	Panel_SetStretchedScreenCoords(
		gSuperDocOckBarCoords[4] + 446,
		gSuperDocOckBarCoords[5] + *gSuperDocOckBarScreenOffset + 16,
		v8, G_SUPER_DOC_TEXS[8],
		gSuperDocOckBarCoords[6], gSuperDocOckBarCoords[7]);
	DCPanel_DrawTexturedPoly(2.0f, v8, G_SUPER_DOC_TEXS[8], 0);

	print_if_false(doc->field_324 >= 0, "Error");
	print_if_false(doc->field_324 < 6, "Error");

	// 19 health pips
	for (int i = 0; i < 342; i += 18) {
		POLY_FT4* v10 = (POLY_FT4*)Panel_DrawTexturedPoly(G_SUPER_DOC_TEXS[0], 0);
		Panel_SetStretchedScreenCoords(
			i + gSuperDocOckBarCoords[8] + 138,
			gSuperDocOckBarCoords[9] + *gSuperDocOckBarScreenOffset + 28,
			v10, G_SUPER_DOC_TEXS[doc->field_324],
			gSuperDocOckBarCoords[10], gSuperDocOckBarCoords[11]);
		DCPanel_DrawTexturedPoly(4.0f, v10, G_SUPER_DOC_TEXS[0], 0);
	}

	// Bar 3 (G_SUPER_DOC_TEXS[6])
	POLY_FT4* v11 = (POLY_FT4*)Panel_DrawTexturedPoly(G_SUPER_DOC_TEXS[6], 0);
	Panel_SetStretchedScreenCoords(
		gSuperDocOckBarCoords[12] + 133,
		gSuperDocOckBarCoords[13] + *gSuperDocOckBarScreenOffset + 28,
		v11, G_SUPER_DOC_TEXS[6],
		gSuperDocOckBarCoords[14], gSuperDocOckBarCoords[15]);
	DCPanel_DrawTexturedPoly(3.0f, v11, G_SUPER_DOC_TEXS[6], 0);

	// Bar 4 (G_SUPER_DOC_TEXS[7])
	POLY_FT4* v12 = (POLY_FT4*)Panel_DrawTexturedPoly(G_SUPER_DOC_TEXS[7], 0);
	Panel_SetStretchedScreenCoords(
		gSuperDocOckBarCoords[16] + 480,
		gSuperDocOckBarCoords[17] + *gSuperDocOckBarScreenOffset + 28,
		v12, G_SUPER_DOC_TEXS[7],
		gSuperDocOckBarCoords[18], gSuperDocOckBarCoords[19]);
	DCPanel_DrawTexturedPoly(3.0f, v12, G_SUPER_DOC_TEXS[7], 0);

	// Anim
	SAnimFrame* anim = Spool_FindAnim("Sp", 1);
	anim += 2;
	i32 shake = 128;
	if (doc->field_378 > 0xC00) {
		i32 idx = (G_TIMER_RELATED << 4) & 0xFFF;
		i32 sin = G_RCOSSIN_TBL[idx].sin;
		i32 abs_sin = my_abs(sin);
		shake = (abs_sin << 7) >> 12;
	}

	i32 var_4 = 0;
	while (var_4 < 112) {
		POLY_FT4* v13 = (POLY_FT4*)Panel_DrawTexturedPoly(anim, 0);
		if (v13 != 0) {
			Panel_SetStretchedScreenCoords(
				gSuperDocOckBarCoords[20] + 470,
				var_4 + gSuperDocOckBarCoords[21] + *gSuperDocOckBarScreenOffset + 90,
				v13, anim,
				gSuperDocOckBarCoords[22], gSuperDocOckBarCoords[23]);
			((u8*)v13)[5] = (u8)shake;
			((u8*)v13)[6] = (u8)shake;
		}
		DCPanel_DrawTexturedPoly(3.0f, v13, anim, 0);
		var_4 += 16;
	}

	// Bar 5 (G_SUPER_DOC_TEXS[14])
	POLY_FT4* v14 = (POLY_FT4*)Panel_DrawTexturedPoly(G_SUPER_DOC_TEXS[14], 0);
	((u8*)v14)[5] = (u8)shake;
	((u8*)v14)[6] = (u8)shake;
	Panel_SetStretchedScreenCoords(
		gSuperDocOckBarCoords[24] + 448,
		gSuperDocOckBarCoords[25] + *gSuperDocOckBarScreenOffset + 55,
		v14, G_SUPER_DOC_TEXS[14],
		gSuperDocOckBarCoords[26], gSuperDocOckBarCoords[27]);
	DCPanel_DrawTexturedPoly(2.999f, v14, G_SUPER_DOC_TEXS[14], 0);

	// Anim 2 (anim+8)
	anim += 2;
	POLY_FT4* v15 = (POLY_FT4*)Panel_DrawTexturedPoly(anim, 0);
	if (v15 != 0) {
		Panel_SetStretchedScreenCoords(
			gSuperDocOckBarCoords[28] + 470,
			gSuperDocOckBarCoords[29] + *gSuperDocOckBarScreenOffset + 200,
			v15, anim,
			gSuperDocOckBarCoords[30], gSuperDocOckBarCoords[31]);
	}
	DCPanel_DrawTexturedPoly(3.0f, v15, anim, 0);

	// Flat shaded polys (field_378 based)
	i32 val = doc->field_378;
	i32 c0 = (((val << 7) - val) >> 12) + 128;
	i32 c1 = ((val << 8) - val) >> 12;
	i32 c2 = ((val << 4) * 7) >> 12;
	i32 h = (val >= 2048) ? ((val - 2048) << 7) >> 11 : 0;

	DCPanel_DrawFlatShadedPoly(3.0f,
		gSuperDocOckBarCoords[32] + 466,
		gSuperDocOckBarCoords[33] - c2 + *gSuperDocOckBarScreenOffset + 202,
		gSuperDocOckBarCoords[34] + 9,
		gSuperDocOckBarCoords[35] + c2,
		(u8)c0, (u8)c1, (u8)h, 0, 0);

	DCPanel_DrawFlatShadedPoly(4.0f,
		gSuperDocOckBarCoords[36] + 466,
		gSuperDocOckBarCoords[37] + *gSuperDocOckBarScreenOffset + 82,
		gSuperDocOckBarCoords[38] + 9,
		gSuperDocOckBarCoords[39] + 120,
		0, 0, 0, 0, 0);
}

// @Ok
// @Matching
void SuperDocOck_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 308)
			delete pSearch;

		pSearch = pNext;
	}

	for (i32 i = 0; i < LEN_SUPER_OCK_TEXS; i++)
	{
		G_SUPER_DOC_TEXS[i] = 0;
	}
}

// @Ok
// @Matching
void SuperDocOck_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = SuperDocOck_RelocatableModuleClear;
	pMod->field_C[0] = SuperDocOck_CreateSuperDocOck;
	pMod->field_C[1] = SuperDocOck_DisplayProgressBars;
}

// @Ok
 i32* CSuperDocOck::GetNewCommandBlock(u32 a1)
{
	i32* res = static_cast<i32*>(DCMem_New(4 * a1, 0, 1, 0, 1));
	res[a1 - 1] = 0;

	if (!this->field_348)
	{
		this->field_348 = res;
	}
	else
	{
		i32* it = this->field_348;
		while (1)
		{
			if (!it[it[1] - 1])
				break;

			it = reinterpret_cast<i32*>(it[it[1] - 1]);
		}

		it[it[1] - 1] = reinterpret_cast<i32>(res);
	}

	return res;
}

// @Ok
i32 CSuperDocOck::Hit(SHitInfo* a2)
{
	if ( this->mHealth < 0 )
		return 0;
	if ( !this->mRMinor )
		return 0;
	if ( this->field_31C.bothFlags != 1024 )
		return 0;

	this->field_218 &= 0xFFFFFF1F;
	this->mHealth -= a2->field_8;

	if ( this->mHealth <= 0 )
	{
		this->PlaySingleAnim(0x1Fu, 0, -1);
		this->field_31C.bothFlags = 0x4000;
		this->dumbAssPad = 0;
		return 1;
	}

	if  (a2->field_0 & 8)
	{
		CSVector v9;
		v9.vx = 0;
		v9.vy = 0;
		v9.vz = 0;

		Utils_CalcAim(&v9, &this->mPos, &(this->mPos + (a2->field_C << 12)));

		i32 v7 = v9.vy - this->mAngles.vy;
		if (v7 < -2048)
		{
			v7 += 4096;
		}
		else if (v7 > 2048)
		{
			v7 -= 4096;
		}

		if (abs(v7) >= 0x600)
		{
			this->field_218 |= 0x80;
		}
		else if  (v7 < -256)
		{
			this->field_218 |= 0x20;
		}
		else if  (v7 > 256)
		{
			this->field_218 |= 0x40;
		}
	}

	this->field_31C.bothFlags = 0x2000;
	this->dumbAssPad = 0;
	return 1;
}

// @Ok
// @Test
void CSuperDocOck::DoPhysics(void)
{
	if (!this->field_338)
	{
			if (this->field_218 & 0x100)
			{
				CSVector v8;
				v8.vx = 0;
				v8.vy = 0;
				v8.vz = 0;

				Utils_CalcAim(&v8, &this->mPos, &this->field_240);

				i16 vx = v8.vx;
				v8.vx = 0;
				Utils_TurnTowards(
					this->mAngles,
					&this->mAngVel,
					&this->mAngAcc,
					v8,
					10);
				v8.vx = vx;

				i32 v5 = abs(this->mAngVel.vy);
				i32 v6;
				if ( v5 >= 64 )
					v6 = 0;
				else
					v6 = (64 - v5) << 6;
				Utils_GetVecFromMagDir(&this->mVel, (v6 * (this->field_374 >> 12)) >> 12, &v8);
			}
			else
			{
				this->mAngVel.vz = 0;
				this->mAngVel.vy = 0;
				this->mAngVel.vx = 0;
				this->mAngAcc.vz = 0;
				this->mAngAcc.vy = 0;
				this->mAngAcc.vx = 0;
			}
	}


	i16 v7 = this->mAngVel.vy + this->mAngAcc.vy;

	this->mAngVel.vx += this->mAngAcc.vx;
	this->mAngVel.vx -= this->mAngVel.vx >> 2;

	this->mAngVel.vy = v7 - (v7 >> 2);
	this->mAngVel.KillSmall();

	for (i32 i = 0; i < this->mElapsedTicks; i++)
	{
		this->mPos += this->mVel;
		this->mAngles += this->mAngVel;
	}

	this->mAngles.KillSmall();
}

// @Ok
void CSuperDocOck::PlaySounds(void)
{
	switch (this->mAnim)
	{
		case 1:
			if (!(this->field_364 & 1) && this->mFrame >= 0)
			{
				SFX_PlayPos((Rnd(3) + 230) | 0x8000, &this->mPos, 0);
				this->field_364 |= 1u;
			}
			else if (!(this->field_364 & 2) && this->mFrame >= 20)
			{
				SFX_PlayPos((Rnd(3) + 230) | 0x8000, &this->mPos, 0);
				this->field_364 |= 2u;
			}
			break;
		case 4:
			if (!(this->field_364 & 1) && this->mFrame >= 0)
			{
				SFX_PlayPos((Rnd(3) + 230) | 0x8000, &this->mPos, 0);
				this->field_364 |= 1u;
			}
			break;
		case 6:
			if (!(this->field_364 & 1) && this->mFrame >= 20)
			{
				SFX_PlayPos((Rnd(3) + 230) | 0x8000, &this->mPos, 0);
				this->field_364 |= 1u;
			}
			break;
	}
}

// @Ok
// @Test
// Not sure what they did in the register array asignment when v2 > v3
// sub i16 but then move i32 and assign i16
void CSuperDocOck::HangAndGetBeaten(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			i16 v5;
			v5 = Rnd(120) + 120;
			print_if_false(1u, "Bad register index");

			this->realRegisterArr[0] = v5;

			this->mCBodyFlags |= 0x10;
			this->mRMinor = 100;
			this->dumbAssPad++;

			break;
		case 1:
			print_if_false(1u, "Bad register index");
			i32 v2 = this->realRegisterArr[0];
			i32 v3 = this->mElapsedTicks;

			if ( v2 > v3 )
			{
				i32 v4;
				v4 = v2 - v3;

				print_if_false(1u, "Bad register index");
				this->realRegisterArr[0] = v4;
			}
			else
			{
				print_if_false(1u, "Bad register index");
				this->realRegisterArr[0] = 0;
				this->mCBodyFlags &= ~0x10u;
				this->mRMinor = 0;
				this->field_364 = 0;
				this->RunAnim(0x18u, 0, -1);
				this->dumbAssPad = 0;
				this->field_31C.bothFlags = 2048;
			}
			break;
	}
}

// @Ok
// @Test
void CSuperDocOck::CreateExplosion(i32 a2, i32)
{
	CVector a3;
	a3.vx = 0;
	a3.vy = 0;
	a3.vz = 0;

	Trig_GetPosition(&a3, a2);
	SFX_PlayPos(0x23u, &a3, 0);

	CSVector v7;
	v7.vx = 0;
	v7.vy = 0;
	v7.vz = 0;
	Utils_CalcAim(&v7, &this->mPos, &a3);

	i32 v4 = v7.vy - this->mAngles.vy;
	if (v4 < -2048)
	{
		v4 += 4096;
	}
	else if (v4 > 2048)
	{
		v4 -= 4096;
	}

	if (abs(v4) >= 0x600)
	{
		this->field_218 |= 0x80;
	}
	else if  (v4 < -256)
	{
		this->field_218 |= 0x20;
	}
	else if  (v4 > 256)
	{
		this->field_218 |= 0x40;
	}

	this->mVel.vz = 0;
	this->mVel.vy = 0;
	this->mVel.vx = 0;
	this->field_218 &= ~0x100;
	this->field_31C.bothFlags = 0x2000;
	this->dumbAssPad = 0;
}

// @Ok
void CSuperDocOck::PlayIdleOrGloatAnim(void)
{
	if ( !(this->field_218 & 0x10))
	{
		if ( !this->field_3D8 )
		{
			this->PlaySingleAnim(22, 0, -1);
			return;
		}

		if ( this->field_3E0 > 600 || G_MECHLIST_PLAYER->mHealth <= 0 )
		{
			if ( this->field_3D4 == 1 )
				this->PlaySingleAnim(35, 0, -1);
			else
				this->PlaySingleAnim(1, 0, -1);

			this->field_3E0 = 0;
			this->field_31C.bothFlags = 0x8000;
			this->dumbAssPad = 0;
		}
		else
		{
			this->PlaySingleAnim(1, 0, -1);
		}
	}
}

// @Ok
void CSuperDocOck::Gloat(void)
{
	if ( this->mAnimFinished )
	{
		this->mAnimSpeed = 0x10000;
		this->PlayIdleOrGloatAnim();
	}
}

// @Ok
 void CSuperDocOck::Initialise(void)
{
	this->field_39C = 455;
	this->field_368 = this->mNode;
	this->field_31C.bothFlags = 0x10000;
	this->dumbAssPad = 0;
}

// @Ok
 void CSuperDocOck::PlaySingleAnim(u32 a2, i32 a3, i32 a4)
{
	this->field_364 = 0;
	this->RunAnim(a2, a3, a4);
}

// @Ok
CSuperDocOck::~CSuperDocOck(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	this->KillAllCommandBlocks();

	delete reinterpret_cast<CItem*>(this->field_360);

	for (i32 i = 0; i< 4; i++)
	{
		if (this->field_3B4[i])
			Mem_Delete(this->field_3B4[i]);

		delete reinterpret_cast<CItem*>(this->field_404[i]);
		delete reinterpret_cast<CItem*>(this->field_3F4[i]);
	}
}

// @Ok
// @AlmostMatching: global assingments slightly out of order
CSuperDocOck::CSuperDocOck(i16 *a2, i32 a3)
{
	this->field_344 = reinterpret_cast<i32>(
			this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2))));

	this->InitItem("superock");
	this->mFlags |= 0x480;
	this->mCBodyFlags &= 0xFFEF;

	this->mpLight = &M3d_SuperOckLight;

	this->mHealth = 500;
	this->mRMinor = 0;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->mType = 309;
	this->field_1F4 = a3;
	this->mNode = a3;

	this->field_36C = 1024;
	this->field_374 = 0x10000;
	this->field_370 = 0x10000;
	this->field_3D8 = 4;
	this->field_31C.bothFlags = 1;
	this->field_21E = 100;

	gBombRelated = 4096;
	gBombAIRelated = 0xFFFFFF;
	gBombDieRelatedTwo = 1;
	gBombDieTimerRelated = G_TIMER_RELATED;

	this->field_194 = 0xFFFE0000;
	this->field_198 = 0x1FFF;
}

// @Ok
void SuperDocOck_CreateSuperDocOck(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CSuperDocOck(v2, v3));
}

// @Ok
void CSuperDocOck::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// @Ok
void CSuperDocOck::RenderClaws(void)
{
	M3d_Render(this->field_3F4[0]);
}

// @Ok
// @Matching
 i32* CSuperDocOck::KillCommandBlock(i32* a1)
{
	i32* res = reinterpret_cast<i32*>(a1[a1[1]-1]);

	if (this->field_348 == a1)
	{
		this->field_348 = res;
	}
	else
	{
		i32* it = this->field_348;

		while (it)
		{
			if (a1 == reinterpret_cast<i32*>(it[it[1]-1]))
			{
				it[it[1]-1] = reinterpret_cast<i32>(res);
				break;
			}

			it = reinterpret_cast<i32*>(it[it[1]-1]);
		}
	}

	Mem_Delete(reinterpret_cast<void*>(a1));
	return res;
}

// @Ok
// @Matching
 void CSuperDocOck::KillAllCommandBlocks(void)
{
	for (int* cur = this->field_348; cur; cur = this->KillCommandBlock(cur))
		;
	this->field_348 = 0;
}

void validate_CSuperDocOck(void){
	VALIDATE_SIZE(CSuperDocOck, 0x414);

	VALIDATE(CSuperDocOck, field_32C, 0x32C);

	VALIDATE(CSuperDocOck, field_338, 0x338);

	VALIDATE(CSuperDocOck, field_344, 0x344);
	VALIDATE(CSuperDocOck, field_348, 0x348);

	VALIDATE(CSuperDocOck, field_360, 0x360);
	VALIDATE(CSuperDocOck, field_364, 0x364);
	VALIDATE(CSuperDocOck, field_368, 0x368);

	VALIDATE(CSuperDocOck, field_36C, 0x36C);
	VALIDATE(CSuperDocOck, field_370, 0x370);
	VALIDATE(CSuperDocOck, field_374, 0x374);

	VALIDATE(CSuperDocOck, field_3B4, 0x3B4);

	VALIDATE(CSuperDocOck, field_3D4, 0x3D4);
	VALIDATE(CSuperDocOck, field_3D8, 0x3D8);

	VALIDATE(CSuperDocOck, field_3E0, 0x3E0);

	VALIDATE(CSuperDocOck, field_3F4, 0x3F4);
	VALIDATE(CSuperDocOck, field_404, 0x404);
}

// @Bogus
// The constructor stays in the exe. Our CSuperDocOck is missing AI, which the
// original vtable at 0x53C4D0 has in slot 2 (CSuperDocOck_AI, 0x4CCF80), so
// stamping our vtable would give the boss CBaddy::AI and it would stand still.
// SuperDocOck_CreateSuperDocOck and SuperDocOck_RelocatableModuleInit go with
// it, they build (or hand out the pointer that builds) that same object.
// Shouldnt_DoPhysics_Be_Virtual is skipped too: the exe has it as a 5 byte jmp
// thunk at 0x4CCDE0 and a hook needs 6.
void patch_superock(void)
{
	PATCH_PUSH_RET(0x004CBD90, SuperDocOck_RelocatableModuleClear);
	PATCH_PUSH_RET(0x004D0E70, SuperDocOck_DisplayProgressBars);

	PATCH_PUSH_RET_POLY(0x004CC080, CSuperDocOck::~CSuperDocOck, "??1CSuperDocOck@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004CCC50, CSuperDocOck::PlaySounds, "?PlaySounds@CSuperDocOck@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004CCD40, CSuperDocOck::RenderClaws, "?RenderClaws@CSuperDocOck@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004CCDF0, CSuperDocOck::DoPhysics, "?DoPhysics@CSuperDocOck@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004CE0D0, CSuperDocOck::PlayIdleOrGloatAnim, "?PlayIdleOrGloatAnim@CSuperDocOck@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004CE3D0, CSuperDocOck::CreateExplosion, "?CreateExplosion@CSuperDocOck@@QAEXHH@Z");
	PATCH_PUSH_RET_POLY(0x004D03E0, CSuperDocOck::HangAndGetBeaten, "?HangAndGetBeaten@CSuperDocOck@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004D0860, CSuperDocOck::Hit, "?Hit@CSuperDocOck@@UAEHPAUSHitInfo@@@Z");
}
