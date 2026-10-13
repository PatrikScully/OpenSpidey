#pragma once

#ifndef SIMBY_H
#define SIMBY_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "engine/assets/reloc.h"

class CPunchOb : public CBaddy {
public:
	EXPORT CPunchOb(i16*, i32);
	EXPORT virtual ~CPunchOb(void);
	EXPORT virtual i32 Hit(SHitInfo*);
	EXPORT virtual void AI(void);

	EXPORT void SendPulse(void);

	PADDING(4);

	u8 field_328;
	PADDING(0x32C - 0x328 - 1);
};

class CSimby : public CBaddy {
public:
	EXPORT CSimby(i32*, i32);
	EXPORT CSimby(void);
	// The only two virtuals CSimby adds beyond CBaddy's 17 (vtable 0x53C0B4
	// slots 17 and 18, the next dword is CSimbyBase's vtable). Flash is
	// 0x4AF260 (Mac: Flash__6CSimbyFiUcUcUc), ExplosionReaction is a
	// tentative name for the unnamed 0x4A7E70 that CManipOb::Chunk calls
	// on every type 324 baddy inside the blast.
	EXPORT virtual void Flash(i32, u8, u8, u8);
	EXPORT virtual void ExplosionReaction(void);
	EXPORT void FlashUpdate(void);
	EXPORT void SetAlertModeTimer(i32);
	EXPORT void ClearAttackData(void);
	EXPORT void SetUpUnitFromDirection(CVector*, i32);
	EXPORT void SetUpJumpData(i32, i32);
	EXPORT void RunAppropriateHitAnim(void);
	EXPORT i32 FireTrappedToDeath(void);
	EXPORT i32 PlayAndAttachXAPlease(i32, i32, CBody*, i32);
	EXPORT void PlayGruntSound(void);
	EXPORT void TakeHit(void);
	EXPORT void Shoot(void);
	EXPORT void SetUpHandPos(void);
	EXPORT void SimbyKnockSpideyDown(i32);


	i32 field_324;
	i16 field_328;
	u16 field_32A;
	u16 field_32C;
	i16 field_32E;
	i32 field_330;

	PADDING(0x344-0x330-4);

	i32 field_344;

	i32 field_348;
	i32 field_34C;

	i32 field_350;
	i32 field_354;
	i32 field_358;
	i32 field_35C;
	i32 field_360;
	i32 field_364;

	i32 field_368;
	i32 field_36C;
	i32 field_370;

	i32 field_374;
	i32 field_378;
	i32 field_37C;
	i32 field_380;
	i32 field_384;
	i32 field_388;
	i32 field_38C;
	i32 field_390;
	i32 field_394;
	i32 field_398;

	i32 field_39C;

	i32 field_3A0;

	PADDING(0x3B8-0x3A4);

	i32 field_3B8;
	i32 field_3BC;
	i32 field_3C0;

	PADDING(0x3CC-0x3C0-4);

	i32 field_3CC;
	i32 field_3D0;

	PADDING(0x3DC-0x3D0-4);

	CVector field_3DC;

	PADDING(0x3EC-0x3E8);

	i32 field_3EC;

	i32 field_3F0;

	PADDING(4);

	i32 field_3F8;
	i32 field_3FC;
	i32 field_400;
	i32 field_404;
	i32 field_408;
	i32 field_40C;

	PADDING(0x460 - 0x40C-4);
};

class CSimbyBase : public CBaddy
{
	public:
		PADDING(0x334 - 0x324);
};

class CSimbySlimeBase : public CQuadBit
{
	public:
		EXPORT void ScaleUp(void);
		EXPORT void ScaleDown(void);
		EXPORT void ScaleDownAndDie(void);

		PADDING(0x9C-0x84);

		i32 field_9C;

		PADDING(0xA4-0x9C-4);

		i32 field_A4;

		PADDING(0x114-0xA4-4);
};

class CEmber : public CFlatBit
{
	public:
		EXPORT CEmber(const CVector*, int);
		EXPORT virtual ~CEmber(void);

		CVector field_68;
		i32 field_74;
		CVector field_78;
		i32 field_84;
		i32 field_88;
		i32 field_8C;
};

// small debris particle spawned by Simby_SplattyExplosion (sub_4A57E0,
// 0x4A57E0). Confirmed CQuadBit-derived (base ctor sub_408FA0, mType=13);
// no standalone name in names.json ("CSimbyDrop_CSimbyDrop" is our own
// guess based on the CGlowFlash/CSimbyShot naming pattern in this file,
// not an IDB-confirmed name).
class CSimbyDrop : public CQuadBit
{
	public:
		EXPORT CSimbyDrop(CVector*, CVector*, i32, i32);

		// always set to 1 by the constructor; purpose beyond that not
		// determined (no reader found yet in this file's scope).
		i32 field_84;

		// ground height under the spawn position (Web_GetGroundY), stashed
		// but not otherwise used within the constructor.
		i32 field_88;

		// opaque value read from the SLineInfo raycast hit item's data if a
		// flag bit was set (see Simby_SplattyExplosion), otherwise 0.
		i32 field_8C;
};

class CSimbyShot : public CQuadBit
{
	public:
		EXPORT CSimbyShot(CVector*);

		// set to 1 when the spawn-to-target raycast (in the constructor) hits
		// an item; gates the splat-spawning branch in CSimbyShot::Move
		// (sub_4A6520, not yet in this repo).
		i32 field_84;

		// sign-extended copy of the raycast hit's SLineInfo::Normal, only
		// meaningful when field_84 != 0.
		CVector field_88;

		// spawn position, kept around as the interpolation base used every
		// frame by CSimbyShot::Move to recompute mPos/mPosC.
		CVector field_94;

		// unit vector from the spawn position toward MechList (or, if the
		// raycast hit something first, still the direction of the original
		// aim; the ray always points at MechList).
		CVector field_A0;

		// two lagged "distance travelled along field_A0" markers (field_AC
		// trails field_B0 by 250) that drive mPos/mPosC every frame in
		// CSimbyShot::Move, clamped to field_B4.
		i32 field_AC;
		i32 field_B0;

		// distance from the spawn position to the raycast hit point (or to
		// the 5000-unit-ahead point if nothing was hit); clamp bound for
		// field_AC/field_B0.
		i32 field_B4;
};

class CSkidMark : public CQuadBit
{
	public:
		EXPORT CSkidMark(void);
		EXPORT virtual void Move(void);
};

class CFireySpark : public CPixel
{
	public:
		EXPORT CFireySpark(CVector*, CVector*, i32);
		EXPORT virtual ~CFireySpark(void);

		EXPORT virtual void Move(void);

		i32 field_48;
		i32 field_4C;
};


class CSimbyDroplet : public CFlatBit
{
	public:
		EXPORT CSimbyDroplet(i16*, i32);
		EXPORT virtual ~CSimbyDroplet(void);

		EXPORT virtual void Move(void);

		u16 field_68;
		u16 field_6A;
		i32 field_6C;
};

class CSymBurn : public CSuper
{
	public:
		EXPORT CSymBurn(CVector*);
		EXPORT virtual ~CSymBurn(void);

		EXPORT void AI(void) OVERRIDE;

		i32 field_1A4;

};

class CFlamingImpactWeb : public CFlatBit
{
	public:
		EXPORT CFlamingImpactWeb(CVector*, CSVector*, i32);
		EXPORT virtual ~CFlamingImpactWeb(void);

		PADDING(4);

		i32 field_6C;
		i32 field_70;
		CItem *pItem;
		u32 *pFace;
		CVector mLinePos;
		CSVector mLineNormal;
};

void validate_CPunchOb(void);
void validate_CSimbyDrop(void);
void validate_CSimby(void);
void validate_CSimbyBase(void);
void validate_CSimbySlimeBase(void);
void validate_CEmber(void);
void validate_CSimbyShot(void);
void validate_CSkidMark(void);
void validate_CFireySpark(void);
void validate_CSimbyDroplet(void);
void validate_CSymBurn(void);
void validate_CFlamingImpactWeb(void);

EXPORT void MakeVertexWibbler(void);
EXPORT void Simby_CreateSimby(const u32 *stack, u32 *result);
EXPORT void Simby_CreateEmber(const u32*, u32*);
EXPORT void Simby_CreateSimbyDroplet(const u32 *, u32 *);
EXPORT void Simby_CreateSimbyPunchOb(const u32 *, u32 *);

EXPORT void Simby_SplattyExplosion(CVector*, CVector*, i32);
EXPORT void Simby_RelocatableModuleInit(reloc_mod*);
EXPORT void Simby_RelocatableModuleClear(void);
EXPORT void Simby_CreateFlamingImpactWeb(const u32 *,u32 *);
EXPORT void Simby_CreatePunchOb(const u32 *stack, u32 *result);
EXPORT void Simby_TestDrop(const u32 *, u32 *);

class CPlayer;
EXPORT void SpideyAI_WaitForSimbyGrab(CPlayer *);
EXPORT void SpideyAI_ThrownBySimby(CPlayer *);

void patch_simby(void);

#endif
