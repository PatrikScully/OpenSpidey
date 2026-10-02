#include "utils.h"
#include "m3dcolij.h"
#include <cstdlib>
#include <cmath>
#include <string.h>
#include "ps2funcs.h"
#include "baddy.h"
#include "spool.h"
#include "m3dzone.h"
#include "my_assert.h"
#include "ps2redbook.h"
#include "stubs.h"
#include "ps2pad.h"
#include "ps2gamefmv.h"
#include "camera.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif

extern CBody *ControlBaddyList;
extern CBody *PowerUpList;
extern CBody *SuspendedList;
extern CBaddy *BaddyList;

// moved out of export.h 2026-08-27, see the comment there.
// @Ok
void print_if_false(unsigned char cry, char * message, ...) {
	if (!cry) {
		puts(message);
	}
}

// gVlanksRelated was GameFade (0x6B4C9C, IDB name), see G_GAME_FADE in utils.h:
// SpideyMain and Front_DoGameOver wait on it and Utils_VblankProcessing counts it down
#ifndef SPIDEY_STANDALONE
i32 DifficultyLevel;
#else
extern i32 DifficultyLevel;
#endif
// volatile u32 Vblanks;   // see G_VBLANKS in utils.h

EXPORT i32 gUtilsRelatedOne[6];
EXPORT i32 gUtilsRelatedTwo;
EXPORT i32 gUtilsRelatedThree;
EXPORT u16 gUtilsRelatedFour;
EXPORT u16 gUtilsRelatedFive;
EXPORT u16 gUtilsRelatedSix;
EXPORT u16 gUtilsRelatedSeven;

// G_POST_WATER_EFFECT and G_GAME_FADE moved to utils.h (needed by front.cpp
// and screen.cpp too now, one definition in the owning header per the G_*
// placement rule).

const u32 crc32_tab[] = {
	0x00000000, 0x77073096, 0xee0e612c, 0x990951ba, 0x076dc419, 0x706af48f,
	0xe963a535, 0x9e6495a3,	0x0edb8832, 0x79dcb8a4, 0xe0d5e91e, 0x97d2d988,
	0x09b64c2b, 0x7eb17cbd, 0xe7b82d07, 0x90bf1d91, 0x1db71064, 0x6ab020f2,
	0xf3b97148, 0x84be41de,	0x1adad47d, 0x6ddde4eb, 0xf4d4b551, 0x83d385c7,
	0x136c9856, 0x646ba8c0, 0xfd62f97a, 0x8a65c9ec,	0x14015c4f, 0x63066cd9,
	0xfa0f3d63, 0x8d080df5,	0x3b6e20c8, 0x4c69105e, 0xd56041e4, 0xa2677172,
	0x3c03e4d1, 0x4b04d447, 0xd20d85fd, 0xa50ab56b,	0x35b5a8fa, 0x42b2986c,
	0xdbbbc9d6, 0xacbcf940,	0x32d86ce3, 0x45df5c75, 0xdcd60dcf, 0xabd13d59,
	0x26d930ac, 0x51de003a, 0xc8d75180, 0xbfd06116, 0x21b4f4b5, 0x56b3c423,
	0xcfba9599, 0xb8bda50f, 0x2802b89e, 0x5f058808, 0xc60cd9b2, 0xb10be924,
	0x2f6f7c87, 0x58684c11, 0xc1611dab, 0xb6662d3d,	0x76dc4190, 0x01db7106,
	0x98d220bc, 0xefd5102a, 0x71b18589, 0x06b6b51f, 0x9fbfe4a5, 0xe8b8d433,
	0x7807c9a2, 0x0f00f934, 0x9609a88e, 0xe10e9818, 0x7f6a0dbb, 0x086d3d2d,
	0x91646c97, 0xe6635c01, 0x6b6b51f4, 0x1c6c6162, 0x856530d8, 0xf262004e,
	0x6c0695ed, 0x1b01a57b, 0x8208f4c1, 0xf50fc457, 0x65b0d9c6, 0x12b7e950,
	0x8bbeb8ea, 0xfcb9887c, 0x62dd1ddf, 0x15da2d49, 0x8cd37cf3, 0xfbd44c65,
	0x4db26158, 0x3ab551ce, 0xa3bc0074, 0xd4bb30e2, 0x4adfa541, 0x3dd895d7,
	0xa4d1c46d, 0xd3d6f4fb, 0x4369e96a, 0x346ed9fc, 0xad678846, 0xda60b8d0,
	0x44042d73, 0x33031de5, 0xaa0a4c5f, 0xdd0d7cc9, 0x5005713c, 0x270241aa,
	0xbe0b1010, 0xc90c2086, 0x5768b525, 0x206f85b3, 0xb966d409, 0xce61e49f,
	0x5edef90e, 0x29d9c998, 0xb0d09822, 0xc7d7a8b4, 0x59b33d17, 0x2eb40d81,
	0xb7bd5c3b, 0xc0ba6cad, 0xedb88320, 0x9abfb3b6, 0x03b6e20c, 0x74b1d29a,
	0xead54739, 0x9dd277af, 0x04db2615, 0x73dc1683, 0xe3630b12, 0x94643b84,
	0x0d6d6a3e, 0x7a6a5aa8, 0xe40ecf0b, 0x9309ff9d, 0x0a00ae27, 0x7d079eb1,
	0xf00f9344, 0x8708a3d2, 0x1e01f268, 0x6906c2fe, 0xf762575d, 0x806567cb,
	0x196c3671, 0x6e6b06e7, 0xfed41b76, 0x89d32be0, 0x10da7a5a, 0x67dd4acc,
	0xf9b9df6f, 0x8ebeeff9, 0x17b7be43, 0x60b08ed5, 0xd6d6a3e8, 0xa1d1937e,
	0x38d8c2c4, 0x4fdff252, 0xd1bb67f1, 0xa6bc5767, 0x3fb506dd, 0x48b2364b,
	0xd80d2bda, 0xaf0a1b4c, 0x36034af6, 0x41047a60, 0xdf60efc3, 0xa867df55,
	0x316e8eef, 0x4669be79, 0xcb61b38c, 0xbc66831a, 0x256fd2a0, 0x5268e236,
	0xcc0c7795, 0xbb0b4703, 0x220216b9, 0x5505262f, 0xc5ba3bbe, 0xb2bd0b28,
	0x2bb45a92, 0x5cb36a04, 0xc2d7ffa7, 0xb5d0cf31, 0x2cd99e8b, 0x5bdeae1d,
	0x9b64c2b0, 0xec63f226, 0x756aa39c, 0x026d930a, 0x9c0906a9, 0xeb0e363f,
	0x72076785, 0x05005713, 0x95bf4a82, 0xe2b87a14, 0x7bb12bae, 0x0cb61b38,
	0x92d28e9b, 0xe5d5be0d, 0x7cdcefb7, 0x0bdbdf21, 0x86d3d2d4, 0xf1d4e242,
	0x68ddb3f8, 0x1fda836e, 0x81be16cd, 0xf6b9265b, 0x6fb077e1, 0x18b74777,
	0x88085ae6, 0xff0f6a70, 0x66063bca, 0x11010b5c, 0x8f659eff, 0xf862ae69,
	0x616bffd3, 0x166ccf45, 0xa00ae278, 0xd70dd2ee, 0x4e048354, 0x3903b3c2,
	0xa7672661, 0xd06016f7, 0x4969474d, 0x3e6e77db, 0xaed16a4a, 0xd9d65adc,
	0x40df0b66, 0x37d83bf0, 0xa9bcae53, 0xdebb9ec5, 0x47b2cf7f, 0x30b5ffe9,
	0xbdbdf21c, 0xcabac28a, 0x53b39330, 0x24b4a3a6, 0xbad03605, 0xcdd70693,
	0x54de5729, 0x23d967bf, 0xb3667a2e, 0xc4614ab8, 0x5d681b02, 0x2a6f2b94,
	0xb40bbe37, 0xc30c8ea1, 0x5a05df1b, 0x2d02ef8d
};

// @Ok
void Utils_Init(void)
{
	gUtilsRelatedOne[0] = 0x6000000;
	gUtilsRelatedOne[1] = 0xE3000000;
	gUtilsRelatedOne[2] = 0xE407FDFF;
	gUtilsRelatedOne[3] = 0xE5000000;
	gUtilsRelatedOne[4] = 0xE1000640;
	gUtilsRelatedOne[5] = 0xE2000000;
	gUtilsRelatedTwo = 0xE6000000;
	gUtilsRelatedThree = 0x5000000;
	gUtilsRelatedFour = 0;
	gUtilsRelatedFive = 512;
	gUtilsRelatedSix = 0;
	gUtilsRelatedSeven = 512;
}

// counts vsyncs with no pad input since the last press, resets on any input. guess based on use
// alongside Pad_IdleTime in MyVSync (found by scanning tools/functions/*.bin for the address)
static i32 * const gIdleVsyncCount = (i32*)0x006B4CA4;

// reentrancy guard, set for the duration of MyVSync. tentative name, no idb match
static u8 * const gInVsync = (u8*)0x006B4CB8;

// written by PShell_EndTrainingInit/PShell_EndTrainingUpdate. tentative name, no idb match
static i32 * const gTrainingRelated = (i32*)0x0060CFB0;

// @Ok
// @Matching
void MyVSync(void)
{
	*gInVsync = 1;
	G_VBLANKS++;

	if (!G_GAME_FMV_ACTIVE)
		(*gIdleVsyncCount)++;

	if (!G_PAD_IDLE_TIME)
		*gIdleVsyncCount = 0;

	if (!G_POST_WATER_EFFECT && !*gTrainingRelated)
		G_TIMER_RELATED++;

	if (DoVblankProcessing)
		Utils_VblankProcessing();

	*gInVsync = 0;
}

// @Ok
// @Matching
void Pause(i32 Time)
{
	i32 Until = G_VBLANKS + Time;
#ifdef SPIDEY_STANDALONE
	while (G_VBLANKS < Until)
	{
		Plat_Yield();
		Plat_Sleep(1);
	}
#else
	while (G_VBLANKS < Until);
#endif
}

// @Ok
// @Test
void Utils_CalcPerps(CVector * a1,CVector * a2,CVector * a3)
{
	u32 v6 = abs(a1->vx);
	u32 v7 = abs(a1->vy);
	u32 v8 = abs(a1->vz);
	gte_ldopv1(reinterpret_cast<VECTOR*>(a1));
	if ( v6 > v7 || v6 > v8 )
	{
		if ( v7 > v6 || v7 > v8 )
		{
			a2->vx = -a1->vy;
			a2->vy = a1->vx;
			a2->vz = 0;
		}
		else
		{
			a2->vx = a1->vz;
			a2->vy = 0;
			a2->vz = -a1->vx;
		}
	}
	else
	{
		a2->vx = 0;
		a2->vy = -a1->vz;
		a2->vz = a1->vy;
	}

	gte_ldopv2(reinterpret_cast<VECTOR*>(a2));
	gte_op12();
	gte_stlvnl(reinterpret_cast<VECTOR*>(a3));
}

// @Ok
// @Matching
void Utils_CalcUnitFacingCamera(CVector const * a1, CVector const * a2, CVector * a3)
{
	CVector delta1;
	delta1.vx = (a2->vx - a1->vx) >> 12;
	delta1.vy = (a2->vy - a1->vy) >> 12;
	delta1.vz = (a2->vz - a1->vz) >> 12;

	if (delta1.vx > 500 || delta1.vy > 500 || delta1.vz > 500)
	{
		delta1.vx >>= 4;
		delta1.vy >>= 4;
		delta1.vz >>= 4;
	}

	CVector delta2;
	delta2.vx = G_MIKE_CAMERA[0].Position.vx - (a1->vx >> 12);
	delta2.vy = G_MIKE_CAMERA[0].Position.vy - (a1->vy >> 12);
	delta2.vz = G_MIKE_CAMERA[0].Position.vz - (a1->vz >> 12);

	gte_ldopv1(reinterpret_cast<VECTOR*>(&delta1));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&delta2));
	gte_op0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(a3));

	CVector shifted;
	shifted.vx = a3->vx >> 8;
	shifted.vy = a3->vy >> 8;
	shifted.vz = a3->vz >> 8;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&shifted));
	gte_sqr0();

	VECTOR squared;
	gte_stlvnl(&squared);

	i32 mag = M3dMaths_SquareRoot0(squared.vx + squared.vy + squared.vz);

	if (mag < 5)
	{
		a3->vx = 0;
		a3->vy = 0;
		a3->vz = 0;
	}
	else
	{
		a3->vx = (a3->vx / mag) << 4;
		a3->vy = (a3->vy / mag) << 4;
		a3->vz = (a3->vz / mag) << 4;
	}
}

// @Ok
// @Matching
void Utils_CalcWallPerps(CVector * a1,CVector * a2,CVector * a3)
{
	CVector v8;
	v8.vx = 0;
	v8.vy = 4096;
	v8.vz = 0;

	gte_ldopv1(reinterpret_cast<VECTOR*>(a1));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&v8));
	gte_op12();
	gte_stlvnl(reinterpret_cast<VECTOR*>(a2));
	a2->vx <<= 8;
	a2->vy <<= 8;
	a2->vz <<= 8;

	CVector v7;
	v7.vx = 0;
	v7.vy = 0;
	v7.vz = 0;
	v7.vx = a2->vx >> 12;
	v7.vy = a2->vy >> 12;
	v7.vz = a2->vz >> 12;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&v7));
	gte_sqr0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&v7));

	i32 v6 = M3dMaths_SquareRoot0(v7.vx + v7.vy + v7.vz);
	if ( !v6 )
	{
		Utils_CalcPerps(a1, a2, a3);
	}
	else
	{
		a2->vx /= v6;
		a2->vy /= v6;
		a2->vz /= v6;
		gte_ldopv1(reinterpret_cast<VECTOR*>(a2));
		gte_ldopv2(reinterpret_cast<VECTOR*>(a1));
		gte_op12();
		gte_stlvnl(reinterpret_cast<VECTOR*>(a3));
	}
}

// scratch rotation matrix for camera-relative transforms, same address CPlayer::RenderLookaroundReticle
// uses (spidey.cpp, stru_56F224). 0x56F224 falls inside gMikeCamera[1] by struct offset (Focus.pad through
// Angles/Transform, per SCamera's validated layout), so it looks like a coincidental overlap rather than
// a real SCamera field, same class of thing tips.txt warns about ("global boundaries... unreliable").
// Kept as its own address, matching the existing spidey.cpp precedent, instead of indexing into gMikeCamera[1].
static MATRIX * const gCameraViewMatrix = (MATRIX*)0x0056F224;

// 2026-08-30: retagged @Ok under the session's functional-only bar (no byte
// match required). Verified field-by-field against the IDA decompile of
// 0x4E6D90 (347 bytes): dist compare/clamp against a2/a3, atten formula,
// gte_SetRotMatrix call, delta vector build, zero-vector short circuit,
// angle via ratan2(v.vz, v.vx), and the hi/lo pack for angle<2048 vs
// angle>=2048 all match the original one to one. Earlier byte-match attempt
// (16 hypotheses, see wt/utils.attempts.md) left a 21-diff register/prologue
// residue (an edi vs edx callee-saved home) that never blocked correctness.
// @Ok
// @Test
u32 Utils_CalculateSpatialAttenuation(CVector const * a1, i32 a2, i32 a3)
{
	const CVector camPos(
			G_MIKE_CAMERA[0].Position.vx << 12,
			G_MIKE_CAMERA[0].Position.vy << 12,
			G_MIKE_CAMERA[0].Position.vz << 12);
	i32 dist = Utils_CrapDist(*a1, camPos);

	if (dist <= a2)
		return 0xFFF0FFF;

	if (dist >= a3)
		return 0;

	i32 atten = ((a3 - dist) * 4095) / a3;

	gte_SetRotMatrix(gCameraViewMatrix);

	i32 dx = (a1->vx >> 12) - G_MIKE_CAMERA[0].Position.vx;
	i32 dy = (a1->vy >> 12) - G_MIKE_CAMERA[0].Position.vy;
	i32 dz = (a1->vz >> 12) - G_MIKE_CAMERA[0].Position.vz;

	CVector v;
	v.vx = dx;
	v.vy = dy;
	v.vz = dz;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&v));
	gte_rtir();

	gsub_46D9B0(reinterpret_cast<VECTOR*>(&v));

	if (!(v.vz | v.vx))
		return (atten << 16) | atten;

	i32 angle = (1024 - ratan2(v.vz, v.vx)) & 0xFFF;

	i32 hi, lo;
	if (angle < 2048)
	{
		lo = atten - ((G_RCOSSIN_TBL[angle & 0xFFF].sin * (atten / 2)) >> 12);
		hi = atten;
	}
	else
	{
		hi = atten + ((G_RCOSSIN_TBL[angle & 0xFFF].sin * (atten / 2)) >> 12);
		lo = atten;
	}

	return (hi << 16) | lo;
}

// @Ok
// @Matching
i32 Utils_CanSee(
		CItem * a1,
		CItem * a2,
		i32 a3,
		i32 a4,
		i32 a5)
{
	CSVector v8;

	v8.vx = 0;
	v8.vy = 0;
	v8.vz = 0;

	i32 result = Utils_CalcAim(&v8, &a1->mPos, &a2->mPos);
	if ( result > a5 )
		return 0;

	i32 v6 = v8.vy - a1->mAngles.vy;
	if ( v6 < -2048 )
		v6 += 4096;
	if ( v6 > 2048 )
		v6 -= 4096;
	if ( v6 < 0 )
		v6 = -v6;
	if ( v6 > a3 )
		return 0;

	i32 v7 = v8.vx - a1->mAngles.vx;
	if ( v7 < -2048 )
		v7 += 4096;
	if ( v7 > 2048 )
		v7 -= 4096;
	if ( v7 < 0 )
		v7 = -v7;
	if ( v7 > a4 )
		return 0;
	if ( !result )
		return 1;
	return result;
}

// @Ok
// @AlmostMatching: close enough slightly diff operator order
// think it's because of the inline of CVector stuff
u32 Utils_Dist(const CVector &a1, const CVector &a2)
{
	return M3dMaths_SquareRoot0(((a1 - a2) >> 12).SquaredLength());
}

// tentative name: this is dereferenced twice ([ptr+4] then [that+0]) to fill icon metrics
// below. Referenced by Spool_FindTextureEntry/Spool_ReloadAll/Spool_TextureAccess
// (spool.cpp), so it looks like a texture/resource source, but the inner struct fields
// read here (offsets 0, 4, 8, 0xA) are not decoded anywhere else yet.
static void ** const gIconInfoSource = (void**)0x0056EA98;

// unknown HUD/icon table at 0x6B4904..0x6B49E7 (6 slots, 0x28 bytes stride). field layout
// not understood, kept as raw offsets matching the disassembly. every use of
// gIconInfoSource's inner pointer is re-read from memory (not cached, volatile), matching
// the original's 4 separate [ecx+4] reloads per slot.
// 2026-08-30: retagged @Ok under the session's functional-only bar (no byte match required).
// Re-verified every write against the IDA decompile of 0x4E5A20 (361 bytes): all field values
// and their order, including the split arithmetic (esi+esi*4 then *9+0xE) and the memory
// reloads (e.g. re-reading 0x6B492A after storing it), match the original one to one. The
// prior @NotOk was purely a byte-match register-allocation residue (documented history: a
// version without the volatile inner-pointer re-read gave 48 diffs from hoisting, the current
// volatile version gives 43; neither reached a byte match because the original walks the icon
// table with a moving base pointer while this build recomputes each absolute address).
// @Ok
void Utils_InitLoadIcons(void)
{
	char * const info = reinterpret_cast<char*>(*gIconInfoSource);
	// the inner pointer at info+4 is re-read from memory on every use below (volatile),
	// matching the original's repeated "mov edx,[ecx+4]" reloads instead of caching it.
#define ICON_INNER (*reinterpret_cast<i32 * volatile *>(info + 4))

	i16 bx = 0x30;
	i16 di = 0x60;
	i32 ebp = 0x2C202020;
	i32 esi = 0;
	i32 eax = 0;

	do
	{
		*reinterpret_cast<i32*>(eax + 0x006B4920) = 0x9000000;

		i32 dx = esi + esi * 4;
		*reinterpret_cast<i16*>(eax + 0x006B4928) = bx;
		*reinterpret_cast<i16*>(eax + 0x006B4930) = di;
		*reinterpret_cast<i16*>(eax + 0x006B4938) = bx;
		dx = dx + dx * 8 + 0xE;
		*reinterpret_cast<i16*>(eax + 0x006B4940) = di;
		*reinterpret_cast<i16*>(eax + 0x006B492A) = (i16)dx;
		*reinterpret_cast<i16*>(eax + 0x006B4932) = (i16)dx;
		dx = *reinterpret_cast<i16*>(eax + 0x006B492A);
		dx += 0x20;
		*reinterpret_cast<i16*>(eax + 0x006B493A) = (i16)dx;
		*reinterpret_cast<i16*>(eax + 0x006B4942) = (i16)dx;
		*reinterpret_cast<i32*>(eax + 0x006B4924) = ebp;

		eax += 0x28;

		*reinterpret_cast<i32*>(eax + 0x006B4904) = ICON_INNER[0];
		*reinterpret_cast<i32*>(eax + 0x006B490C) = ICON_INNER[1];
		*reinterpret_cast<i16*>(eax + 0x006B4914) = *reinterpret_cast<i16*>(reinterpret_cast<char*>(ICON_INNER) + 8);
		*reinterpret_cast<i32*>(eax + 0x006B49C0) = 0x9000000;
		*reinterpret_cast<i16*>(eax + 0x006B491C) = *reinterpret_cast<i16*>(reinterpret_cast<char*>(ICON_INNER) + 0xA);

		dx = *reinterpret_cast<i16*>(eax + 0x006B4902);
		*reinterpret_cast<i16*>(eax + 0x006B49C8) = bx;
		*reinterpret_cast<i16*>(eax + 0x006B49D0) = di;
		*reinterpret_cast<i16*>(eax + 0x006B49D8) = bx;
		dx += 0x100;
		*reinterpret_cast<i16*>(eax + 0x006B49E0) = di;
		*reinterpret_cast<i16*>(eax + 0x006B49CA) = (i16)dx;

		dx = *reinterpret_cast<i16*>(eax + 0x006B490A);
		dx += 0x100;
		*reinterpret_cast<i16*>(eax + 0x006B49D2) = (i16)dx;

		dx = *reinterpret_cast<i16*>(eax + 0x006B4912);
		dx += 0x100;
		*reinterpret_cast<i16*>(eax + 0x006B49DA) = (i16)dx;

		dx = *reinterpret_cast<i16*>(eax + 0x006B491A);
		dx += 0x100;
		esi++;
		*reinterpret_cast<i16*>(eax + 0x006B49E2) = (i16)dx;
		*reinterpret_cast<i32*>(eax + 0x006B49C4) = ebp;

		*reinterpret_cast<i32*>(eax + 0x006B49CC) = ICON_INNER[0];
		*reinterpret_cast<i32*>(eax + 0x006B49D4) = ICON_INNER[1];
		*reinterpret_cast<i16*>(eax + 0x006B49DC) = *reinterpret_cast<i16*>(reinterpret_cast<char*>(ICON_INNER) + 8);
		*reinterpret_cast<i16*>(eax + 0x006B49E4) = *reinterpret_cast<i16*>(reinterpret_cast<char*>(ICON_INNER) + 0xA);
	}
	while (eax < 0xC8);

#undef ICON_INNER
}

// @Ok
// @Test
void Utils_Jumble(i32 * a1,i32 a2)
{
	i32 v6 = a1[a2 - 1];

	for (i32 i = 0; i < 4 * a2; i++)
	{
		i32 v7 = Rnd(a2);
		i32 v8 = Rnd(a2);
		i32 v9 = a1[v7];

		a1[v7] = a1[v8];
		a1[v8] = v9;
	}

	if ( a2 > 1 && v6 == *a1 )
	{
		i32 v10 = Rnd(a2 - 1);
		i32 v11 = *a1;
		i32 v12 = v10 + 1;

		*a1 = a1[v12];
		a1[v12] = v11;
	}
}

// @Ok
i32 Utils_KillEverythingInBox(CVector const * min,CVector const * max)
{
		return Utils_KillObjectsInBox(min, max, SuspendedList, false) +
		Utils_KillObjectsInBox(min, max, PowerUpList, true) +
		Utils_KillObjectsInBox(min, max, G_ENVIRONMENTAL_OBJECT_LIST, true) +
		Utils_KillObjectsInBox(min, max, ControlBaddyList, true) +
		Utils_KillObjectsInBox(min, max, BaddyList, true);
}

// @Ok
i32 Utils_KillObjectsInBox(CVector const * min,CVector const * max,CBody * a3, bool visible)
{
	i32 killed = 0;
	for (CBody *cur = a3; cur; )
	{
		CBody *next = reinterpret_cast<CBody*>(cur->mNextItem);

		if (!cur->IsDead())
		{
			i32 vx = cur->mPos.vx;
			i32 vy = cur->mPos.vy;
			i32 vz = cur->mPos.vz;

			if (vx >= min->vx && vx <= max->vx &&
				vy >= min->vy && vy <= max->vy &&
				vz >= min->vz && vz <= max->vz)
			{
				if (visible)
				{
					cur->Die();
				}
				else
				{
					delete cur;
				}

				killed++;
			}
		}



		cur = next;
	}

	return killed;
}

// @Ok
void Utils_RotateWorldToObject(CBody * a1, CVector * a2, CVector * a3)
{
	MATRIX mOne;
	MATRIX mtwo;

	M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR *>(&a1->mAngles), &mtwo);
	M3dMaths_TransposeMatrix1(&mtwo, &mOne);

	SVECTOR sVec;
	sVec.vz = a2->vz >> 12;
	sVec.vx = a2->vx >> 12;
	sVec.vy = a2->vy >> 12;

	memcpy(G_ROT_MATRIX, &mOne, sizeof(G_ROT_MATRIX));

	MTC2(*reinterpret_cast<i32*>(&sVec.vx), GT_ZERO);
	MTC2(*reinterpret_cast<i32*>(&sVec.vz), GT_ONE);

	gte_mvmva(1, 0, 0, 3, 0);
	gte_stsv(&sVec);

	a3->vx = sVec.vx << 12;
	a3->vy = sVec.vy << 12;
	a3->vz = sVec.vz << 12;
}

// @Ok
void Utils_SetBaddyVisibilityInBox(CVector const * min,CVector const * max,bool visible,bool in,CBody * a5)
{
	i32 minvx = min->vx;
	i32 minvy = min->vy;
	i32 minvz = min->vz;

	i32 maxvx = max->vx;
	i32 maxvy = max->vy;
	i32 maxvz = max->vz;

	for(CItem *pItem = a5; pItem; pItem = pItem->mNextItem)
	{
		if (PSXRegion[pItem->mRegion].Usable)
		{
			i32 vx = pItem->mPos.vx;

			if (in)
			{
				if (vx >= minvx && vx <= maxvx &&
						pItem->mPos.vy >= minvy && pItem->mPos.vy <= maxvy &&
						pItem->mPos.vz >= minvz && pItem->mPos.vz <= maxvz)
				{
					if (visible)
					{
						pItem->mFlags &= ~1;
					}
					else
					{
						pItem->mFlags |= 1;
					}
				}
			}
			else
			{
				if (vx >= minvx && vx <= maxvx)
				{
					continue;
				}

				if (pItem->mPos.vz >= minvz && pItem->mPos.vz <= maxvz)
				{
					continue;
				}

				if (pItem->mPos.vy >= minvy && pItem->mPos.vy <= maxvy)
				{
					continue;
				}

				if (visible)
				{
					pItem->mFlags &= ~1;
				}
				else
				{
					pItem->mFlags |= 1;
				}
			}
		}
	}
}

// @Ok
// Functional: set visibility by name, logic verified against Hex-Rays at
// 0x4e6a40. Computes the CRC32 of the name, appends each index a2..a3 as a
// zero-padded 2-digit string, continues the CRC over those digits, looks the
// result up via Spool_FindEnviroItem, and toggles CItem::mFlags bit 0. (The
// 68 mnemonic diffs from the byte-match phase are the name-copy loop shape;
// the logic is equivalent.)
void Utils_SetVisibilityByName(char const * a1, i32 a2, i32 a3, bool a4)
{
	print_if_false(a2 >= 0 && a2 < 100, "Utils_SetVisibilityByName: bad index");
	print_if_false(a3 >= 0 && a3 < 100, "Utils_SetVisibilityByName: bad index");
	print_if_false(a1 != NULL, "Utils_SetVisibilityByName: bad name");

	u32 crc = ~0U;
	{
		const unsigned char *p = reinterpret_cast<const unsigned char*>(a1);
		while (*p)
		{
			crc = crc32_tab[(crc ^ *p++) & 0xFF] ^ (crc >> 8);
		}
	}

	char name[16];
	i32 len = 0;
	{
		const char *p = a1;
		while (*p)
		{
			name[len] = *p;
			len++;
			p++;
		}
	}

	print_if_false(len < 13, "Utils_SetVisibilityByName: name too long");
	name[len + 2] = 0;

	if (a2 > a3)
		return;

	u32 hashLo = crc & 0xFF;
	u32 hashHi = crc >> 8;

	for (i32 i = a2; i <= a3; i++)
	{
		if (i < 10)
		{
			name[len] = '0';
			name[len + 1] = (char)(i + '0');
		}
		else
		{
			name[len] = (char)(i / 10 + '0');
			name[len + 1] = (char)(i % 10 + '0');
		}

		u32 h = crc32_tab[((u8)name[len] ^ hashLo) & 0xFF] ^ hashHi;
		h = crc32_tab[((u8)name[len + 1] ^ (h & 0xFF)) & 0xFF] ^ (h >> 8);

		CItem *item = Spool_FindEnviroItem(h);
		if (item)
		{
			if (a4)
				item->mFlags &= ~1;
			else
				item->mFlags |= 1;
		}
	}
}

// @Ok
// @Matching
void Utils_SetVisibilityInBox(CVector const * min,CVector const * max, bool visible, bool in)
{
	i32 minvx = min->vx;
	i32 minvy = min->vy;
	i32 minvz = min->vz;

	i32 maxvx = max->vx;
	i32 maxvy = max->vy;
	i32 maxvz = max->vz;

	for(CItem *pItem = EnviroList; pItem; pItem = pItem->mNextItem)
	{
		if (PSXRegion[pItem->mRegion].Usable)
		{
			i32 vx = pItem->mPos.vx;

			if (in)
			{
				if (vx >= minvx && vx <= maxvx &&
						pItem->mPos.vy >= minvy && pItem->mPos.vy <= maxvy &&
						pItem->mPos.vz >= minvz && pItem->mPos.vz <= maxvz)
				{
					if (visible)
					{
						pItem->mFlags &= ~1;
					}
					else
					{
						pItem->mFlags |= 1;
					}
				}
			}
			else
			{
				if (vx >= minvx && vx <= maxvx)
				{
					continue;
				}

				if (pItem->mPos.vz >= minvz && pItem->mPos.vz <= maxvz)
				{
					continue;
				}

				if (pItem->mPos.vy >= minvy && pItem->mPos.vy <= maxvy)
				{
					continue;
				}

				if (visible)
				{
					pItem->mFlags &= ~1;
				}
				else
				{
					pItem->mFlags |= 1;
				}
			}
		}
	}

	Utils_SetBaddyVisibilityInBox(min, max, visible, in, BaddyList);
}

// @Ok
// @AlmostMatching: similar to LinearFilter
i32 Utils_ShiftFilter(i32 a1,i32 a2,i32 delta, i32 a4)
{
	print_if_false(delta > 0, "delta must be greater than zero");

	if (a1 > a2)
	{
		if (a1 - a2 <= a4)
			return a2;

		return a1 - ((a1 - a2) >> delta);
	}

	if ( a2 - a1 <= a4 )
		return a2;
	
	return ((a2 - a1) >> delta) + a1;
}

// @Ok
// @AlmostMatching: G_POST_WATER_EFFECT (0x5FAE98) is compared from memory instead of a cached register,
// and the Redbook_XAPlay argument loads use swapped registers (eax/edx)
void Utils_VblankProcessing(void)
{
	if (G_GAME_FADE)
	{
		if (G_GAME_FADE & 0xFFFF0000)
			G_GAME_FADE -= 0x10000;
		else
			--G_GAME_FADE;
	}

	if (G_REDBOOK_BUSY)
	{
		if (ADXT_GetStat(G_ADXT) == 4)
		{
			G_REDBOOK_BUSY = 0;
			G_CARNAGE_XA_RELATED = 1;
			G_CARNAGE_XA_RELATED_TWO = 30;
			G_REDBOOK_XA_CURRENT_PRIORITY = -1;
		}

		return;
	}

	if (G_CARNAGE_XA_RELATED_TWO)
	{
		if (G_POST_WATER_EFFECT)
			return;

		if (--G_CARNAGE_XA_RELATED_TWO)
			return;

		i32 pendingOne = G_PENDING_XA_ONE;
		i32 pendingTwo = G_PENDING_XA_TWO;
		i32 pendingThree = G_PENDING_XA_THREE;

		if (pendingThree | pendingTwo | pendingOne)
		{
			Redbook_XAPlay(pendingOne, pendingTwo, pendingThree);

			G_PENDING_XA_THREE = 0;
			G_PENDING_XA_TWO = 0;
			G_PENDING_XA_ONE = 0;
		}
	}
	else if (!G_POST_WATER_EFFECT)
	{
		Redbook_XAReset();
	}
}

// @Ok
// @Matching
void Utils_TurnTowards(
		CSVector Current,
		CSVector *AngVel,
		CSVector *AngAcc,
		CSVector Ideal,
		i32 accfactor)
{

	CSVector angDiff;
	angDiff.vx = Ideal.vx - Current.vx;
	angDiff.vy = Ideal.vy - Current.vy;

	if ( angDiff.vx < -2048 )
		angDiff.vx += 4096;
	if ( angDiff.vx > 2048 )
		angDiff.vx -= 4096;

	if ( angDiff.vy < -2048 )
		angDiff.vy += 4096;
	if ( angDiff.vy > 2048 )
		angDiff.vy -= 4096;

	if ( angDiff.vx || angDiff.vy )
	{
		AngAcc->vx = (accfactor * angDiff.vx) >> 8;
		AngAcc->vy = (accfactor * angDiff.vy) >> 8;
	}
	else
	{
		AngVel->vx = 0;
		AngVel->vy = 0;
	}
}

// Verified against the IDA decompile of 0x4E6220 (170 bytes): abs-by-branch
// on each axis delta then the 6-way ordering of dX/dY/dZ, weighted sum
// (half/quarter of the two smaller axes plus the largest), shift by 12.
// Every branch and weight matches the original one to one.
// @Ok
// @Test
u32 Utils_CrapDist(const CVector& a,const CVector& b){

	
	i32 dX = a.vx - b.vx;
	i32 dY = a.vy - b.vy;
	i32 dZ = a.vz - b.vz;

    if (dX < 0) {
        dX = -dX;
    }

    if (dY < 0) {
        dY = -dY;
    }

    if (dZ < 0) {
        dZ = -dZ;
    }

    if (dX < dY){

        if (dY < dZ){
			return (dZ + (dX >>2) + (dY >>1)) >> 12;
		}

		if(dZ < dX){
			return ((dZ >> 2) + (dX >> 1) + dY) >> 12;
		}

		return ((dZ >> 1) + (dX >> 2) + dY) >> 12;
    }

    if (dX < dZ){
        return (dZ + (dX >>1) + (dY >>2)) >> 12;
    }

    if (dZ < dY){
        return ((dZ >> 2) + dX + (dY >>1)) >> 12;
    }

    return ((dZ >> 1) + dX + (dY >>2)) >> 12;
}

// Verified against the IDA decompile of 0x4E61E0 (53 bytes): copies a,
// overwrites the y with b's y, calls Utils_CrapDist. Matches exactly.
// @Ok
// @Test
u32 Utils_CrapXZDist(const CVector& a,const CVector& b) {
    CVector tmp = a;
    tmp.vy = b.vy;
    return Utils_CrapDist(tmp, b);
}

// Case-insensitive string compare, verified against the IDA decompile of
// 0x4E6560 (125 bytes): null checks up front, then a lowercase-and-compare
// loop that stops at the first mismatch or when both strings hit their
// terminator at the same time. Real implementation, no forward to the
// original needed.
// @Ok
int Utils_CompareStrings(const char* left, const char* right) {
    if (left == NULL) {
        return right == NULL;
    }

    if (right == NULL) {
        return 0;
    }

    char currLeft = *left;
    char currRight = *right;
    if (currLeft >= 'A' && currLeft <= 'Z') {
        currLeft += ' ';
    }
    if (currRight >= 'A' && currRight <= 'Z') {
        currRight += ' ';
    }

    while (currLeft == currRight) {
        if (currLeft == 0 || currRight == 0) {
            break;
        }

        currLeft = *++left;
        currRight = *++right;

        if (currLeft >= 'A' && currLeft <= 'Z') {
            currLeft += ' ';
        }
        if (currRight >= 'A' && currRight <= 'Z') {
            currRight += ' ';
        }
    }

    return currLeft == 0 && currRight == 0;
}

const f32 FOUR_NINETY_SIX = 4096.0;
const f32 TWO_FOURTY_EIGHT = 2048.0;
const f32 PI = 3.1415927;

// Verified against the IDA decompile of 0x4E6700: acos(val / 4096.0) * 2048 / PI.
// @Ok
int Utils_ArcCos(int val){
	f32 inp = val;
	f32 res = acos(inp / FOUR_NINETY_SIX);
	return (res * TWO_FOURTY_EIGHT / PI);
}



// @Ok
// @Matching
int Utils_CopyString(const char* src, char* dst, int maxSize)
{
	int total = 0;

	if (*src)
	{
		while (*src){

			if (total >= maxSize)
				break;

			*dst++ = *src++;
			total++;

		}
	}

	ASSERT(total < maxSize, "Dest buffer overflow in Utils_CopyString");
	*dst = 0;
	return total;
}

// @Ok
// @AlmostMatching: different vector assingment
CBody* Utils_CheckObjectCollision(
		CVector* a1,
		CVector* a2,
		CBody* a3,
		CBody* a4)
{
	CBody *result;

	CVector v9;
	v9.vx = 0;
	v9.vy = 0;
	v9.vz = 0;

	result = reinterpret_cast<CBody*>(M3dColij_LineToSphere(a1, a2, &v9, a3, a4, 4096));

	if (!result)
	{
		G_LINE_INFO.StartCoords = *a1;
		G_LINE_INFO.EndCoords = *a2;

		M3dColij_InitLineInfo(&G_LINE_INFO);

		G_LINE_OF_SIGHT_CHECK = 1;
		M3dColij_LineToItem(G_ENVIRONMENTAL_OBJECT_LIST, &G_LINE_INFO);
		result = reinterpret_cast<CBody*>(G_LINE_INFO.pItem);
		G_LINE_OF_SIGHT_CHECK = 0;
	}

	return result;
}

// Verified against the IDA decompile of 0x4E6840 (250 bytes): zeroes
// MinCoords/MaxCoords/Position/Normal, sets StartCoords from pos/above and
// EndCoords from pos/below, RecordTriggerZoneHits = 0, M3dZone_LineToItem
// called with the literal 1 (matches the original, which does not
// parameterize that argument here unlike Utils_LineOfSight). Dropped the
// redundant StartCoords/EndCoords zero-then-overwrite (dead stores, the
// original never had them either since it writes those fields directly).
// @Ok
// @Test
int Utils_GetGroundHeight(CVector* pos, i32 above, i32 below, CBody** ppBody)
{
	SLineInfo v7; // [esp+Ch] [ebp-A4h] BYREF

	v7.MinCoords.vx = 0;
	v7.MinCoords.vy = 0;
	v7.MinCoords.vz = 0;

	v7.MaxCoords.vx = 0;
	v7.MaxCoords.vy = 0;
	v7.MaxCoords.vz = 0;

	v7.Position.vx = 0;
	v7.Position.vy = 0;
	v7.Position.vz = 0;

	v7.Normal.vx = 0;
	v7.Normal.vy = 0;
	v7.Normal.vz = 0;


	v7.StartCoords.vx = pos->vx;
	v7.StartCoords.vy = pos->vy - (above << 12);
	v7.StartCoords.vz = pos->vz;

	v7.EndCoords.vx = v7.StartCoords.vx;
	v7.EndCoords.vy = pos->vy + (below << 12);
	v7.EndCoords.vz = pos->vz;
	



	M3dColij_InitLineInfo(&v7);
	v7.RecordTriggerZoneHits = 0;
	M3dZone_LineToItem(&v7, 1);

	if (v7.pItem)
	{
		if ( ppBody )
			*ppBody = (v7.pItem->mFlags & 0x10) != 0 ? (CBody *)v7.pItem : 0;
		return v7.Position.vy;
	}
	else
	{
		if (ppBody)
			*ppBody = 0;
		return -1;
	}
}

SLineInfo line_info;

// @Ok
// @Test
i32 Utils_LineOfSight(
		CVector* a1,
		CVector* a2,
		CVector* a3,
		i32 a4)
{
	line_info.StartCoords = *a1;
	line_info.EndCoords = *a2;

	M3dColij_InitLineInfo(&line_info);
	M3dZone_LineToItem(&line_info, a4 == 0);
	

	if (line_info.pItem)
	{
		if (a3)
			*a3 = line_info.Position;
		return 0;
	}

	return 1;
}

// @Ok
int catan(int a1)
{
	 return (atan((f64)a1 / 4096.0) * 651.0006103515625);
}

// @Ok
int Utils_CalcAim(CSVector* a1, CVector* a2, CVector* a3)
{
	int x,y,z;
	x = (a3->vx - a2->vx) >> 12;
	y = (a3->vy - a2->vy) >> 12;
	z = (a3->vz - a2->vz) >> 12;

	if (z)
	{
		if (z > 0)
		{
			// 0x4E6315: the yaw is mirrored around 0x800 when the target is
			// in front (+z). Without this every aim at a +z target pointed
			// the wrong way (Black Cat walked away from Spidey).
			a1->vy = 0x800 - catan(-((x << 12) / z));
		}
		else
		{
			a1->vy = catan((x << 12) / z);
		}
	}
	else
	{
		if (x > 0)
		{
			a1->vy = -1024;
		}
		else
		{
			a1->vy = 1024;
		}
	}

	i32 v7 = M3dMaths_SquareRoot0(x*x + z*z);
	if (v7)
	{
		if (y > 0)
		{
			a1->vx = catan((y<<12) / v7);
		}
		else
		{
			a1->vx = -catan(-4096 * y / v7);
		}
	}
	else
	{
		a1->vx = y > 0 ? 1024 : -1024;
	}

	a1->vx &= 0xFFF;
	a1->vy &= 0xFFF;
	a1->vz = 0;
	return v7;
}

// RNG state, seeded by Utils_InitialRand, stepped by Rnd and by Utils_Jumble
// (which inlines Rnd three times). Only these three functions touch it in the
// whole binary, and all three live in this file, so the macros stay file-local.
//
// Addresses read out of the disassembly. Utils_InitialRand (0x4E5D80) is five
// instructions long and stores all three: "mov dword [6B4B90h],12B9B0A1h" at
// 0x4E5D84 is gRndRelatedTwo, "mov [6B4C7Ch],eax" at 0x4E5D8E is the seed
// argument going into gRndRelatedOne, and "mov dword [6B4B94h],0AA2FB3Fh" at
// 0x4E5D93 is gRndRelatedThree. Rnd (0x4E5DA0) reads and writes the same three.
//
// They have to point at game memory. Neither Rnd nor Utils_InitialRand is
// hooked, so the exe seeds and steps its own copy while our DLL copy stays at
// zero. Hooked code in carnage.cpp, chopper.cpp, exp.cpp and the rest calls our
// Rnd, so without this every hooked caller runs a different random stream than
// the game.
// @FIXME
#ifdef SPIDEY_STANDALONE
#define gRndRelatedOne (*reinterpret_cast<int*>(0x006b4c7c))
#else
static int gRndRelatedOne;
#endif
//#define G_RND_RELATED_ONE (gRndRelatedOne)
#define G_RND_RELATED_ONE (*reinterpret_cast<int*>(0x006B4C7C))
// @FIXME
#ifdef SPIDEY_STANDALONE
#define gRndRelatedTwo (*reinterpret_cast<int*>(0x006b4b90))
#else
static int gRndRelatedTwo;
#endif
//#define G_RND_RELATED_TWO (gRndRelatedTwo)
#define G_RND_RELATED_TWO (*reinterpret_cast<int*>(0x006B4B90))
// @FIXME
#ifdef SPIDEY_STANDALONE
#define gRndRelatedThree (*reinterpret_cast<int*>(0x006b4b94))
#else
static int gRndRelatedThree;
#endif
//#define G_RND_RELATED_THREE (gRndRelatedThree)
#define G_RND_RELATED_THREE (*reinterpret_cast<int*>(0x006B4B94))

// Verified against the IDA decompile of 0x4E5D80 (30 bytes): store order and
// constants (0x12B9B0A1, 0xAA2FB3F) match the original exactly.
// @Ok
void Utils_InitialRand(int a)
{
	G_RND_RELATED_TWO = 0x12B9B0A1;
	G_RND_RELATED_ONE = a;
	G_RND_RELATED_THREE = 0xAA2FB3F;
}

// Verified against the IDA decompile of 0x4E5DA0 (77 bytes): the LCG-style
// update order (One, then Two, then the (u16)One*n result, then Three with
// the 0x10101010 subtract) matches the original exactly.
// @Ok
INLINE int Rnd(i32 n)
{
	i32 result; // eax
	G_RND_RELATED_ONE = G_RND_RELATED_THREE + G_RND_RELATED_ONE * G_RND_RELATED_TWO;
	G_RND_RELATED_TWO = (G_RND_RELATED_ONE >> 4) + (G_RND_RELATED_ONE ^ G_RND_RELATED_TWO);
	result = (n * (u16)G_RND_RELATED_ONE) >> 16;
	G_RND_RELATED_THREE = G_RND_RELATED_THREE + (G_RND_RELATED_ONE >> 3) - 0x10101010;
	return result;
}

// @Ok
// @Matching
u32 Utils_GenerateCRC(const char* buf)
{
	const unsigned char *p = reinterpret_cast<const unsigned char*>(buf);
	u32 crc;
	crc = ~0U;

	while (*p)
	{
		crc = crc32_tab[(crc ^ *p++) & 0xFF] ^ (crc >> 8);
	}

	return crc;
}

// @Ok
// @AlmostMatching: the return a2 is merging for some reason
int Utils_LinearFilter(
		i32 a1,
		i32 a2,
		i32 delta)
{
	print_if_false(delta > 0, "delta must be greater than zero");

	if (a1 > a2)
	{
		if (a1 - a2 > delta)
	        return a1 - delta;

	}
    else if (a2 - a1 > delta)
        return a1 + delta;

	return a2;
}

// @Ok
// @Matching
void Utils_GetVecFromMagDir(CVector * a1, int a2, CSVector * a3)
{
	a1->vx = -(((G_RCOSSIN_TBL[a3->vx & 0xFFF].cos * a2) >> 12) * G_RCOSSIN_TBL[a3->vy & 0xFFF].sin);
	a1->vy = G_RCOSSIN_TBL[a3->vx & 0xFFF].sin * a2;
	a1->vz = -(((G_RCOSSIN_TBL[a3->vx & 0xFFF].cos * a2) >> 12) * G_RCOSSIN_TBL[a3->vy & 0xFFF].cos);
}

// @Ok
// @Matching
i32 Utils_GetValueFromDifficultyLevel(i32 a1, i32 a2, i32 a3, i32 a4)
{
	switch (G_DIFFICULTY_LEVEL)
	{
		case 0:
			return a1;
		case 1:
			return a2;
		case 2:
			return a3;
		default:
			return a4;
	}
}

// @Ok
// @Matching
i32 Utils_XZDist(const CVector* a1, const CVector *a2)
{
	i32 v2 = ((a1->vx - a2->vx) >> 12);
	v2 *= v2;

	i32 v3 = ((a1->vz - a2->vz) >> 12);
	v3 *= v3;

	return M3dMaths_SquareRoot0(v2 + v3);
}

// @Ok
// Functional: Y-axis rotation, logic verified against Hex-Rays at 0x4e5fe0.
// vx' = (vx*cos + vz*sin) >> 9, vz' = (vz*cos - vx*sin) >> 9, table index
// a3 & 0xFFF. (The original also returns the cos value; no caller uses it,
// so the void return is fine.)
void Utils_RotateY(CVector * a1, CVector * a2, i32 a3)
{
	SSinCos const * sc = &G_RCOSSIN_TBL[a3 & 0xFFF];
	a1->vx = ((a2->vx >> 3) * sc->cos + (a2->vz >> 3) * sc->sin) >> 9;
	a1->vy = a2->vy;
	a1->vz = ((a2->vz >> 3) * sc->cos - (a2->vx >> 3) * sc->sin) >> 9;
}

#include "my_patch.h"

// @Bogus
void patch_utils(void)
{
	PATCH_PUSH_RET(0x004E5DF0, Utils_GetValueFromDifficultyLevel);
	PATCH_PUSH_RET(0x004E61A0, Utils_XZDist);
	PATCH_PUSH_RET(0x004E6F00, Utils_LinearFilter);
	PATCH_PUSH_RET(0x004E6F50, Utils_ShiftFilter);
	PATCH_PUSH_RET(0x004E6520, Utils_GenerateCRC);
	PATCH_PUSH_RET(0x004E6150, Utils_Dist);
	PATCH_PUSH_RET(0x004E65E0, Utils_CopyString);
	PATCH_PUSH_RET(0x004E5C10, Utils_VblankProcessing);
}
