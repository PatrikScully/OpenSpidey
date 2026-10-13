#include "game/actors/mysterio.h"
#include "compatibility/validate.h"
#include "engine/audio/ps2redbook.h"
#include "engine/input/ps2pad.h"
#include "engine/world/trig.h"
#include "engine/world/ai.h"
#include "core/utils.h"
#include "game/ui/panel.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/rendering/m3dutils.h"
#include "engine/world/collision.h"
#include "game/player/spidey.h"
#include "game/ui/front.h"
#include "engine/rendering/pal.h"
#include "core/game_heap.h"
#include "engine/assets/spool.h"
#include "engine/world/bit2.h"

extern struct tag_S_Pal *pPaletteList;
extern CBody *MiscList;
extern i32 gShellMysterioRelated;

EXPORT SLight M3d_MysterioLight =
{
  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },
  0,

  { { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
  0,
  { 1800, 1800, 1440 }
};



#ifndef SPIDEY_STANDALONE
EXPORT SHandle gMystHandle;
#else
extern SHandle gMystHandle;
#endif

// tentative, address not in the maintainer's IDB. Guards "only one
// CFadePalettes at a time" (message: "Tried to create two global fade
// palettes"), set here and cleared in the destructor.
EXPORT i32 gFadePalettesActive;

// tentative, address not in the maintainer's IDB. Saved/restored triple of
// RGB shift values, read by the constructor, written back by the destructor.
EXPORT u8 gPaletteFadeRGB[3];
EXPORT u8 gPaletteFadeRGB2[3];

// @Ok
// Verified functionally against 0x45bc70 field by field: field_458/9/A =
// arg>>3, field_45C=1, field_45D/E/F loaded from gPaletteFadeRGB (matches
// byte_56FB79/7A/7B in the disasm), the pPaletteList walk (Clut>>6 for hi,
// (Clut&0x3F)<<4 for lo, flags&1 picks the 16C vs 256C block, DCMem_New
// sizes 0x44/0x404 match, StoreImage() called twice per palette matches the
// two guarded sub_46CB90 debug-stub calls, DrawSync() once at the end).
// cmpsum still shows 68 mnemonic diffs (register/stack scheduling in the
// walk loop only, no offset or logic mismatch); left as register-allocation
// residue per this session's functional-only bar.
CFadePalettes::CFadePalettes(u8 a1, u8 a2, u8 a3)
{
	print_if_false(gFadePalettesActive == 0, "Tried to create two global fade palettes");
	gFadePalettesActive = 1;

	this->field_458 = a1 >> 3;
	this->field_459 = a2 >> 3;
	this->field_45A = a3 >> 3;
	this->field_45C = 1;

	this->field_45D = gPaletteFadeRGB[0];
	this->field_45E = gPaletteFadeRGB[1];
	this->field_45F = gPaletteFadeRGB[2];

	tag_S_Pal * volatile pPal = pPaletteList;
	if (pPal)
	{
		do
		{
			u16 hi = pPal->Clut >> 6;
			u8 flags = pPal->flags;
			u16 lo = (pPal->Clut & 0x3F) << 4;

			if (flags & 1)
			{
				print_if_false(this->field_450 < 0xC0, "More 16C palettes used than expected");
				this->field_3C[this->field_450] = DCMem_New(0x44, 1, 1, 0, 1);
				*reinterpret_cast<u16*>(this->field_3C[this->field_450]) = lo;
				*reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_3C[this->field_450]) + 2) = hi;
				StoreImage();
				StoreImage();
				this->field_450++;
			}
			else
			{
				print_if_false(this->field_454 < 0x44, "More 256C palettes used than expected");
				this->field_33C[this->field_454] = DCMem_New(0x404, 1, 1, 0, 1);
				*reinterpret_cast<u16*>(this->field_33C[this->field_454]) = lo;
				*reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_33C[this->field_454]) + 2) = hi;
				StoreImage();
				StoreImage();
				this->field_454++;
			}

			pPal = pPal->pNext;
		} while (pPal);
	}

	DrawSync();
}

// @Ok
// @Matching
 void CFadePalettes::FadeDown(void)
{
	if (this->field_45B != 1 && this->field_45B != 3)
	{
		this->mAge = 0;
		this->field_45B = 1;
	}
}

static u8 * const gPSXRegionActiveFlags = (u8*)0x6B244A;
// tentative, not in the maintainer's IDB. Nearest named neighbours are
// PSXRegion at 0x6B2440 and CItemRelatedList's identical "index*17" region
// table at 0x6B2454 (ob.h, also used by CMysterio's ctor and platform.cpp);
// guessing a byte flag per region slot with the same stride and indexing.

// @Ok
// Re-traced the full function against 0x45c030 field by field (IDA
// decompile + disasm). Found and fixed a real algorithm bug in the old
// version: the per-pixel blend loops for field_45B == 0 (case 0 below) do
// NOT fade pHi[j] toward a second "current" array (pLo[j]); they fade each
// channel toward the FIXED per-instance target color field_458/459/45A
// (the same constant used for the gPaletteFadeRGB update just above), and
// never touch the +4 (pLo) offset at all. The field_45B == 1 blend loops
// (case 1) are the mirror: they DO fade pHi[j] toward pLo[j] (the original
// per-pixel snapshot), confirmed unchanged against the disasm. So the two
// phases are asymmetric: phase 0 pushes every pixel toward one flat color,
// phase 1 restores each pixel's own original color. The old code used the
// pLo-vs-pHi shape for both phases, which was wrong for phase 0.
// Other confirmed offsets/shapes: field_450/454 counts, field_3C (+0x3C)
// and field_33C (+0x33C) bases, +0x24/+0x204 pHi offsets, the
// gPaletteFadeRGB/gPaletteFadeRGB2 dual write, mAge (0xC) and field_45B
// (0x45B) transition targets (0 -> 2, 1 -> 3), case 3 -> Die(). Register
// allocation/scheduling not chased (cmpsum still shows diffs); functional
// bar only per this session.
void CFadePalettes::Move(void)
{
	print_if_false(
			gPSXRegionActiveFlags[this->field_44C * 17 * 4] != 0,
			"Region became unusable");

	switch (this->field_45B)
	{
		case 0:
		{
			if (this->mAge > 0xB)
			{
				this->mAge = 0;
				this->field_45B = 2;
				return;
			}
			this->mAge++;

			if (this->field_45C)
			{
				i32 r = gPaletteFadeRGB[0] >> 3;
				i32 g = gPaletteFadeRGB[1] >> 3;
				i32 b = gPaletteFadeRGB[2] >> 3;

				i32 tr = this->field_458;
				i32 tg = this->field_459;
				i32 tb = this->field_45A;

				if (r > tr) { r -= 3; if (r < tr) r = tr; }
				else { r += 3; if (r > tr) r = tr; }

				if (g > tg) { g -= 3; if (g < tg) g = tg; }
				else { g += 3; if (g > tg) g = tg; }

				if (b > tb) { b -= 3; if (b < tb) b = tb; }
				else { b += 3; if (b > tb) b = tb; }

				gPaletteFadeRGB[0] = static_cast<u8>(r << 3);
				gPaletteFadeRGB[1] = static_cast<u8>(g << 3);
				gPaletteFadeRGB[2] = static_cast<u8>(b << 3);

				gPaletteFadeRGB2[0] = static_cast<u8>(r << 3);
				gPaletteFadeRGB2[1] = static_cast<u8>(g << 3);
				gPaletteFadeRGB2[2] = static_cast<u8>(b << 3);
			}

			{
				i32 i;

				for (i = 0; i < this->field_450; i++)
				{
					u16 *pHi = reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_3C[i]) + 0x24);

					for (i32 j = 0; j < 16; j++)
					{
						u16 target = pHi[j];

						if (target & 0x7FFF)
						{
							i32 tr = target & 0x1F;
							i32 tg = (target >> 5) & 0x1F;
							i32 tb = (target >> 10) & 0x1F;

							i32 gr = this->field_458;
							i32 gg = this->field_459;
							i32 gb = this->field_45A;

							if (tr > gr) { tr -= 3; if (tr < gr) tr = gr; }
							else { tr += 3; if (tr > gr) tr = gr; }

							if (tg > gg) { tg -= 3; if (tg < gg) tg = gg; }
							else { tg += 3; if (tg > gg) tg = gg; }

							if (tb > gb) { tb -= 3; if (tb < gb) tb = gb; }
							else { tb += 3; if (tb > gb) tb = gb; }

							pHi[j] = static_cast<u16>((tb << 10) | (tg << 5) | tr | (target & 0x8000));
						}
					}

					_LoadImage();
				}

				for (i = 0; i < this->field_454; i++)
				{
					u16 *pHi = reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_33C[i]) + 0x204);

					for (i32 j = 0; j < 256; j++)
					{
						u16 target = pHi[j];

						if (target & 0x7FFF)
						{
							i32 tr = target & 0x1F;
							i32 tg = (target >> 5) & 0x1F;
							i32 tb = (target >> 10) & 0x1F;

							i32 gr = this->field_458;
							i32 gg = this->field_459;
							i32 gb = this->field_45A;

							if (tr > gr) { tr -= 3; if (tr < gr) tr = gr; }
							else { tr += 3; if (tr > gr) tr = gr; }

							if (tg > gg) { tg -= 3; if (tg < gg) tg = gg; }
							else { tg += 3; if (tg > gg) tg = gg; }

							if (tb > gb) { tb -= 3; if (tb < gb) tb = gb; }
							else { tb += 3; if (tb > gb) tb = gb; }

							pHi[j] = static_cast<u16>((tb << 10) | (tg << 5) | tr | (target & 0x8000));
						}
					}

					_LoadImage();
				}
			}

			break;
		}

		case 1:
		{
			if (this->mAge > 0xB)
			{
				this->mAge = 0;
				this->field_45B = 3;
				return;
			}
			this->mAge++;

			if (this->field_45C)
			{
				i32 r = gPaletteFadeRGB[0] >> 3;
				i32 g = gPaletteFadeRGB[1] >> 3;
				i32 b = gPaletteFadeRGB[2] >> 3;

				i32 tr = this->field_45D;
				i32 tg = this->field_45E;
				i32 tb = this->field_45F;

				if (r > tr) { r -= 3; if (r < tr) r = tr; }
				else { r += 3; if (r > tr) r = tr; }

				if (g > tg) { g -= 3; if (g < tg) g = tg; }
				else { g += 3; if (g > tg) g = tg; }

				if (b > tb) { b -= 3; if (b < tb) b = tb; }
				else { b += 3; if (b > tb) b = tb; }

				gPaletteFadeRGB[0] = static_cast<u8>(r << 3);
				gPaletteFadeRGB[1] = static_cast<u8>(g << 3);
				gPaletteFadeRGB[2] = static_cast<u8>(b << 3);

				gPaletteFadeRGB2[0] = static_cast<u8>(r << 3);
				gPaletteFadeRGB2[1] = static_cast<u8>(g << 3);
				gPaletteFadeRGB2[2] = static_cast<u8>(b << 3);
			}

			{
				i32 i;

				for (i = 0; i < this->field_450; i++)
				{
					u16 *pLo = reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_3C[i]) + 4);
					u16 *pHi = reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_3C[i]) + 0x24);

					for (i32 j = 0; j < 16; j++)
					{
						u16 target = pHi[j];

						if (target & 0x7FFF)
						{
							u16 current = pLo[j];

							i32 tr = target & 0x1F;
							i32 tg = (target >> 5) & 0x1F;
							i32 tb = (target >> 10) & 0x1F;

							i32 cr = current & 0x1F;
							i32 cg = (current >> 5) & 0x1F;
							i32 cb = (current >> 10) & 0x1F;

							if (tr > cr) { tr -= 3; if (tr < cr) tr = cr; }
							else { tr += 3; if (tr > cr) tr = cr; }

							if (tg > cg) { tg -= 3; if (tg < cg) tg = cg; }
							else { tg += 3; if (tg > cg) tg = cg; }

							if (tb > cb) { tb -= 3; if (tb < cb) tb = cb; }
							else { tb += 3; if (tb > cb) tb = cb; }

							pHi[j] = static_cast<u16>((tb << 10) | (tg << 5) | tr | (target & 0x8000));
						}
					}

					_LoadImage();
				}

				for (i = 0; i < this->field_454; i++)
				{
					u16 *pLo = reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_33C[i]) + 4);
					u16 *pHi = reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_33C[i]) + 0x204);

					for (i32 j = 0; j < 256; j++)
					{
						u16 target = pHi[j];

						if (target & 0x7FFF)
						{
							u16 current = pLo[j];

							i32 tr = target & 0x1F;
							i32 tg = (target >> 5) & 0x1F;
							i32 tb = (target >> 10) & 0x1F;

							i32 cr = current & 0x1F;
							i32 cg = (current >> 5) & 0x1F;
							i32 cb = (current >> 10) & 0x1F;

							if (tr > cr) { tr -= 3; if (tr < cr) tr = cr; }
							else { tr += 3; if (tr > cr) tr = cr; }

							if (tg > cg) { tg -= 3; if (tg < cg) tg = cg; }
							else { tg += 3; if (tg > cg) tg = cg; }

							if (tb > cb) { tb -= 3; if (tb < cb) tb = cb; }
							else { tb += 3; if (tb > cb) tb = cb; }

							pHi[j] = static_cast<u16>((tb << 10) | (tg << 5) | tr | (target & 0x8000));
						}
					}

					_LoadImage();
				}
			}

			break;
		}

		case 3:
			this->Die();
			break;

		default:
			break;
	}
}

// @Ok
// @Matching
CFadePalettes::~CFadePalettes(void)
{
	if (this->field_45B != 3)
	{
		for (i32 i = 0; i < this->field_450; i++)
		{
			_LoadImage();
		}

		for (i32 j = 0; j < this->field_454; j++)
		{
			_LoadImage();
		}
	}

	DrawSync();

	for (i32 k = 0; k < this->field_450; k++)
	{
		Mem_Delete(this->field_3C[k]);
	}

	for (i32 l = 0; l < this->field_454; l++)
	{
		Mem_Delete(this->field_33C[l]);
	}

	if (this->field_45C)
	{
		gPaletteFadeRGB[0] = this->field_45D;
		gPaletteFadeRGB[1] = this->field_45E;
		gPaletteFadeRGB[2] = this->field_45F;

		gPaletteFadeRGB2[0] = this->field_45D;
		gPaletteFadeRGB2[1] = this->field_45E;
		gPaletteFadeRGB2[2] = this->field_45F;
	}

	gFadePalettesActive = 0;
}


// @Ok
// @Matching
void Mysterio_FadePalettesUp(const u32* a1, u32*)
{
	void* v2 = Mem_RecoverPointer(&gMystHandle);
	print_if_false(v2 == 0, "Tried to do two fade ups");

	gMystHandle = Mem_MakeHandle(new CFadePalettes(a1[0], a1[1], a1[2]));
}

// @Ok
// @Matching
void Mysterio_FadePalettesDown(const u32*, u32*)
{
	CFadePalettes *pFade = static_cast<CFadePalettes*>(Mem_RecoverPointer(&gMystHandle));
	if (pFade)
		pFade->FadeDown();
}

// @Ok
// @Matching
void Mysterio_RelocatableModuleClear(void)
{
	CItem *pSearch = BaddyList;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 311)
			delete pSearch;

		pSearch = pNext;
	}

	gSuperItemRelated = 1;
}

// @Ok
// @Matching
void Mysterio_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Mysterio_RelocatableModuleClear;

	pMod->field_C[0] = Mysterio_CreateMysterio;
	pMod->field_C[1] = Mysterio_FadePalettesUp;
	pMod->field_C[2] = Mysterio_FadePalettesDown;

	gSuperItemRelated = 0;
	gFloatSuperRelated = 1.0f;
}

// @Bogus
// No out-of-line address in names.json: the only call site is fully
// inlined into CSoftSpot::Hit (0x45F940, still unimplemented in this
// file), matching the CMystFoot precedent above (checked via IDA: only 2
// xrefs to this class's vtable in the whole binary, one is this
// constructor's inlined body, the other is the destructor). Read straight
// off the disasm of that inline block: pBody is stored as a handle,
// field_44 gets the raw i32 arg, then a CSmokeGenerator is spawned at
// pBody's position and marked mProtected (same idiom as
// CMysterioHeadGlow below). The 84-byte Mem_New for the smoke generator
// is checked for failure (field_48 = 0 in that case) but the following
// mProtected store still dereferences it unconditionally, an original
// bug reproduced here rather than fixed.
CDamagedSoftSpotEffect::CDamagedSoftSpotEffect(CBody *pBody, i32 a2)
{
	print_if_false(pBody != 0, "NULL pBody sent to CDamagedSoftSpotEffect");

	this->field_3C = Mem_MakeHandle(pBody);
	this->field_44 = a2;

	CSmokeGenerator *pSmoke = new CSmokeGenerator(
			&pBody->mPos, 0xFFFF, 2, 128, 128, 128, 20, 10, 1000, 700);

	this->field_48 = pSmoke;
	pSmoke->mProtected = 1;
}

// @Ok
// field_48 is a CSmokeGenerator* (set in the constructor above), deleted
// here through its own virtual destructor. Matches 0x45aff0: read field_48
// (offset 0x48), call its vtable scalar-deleting-destructor slot if
// non-null, then chain to the base class destructor (compiler generated).
CDamagedSoftSpotEffect::~CDamagedSoftSpotEffect(void)
{
	delete this->field_48;
}

static CVector * const stru_56F1B4 = (CVector*)0x56F1B4;
// same player-relative reference point used by spidey.cpp (stru_56F1B4)
// and CPlayer::RenderLookaroundReticle; file-local copy, address only.

static i16 * const word_610C48 = (i16*)0x610C48;
static i16 * const word_610C4A = (i16*)0x610C4A;

// @Ok
// Full disasm trace (0x45A010-0x45A4A0) via IDA. Found and fixed a real
// bug in the old version: all four blend expressions (mPosC offset, mVel,
// posOffset, cornerOffset) were written as "down + X * (Y * toCam)", a
// triple nested multiply plus an addend. The actual call sequence at each
// site is two SEPARATE multiplies, X*down and Y*toCam, then ONE add of
// the two results: "X * down + Y * toCam" (confirmed by tracing the
// pushed operands of each sub_4E77D0/sub_4E7720 call: e.g. for mPosC,
// call1 = d1*toCam into a temp, call2 = d2*down into a temp, call3 =
// operator+(lhs=call2 result, rhs=call1 result)). "down" is a multiply
// operand, not a plain addend. mPos/mPosD/mPosB/cornerOffset's use as
// plain operator+/- operands was already correct and unchanged. Verified
// mPosC/mVel/mPos/mPosB/mPosD offsets (0x48/0x1C/0x10/0x3C/0x54) against
// bit.h/bit.cpp VALIDATE entries, and mType (0x3B, value 0x24) at the end.
// Uses the CVector free operator* and operator+ exactly as they exist in
// vector.cpp (both only read lhs.vx for every output component, a genuine
// original bug, not something to fix here). cmpsum still shows register/
// stack scheduling diffs; not chased further, functional bar only per
// this session. Earlier attempts logged in
// ~/Documents/spidey-work/wt/CAngrySpark_CAngrySpark.attempts.md.
CAngrySpark::CAngrySpark(CVector *a2)
{
	this->mPosC = *a2;

	CVector down;
	down.vx = 0;
	down.vy = -4096;
	down.vz = 0;

	CVector toCam;
	toCam.vx = stru_56F1B4->vx - (this->mPosC.vx >> 12);
	toCam.vy = stru_56F1B4->vy - (this->mPosC.vy >> 12);
	toCam.vz = stru_56F1B4->vz - (this->mPosC.vz >> 12);

	gte_ldopv1(reinterpret_cast<VECTOR*>(&down));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&toCam));
	gte_op0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&toCam));

	CVector shifted;
	shifted.vx = toCam.vx >> 8;
	shifted.vy = toCam.vy >> 8;
	shifted.vz = toCam.vz >> 8;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&shifted));
	gte_sqr0();

	VECTOR squared;
	gte_stlvnl(&squared);

	i32 mag = M3dMaths_SquareRoot0(squared.vx + squared.vy + squared.vz);

	toCam.vx = (toCam.vx / mag) << 4;
	toCam.vy = (toCam.vy / mag) << 4;
	toCam.vz = (toCam.vz / mag) << 4;

	i32 angle;
	switch (Rnd(4))
	{
		case 0: angle = Rnd(200) - 1024; break;
		case 1: angle = -1024 - Rnd(300); break;
		case 2: angle = 1024 - Rnd(200); break;
		case 3: angle = Rnd(300) + 1024; break;
		default: angle = reinterpret_cast<i32>(this); break;
	}

	i16 sinA = word_610C48[2 * (angle & 0xFFF)];
	i16 cosA = word_610C4A[2 * (angle & 0xFFF)];

	const i32 positionAlongCamera = (100 * sinA) >> 12;

	const i32 positionAlongDown = (100 * cosA) >> 12;

	this->mPosC += positionAlongDown * down + positionAlongCamera * toCam;

	i32 velScale = Rnd(0x46) + 100;

	const i32 velocityAlongCamera = (velScale * sinA) >> 12;

	const i32 velocityAlongDown = (velScale * cosA) >> 12;

	this->mVel = velocityAlongDown * down + velocityAlongCamera * toCam;

	i32 posScale = Rnd(150) + 150;

	const i32 offsetAlongCamera = (posScale * sinA) >> 12;

	const i32 offsetAlongDown = (posScale * cosA) >> 12;

	CVector posOffset = offsetAlongDown * down + offsetAlongCamera * toCam;

	this->mPos = this->mPosC + posOffset;

	i32 rotAngle = (angle + 1024) & 0xFFF;

	i16 sin2 = word_610C48[2 * rotAngle];
	i16 cos2 = word_610C4A[2 * rotAngle];

	const i32 cornerAlongCamera = (50 * sin2) >> 12;

	const i32 cornerAlongDown = (50 * cos2) >> 12;

	CVector cornerOffset = cornerAlongDown * down + cornerAlongCamera * toCam;

	if (Rnd(2))
		this->mPosD = this->mPosC + cornerOffset;
	else
		this->mPosD = this->mPosC - cornerOffset;

	this->mPosB = this->mPosD + posOffset;

	this->SetTexture(0x877E63C8);
	this->SetSemiTransparent();
	this->SetTint(0xFF, 0x64, 0);

	this->mType = 0x24;
}

// @Ok
CAngrySpark::~CAngrySpark(void)
{
}

// @Ok
// not matching but good enough
i32 CMysterio::MonitorAttack(
		i32 a2,
		VECTOR* a3,
		i32 a4)
{
	SHook hook;


	hook.Part.vy = a3->vy;
	i32 res = 0;
	hook.Part.vz = a3->vz;
	hook.Part.vx = a3->vx;

	if (this->field_388 != gAttackRelated - 1)
	{
		M3dUtils_GetDynamicHookPosition(
				reinterpret_cast<VECTOR*>(&this->field_37C),
				this,
				&hook);
	}
	else
	{
		CVector v13;
		CVector v14;

		v13.vx = 0;
		v13.vy = 0;
		v13.vz = 0;

		v14.vx = 0;
		v14.vy = 0;
		v14.vz = 0;

		M3dUtils_GetDynamicHookPosition(
				reinterpret_cast<VECTOR*>(&v13),
				this,
				&hook);

		if (M3dColij_LineToSphere(
					&this->field_37C,
					&v13,
					&v14,
					G_MECHLIST_PLAYER,
					0,
					((G_MECHLIST_PLAYER->mRMinor + a4) << 12) / G_MECHLIST_PLAYER->mRMinor))
		{
			res = 1;
		}

		this->field_37C = v13;
	}

	this->field_388 = gAttackRelated;

	return res;
}

extern CBaddy* BaddyList;

// @Ok
CMysterio::~CMysterio(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&BaddyList));
	gBossRelated = 0;
	Panel_DestroyHealthBar();

	if (this->field_3B8)
		SFX_Stop(this->field_3B8);

	this->field_3B8 = 0;

	if (this->field_324)
		delete this->field_324;


	CItem* first = reinterpret_cast<CItem*>(Mem_RecoverPointer(&this->field_360));
	if (first)
		delete first;

	CItem* second = reinterpret_cast<CItem*>(Mem_RecoverPointer(&this->field_368));
	if (second)
		delete second;



}

// tentative, address not in the maintainer's IDB. Zeroed unconditionally at
// the very top of CMysterio::CMysterio, before InitItem is even called.
// Purpose unknown (no other reader/writer found in this file). Nearest
// named neighbours: gSfxGlobal (0x60D86C, before) and gMystHandle (0x60D9A0,
// after, idb_globals.txt); not proven related to either.
EXPORT i32 gMysterioCtorFlag;

// CMysterioHeadGlow::CMysterioHeadGlow and CSoftSpot::CSoftSpot (both
// @SMALLTODO stubs) are defined in baddy.cpp, not here. mysterio.cpp
// compiles with /Ob2 (auto-inline), so a same-TU printf placeholder for a
// callee of CMysterio::CMysterio would get inlined into it and pollute its
// codegen (CLAUDE.md leaf-first rule); an __asm forward-to-original also
// does not work here, it forces an ebp-based frame for the whole
// translation unit and changes CMysterio::CMysterio's own prologue. Keeping
// the stub bodies in a different .cpp avoids both (cross-TU inlining does
// not happen for functions defined outside headers).

// @Bogus
// Inlined at both "new CMystFoot()" call sites in CMysterio's ctor (no
// out-of-line address in names.json for it). Same InitItem/Spool_GetModel/
// AttachTo/mFlags idiom as CManipOb::CManipOb (manipob.cpp). mType 0x19D
// (413) and the model hash 0x98A81283 are read straight off that disasm.
 CMystFoot::CMystFoot(void)
{
	this->InitItem(G_OBJ_FILE);
	this->mModel = static_cast<u16>(Spool_GetModel(0x98A81283, gObjFileRegion));
	this->AttachTo(&G_ENVIRONMENTAL_OBJECT_LIST);
	this->mType = 0x19D;
	this->mFlags = (this->mFlags & 0xFFFD) | 0x10;
}

// @Ok
// @AlmostMatching: 16 mnemonic diffs left (cmpsum against 0x45c910), all
// inside the trigger-link (CSoftSpot) loop near the end: the
// Trig_GetLinkInfoList call setup, the loop's entry guard (original has a
// redundant je+jle pair we could not reproduce), and one field_32C walking
// pointer that the original keeps live in a register (ebx) for the whole
// loop but our build spills to the stack and reloads. Instruction count
// checked per the "verify byte length" rule: built is 965 bytes / 243
// instructions vs original 955 bytes / 241 (exactly those 2 extra
// spill/reload instructions, not a missing/extra semantic operation).
// 15 distinct hypotheses tried and logged in
// ~/Documents/spidey-work/wt/CMysterio_CMysterio.attempts.md (medium
// bracket, 955 bytes, needs >=15); 4 fixed real diffs (took this from
// ~170 diffs down to 16), the rest made things worse and were reverted.
// Globals: CItemRelatedList (ob.h) region-table poke at construction time
// (bounding box constants 0x400000/0xFE000200/0xFC000400/0xFE000200, offsets
// +8/+0xC/+0x10/+0x14 of CItemRelatedList[mRegion*17][0]); reused from the
// gPSXRegionActiveFlags comment above (also cites this constructor).
CMysterio::CMysterio(i16 *a1, i32 a2)
{
	gMysterioCtorFlag = 0;

	this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a1)));
	this->InitItem("mysterio");

	i32 **pRegionSlot = ((i32***)0x6B2454)[this->mRegion * 17];
	i32 *pRegionEntry = pRegionSlot[0];
	pRegionEntry[2] = 0x400000;
	pRegionEntry[3] = static_cast<i32>(0xFE000200);
	pRegionEntry[4] = static_cast<i32>(0xFC000400);
	pRegionEntry[5] = static_cast<i32>(0xFE000200);

	i32 rnd = Rnd(0x294);
	rnd = ((rnd - 0x528) >> 2) + 0x528;

	this->mFlags |= 0x480;
	this->field_2A8 |= 0x10200;

	this->field_38C = rnd;
	this->field_378 = 2;
	this->field_374 = 0xA;
	this->mpLight = &M3d_MysterioLight;

	this->AttachTo(reinterpret_cast<CBody**>(&BaddyList));

	this->field_1F4 = a2;
	this->mNode = a2;
	this->mType = 311;

	this->RunAnim(0, 0, -1);

	this->field_398 = 0x4B0;

	this->field_324 = reinterpret_cast<CItem*>(new CMysterioHeadGlow(this));
	reinterpret_cast<CMysterioHeadGlow*>(this->field_324)->mProtected = 1;

	this->mHealth = 0x2710;

	Panel_CreateHealthBar(this, 311);

	this->field_360 = Mem_MakeHandle(new CMystFoot());
	this->field_368 = Mem_MakeHandle(new CMystFoot());

	SLinkInfo links[20];
	i32 count = reinterpret_cast<i32>(
			Trig_GetLinkInfoList(this->field_1F4, links, 20));

	if (count > 0)
	{
		CSoftSpot **pSlot = this->field_32C;
		SLinkInfo *pLink = links;

		do
		{
			i32 code = pLink->field_8;

			if (code == 0)
			{
			}
			else if (code == 0xC)
			{
				this->field_3A8 = pLink->field_0;
			}
			else
			{
				print_if_false(code - 1 < 0xB, "Bad mysterio link code");

				if (code - 1 >= 8)
				{
					*(reinterpret_cast<i32*>(&this->field_38C) + (code - 1)) = pLink->field_0;
				}
				else
				{
					print_if_false(*pSlot == 0, "Too many mysterio soft spots");
					*pSlot = new CSoftSpot(this, 100, pLink->field_0, code - 1);
					this->field_358 += 100;
				}
			}

			pLink++;
			pSlot++;
			count--;
		} while (count);
	}
}

// @Bogus
// No out-of-line address in names.json: the only call site is in
// Mysterio_CreateMysterio (0x459f20, same TU), fully inlined there, same
// precedent as CMystFoot's constructor above. Read straight off that
// inlined block (0x459fb7-0x459feb): base ctor call (compiler generated),
// zero field_37C (offset 0x37C, matches VALIDATE below), InitItem("mysterio"),
// mFlags |= 0x480 (offset 4, matches CItem::mFlags), mpLight = &M3d_MysterioLight
// (offset 0x3C, matches CItem::mpLight; unk_54E0F0 in the IDB is named
// M3d_MysterioLight). Order and offsets checked against the disasm.
CMysterio::CMysterio(void)
{
	this->field_37C.vx = 0;
	this->field_37C.vy = 0;
	this->field_37C.vz = 0;

	this->InitItem("mysterio");

	this->mFlags |= 0x480;
	this->mpLight = &M3d_MysterioLight;
}

// @Ok
void Mysterio_CreateMysterio(const unsigned int *stack, unsigned int *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	int v3 = static_cast<int>(stack[1]);

	if (v2)
	{
		*result = reinterpret_cast<unsigned int>(new CMysterio(v2, v3));
	}
	else
	{
		*result = reinterpret_cast<unsigned int>(new CMysterio());
	}
}

// @Ok
void CMysterioLaser::SetDamage(int damage)
{
	this->field_4C = damage;
}

// @Ok
// Read straight off the inlined block at 0x45AB39-0x45AB8D inside
// CMysterioHeadGlow::CMysterioHeadGlow (baddy.cpp). mCBodyFlags |= 0x20 is
// NOT part of this (it happens unconditionally, even on allocation failure,
// right after the caller stores the pointer -- see the comment on that
// function).
CGoldFish::CGoldFish(void)
{
	this->InitItem("goldfish");
	this->mType = 506;
	this->AttachTo(&MiscList);
	this->mFlags |= 0x200;
	this->mAngVel.vy = 50;
	this->mScale.vz = 10000;
	this->mScale.vy = 10000;
	this->mScale.vx = 10000;
}

// @Ok
void CGoldFish::AngryMode(void)
{
	this->field_F8 = 1;
}

// @Ok
void CGoldFish::NormalMode(void)
{
	this->field_F8 = 0;
}

void validate_CMystFoot(void){
	VALIDATE_SIZE(CMystFoot, 0x324);
}

// @Ok
// Read straight off the two inlined blocks at 0x45ABCF-0x45AC3A and
// 0x45AC6C-0x45ACD7 inside CMysterioHeadGlow::CMysterioHeadGlow (baddy.cpp),
// which are identical apart from which of the two circle instances they
// build. mProtected = 1 is NOT part of this (it happens unconditionally,
// even on allocation failure, right after the caller stores the pointer --
// see the comment on that function). Same field_88/gShellMysterioRelated
// idiom as the menu-preview twin, CShellMysterioHeadCircle::CShellMysterioHeadCircle
// (shell.cpp): a signed random "wobble amplitude" that alternates sign every
// other spawn, halved/doubled by NormalMode/AngryMode below.
CMysterioHeadCircle::CMysterioHeadCircle(void)
{
	this->SetTexture(Spool_FindTextureChecksum("Ken'sCircle"));
	this->SetSemiTransparent();

	this->field_88 = Rnd(100) + 100 * gShellMysterioRelated + 50;

	if (gShellMysterioRelated & 1)
		this->field_88 = -this->field_88;

	gShellMysterioRelated++;

	this->mType = 28;
}

// @Ok
void CMysterioHeadCircle::NormalMode(void)
{
	if (this->field_8C)
		this->field_88 >>= 1;
	this->field_8C = 0;
}

// @Ok
void CMysterioHeadCircle::AngryMode(void)
{
	if (!this->field_8C)
		this->field_88 <<= 1;
	this->field_8C = 1;
}

// @Ok
u8  CMysterio::MystRedbook_XAPlayPos(
		i32 a2,
		i32 a3,
		CVector *a4,
		i32 a5)
{
	u8 res = Redbook_XAPlay(a2, a3, a5);
	if (res)
	{
		this->field_3A0 = 0;
		this->field_39C = 480;
	}

	return res;
}

// @Ok
i32 CMysterio::PlayAndAttachXAPlease(
		i32 a2,
		i32 a3,
		CBody* pBody,
		i32 a5)
{
	if (this->MystRedbook_XAPlayPos(a2, a3, &pBody->mPos, a5))
	{
		pBody->AttachXA(a2, a3);
	}
	
	return 0;
}


// @Ok
// @Matching
void CMysterio::ShakePad(void)
{
	if (G_SAVE_GAME.field_7B)
	{
		if (Pad_GetActuatorTime(0, 0) <= 2)
			Pad_ActuatorOn(0, 6, 0, 1);
		if (Pad_GetActuatorTime(0, 1) <= 2)
			Pad_ActuatorOn(0, 10, 1, 0xC8);
	}
}

#include "game/cinematics/camera.h"

// @Ok
// @Validate: when inlined
i32 CMysterio::CheckforCameraShake(i32 a2)
{
	if (this->field_218 & 8 || this->field_218 < a2)
		return 0;

	G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_BIG);

	this->ShakePad();

	this->field_218 |= 8;
	return 1;
}

// @Ok
void CMysterio::EnterP2(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->RunAnim(10, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 1;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

extern i32 DifficultyLevel;

// @Bogus
// No out-of-line address in names.json (INLINE, no separate symbol). Traced
// one inlined call site directly: CMysterio_FireBoobies (0x45d200, not yet
// in this file) calls this right before RotateToOptimalAttackAngle at
// 0x45d517; also called from CMysterio_KickAttack/SwipeAttack/GrabAttack
// (0x45d630/0x45d8e0/0x45dde0, xrefs to RotateToOptimalAttackAngle, not
// checked individually). Fixed a real bug: the hard-difficulty branch
// (DifficultyLevel >= 2) was inverted. The disasm (0x45d4e0-0x45d4f8) is
// "if field_34C == 0, return 12; else if field_350 != 0, return 5; else
// return 12", i.e. both flags set gives the FAST speed 5, same relative
// direction as the easy/normal branches (both set = fast), not the SLOW
// speed 12 the old code returned when both were set.
 i32 CMysterio::GetAttackRotSpeed(void)
{
	if (!DifficultyLevel)
	{
		if (this->field_34C && this->field_350)
		{
			return 2;
		}

		return 5;
	}
	else
	{
		if (DifficultyLevel == 1)
		{
			if (this->field_34C && this->field_350)
			{
				return 3;
			}

			return 9;
		}
		else if (this->field_34C && this->field_350)
		{
			return 5;
		}

		return 12;

	}
}

// @Ok
void CMysterio::SummonAttack(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->RunAnim(10, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->field_218 >= 58)
			{
				Trig_SendPulse(reinterpret_cast<u16*>(
							Trig_GetLinksPointer(this->mNode)));
				this->dumbAssPad++;
			}
			break;
		case 2:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 1;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Bogus
// No out-of-line address in names.json: fully inlined into CMysterio_AI's
// state dispatch (0x45ef10), the "case 1" arm at 0x45f342-0x45f3c1.
// Identified by the exact CAIProc_LookAt constructor args (this, MechList,
// 0, 1, 60, 341, matching sub_401180) and mAnimFinished at offset 0x142
// (ob.h, matches *(byte*)(this+322) in the disasm). Found a real gap
// against the disasm: dumbAssPad==0 also calls RunAnim(8,0,-1) right after
// creating the AI proc, unconditionally, before incrementing dumbAssPad;
// the old source was missing that call. Fixed here.
void CMysterio::LookMenacing(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 1, 60, 341);
			this->RunAnim(8, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->RunAnim(8, 0, -1);
			}
			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
void CMysterio::RotateToOptimalAttackAngle(
		i32 a2,
		i32 a3)
{
	CSVector v7;

	v7.vx = 0;
	v7.vy = 0;
	v7.vz = 0;

	Utils_CalcAim(&v7, &this->mPos, &G_MECHLIST_PLAYER->mPos);

	i32 v4 = v7.vy - this->mAngles.vy;

	if (v4 < -2056)
	{
		v4 += 4096;
	}
	else if (v4 > 2056)
	{
		v4 -= 4096;
	}

	v4 -= a2;
	if (v4 < -2056)
	{
		v4 += 4096;
	}
	else if (v4 > 2056)
	{
		v4 -= 4096;
	}

	new CAIProc_LookAt(
			this,
			v4 + this->mAngles.vy,
			1,
			a3,
			200);
}

void validate_CMysterio(void){
	VALIDATE_SIZE(CMysterio, 0x3D0);

	VALIDATE(CMysterio, field_324, 0x324);
	VALIDATE(CMysterio, field_328, 0x328);
	VALIDATE(CMysterio, field_32C, 0x32C);

	VALIDATE(CMysterio, field_34C, 0x34C);
	VALIDATE(CMysterio, field_350, 0x350);

	VALIDATE(CMysterio, field_358, 0x358);

	VALIDATE(CMysterio, field_360, 0x360);
	VALIDATE(CMysterio, field_368, 0x368);

	VALIDATE(CMysterio, field_374, 0x374);

	VALIDATE(CMysterio, field_378, 0x378);
	VALIDATE(CMysterio, field_37C, 0x37C);

	VALIDATE(CMysterio, field_388, 0x388);

	VALIDATE(CMysterio, field_38C, 0x38C);
	VALIDATE(CMysterio, field_398, 0x398);

	VALIDATE(CMysterio, field_39C, 0x39C);
	VALIDATE(CMysterio, field_3A0, 0x3A0);

	VALIDATE(CMysterio, field_3A8, 0x3A8);

	VALIDATE(CMysterio, field_3B8, 0x3B8);
}

void validate_CSoftSpot(void){
	VALIDATE_SIZE(CSoftSpot, 0x338);

	VALIDATE(CSoftSpot, field_324, 0x324);
	VALIDATE(CSoftSpot, field_328, 0x328);
	VALIDATE(CSoftSpot, field_32c, 0x32c);
	VALIDATE(CSoftSpot, field_330, 0x330);
}

void validate_CMysterioLaser(void)
{
	VALIDATE_SIZE(CMysterioLaser, 0x64);

	VALIDATE(CMysterioLaser, field_4C, 0x4C);
}

void validate_CGoldFish(void)
{
	VALIDATE_SIZE(CGoldFish, 0x110);

	VALIDATE(CGoldFish, field_F8, 0xF8);
}

void validate_CMysterioHeadCircle(void)
{
	VALIDATE_SIZE(CMysterioHeadCircle, 0x90);

	VALIDATE(CMysterioHeadCircle, field_88, 0x88);
	VALIDATE(CMysterioHeadCircle, field_8C, 0x8C);
}

void validate_CFadePalettes(void)
{
	VALIDATE_SIZE(CFadePalettes, 0x460);

	VALIDATE(CFadePalettes, field_3C, 0x3C);
	VALIDATE(CFadePalettes, field_33C, 0x33C);
	VALIDATE(CFadePalettes, field_450, 0x450);
	VALIDATE(CFadePalettes, field_454, 0x454);

	VALIDATE(CFadePalettes, field_458, 0x458);
	VALIDATE(CFadePalettes, field_459, 0x459);
	VALIDATE(CFadePalettes, field_45A, 0x45A);

	VALIDATE(CFadePalettes, field_45B, 0x45B);

	VALIDATE(CFadePalettes, field_45C, 0x45C);
	VALIDATE(CFadePalettes, field_45D, 0x45D);
	VALIDATE(CFadePalettes, field_45E, 0x45E);
	VALIDATE(CFadePalettes, field_45F, 0x45F);
}

void validate_CAngrySpark(void)
{
	VALIDATE_SIZE(CAngrySpark, 0x84);
}

void validate_CDamagedSoftSpotEffect(void)
{
	VALIDATE_SIZE(CDamagedSoftSpotEffect, 0x4C);

	VALIDATE(CDamagedSoftSpotEffect, field_3C, 0x3C);
	VALIDATE(CDamagedSoftSpotEffect, field_44, 0x44);
	VALIDATE(CDamagedSoftSpotEffect, field_48, 0x48);
}
