#pragma once

#ifndef THUG_H
#define THUG_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "core/game_heap.h"
#include "engine/rendering/m3dutils.h"
#include "engine/world/bit2.h"
#include "engine/assets/reloc.h"
#include "game/objects/bullet.h"

class CGrenade : public CBullet
{
public:
	EXPORT void CommonInit(CBody**, i32, u32, u8, u8);
	EXPORT CGrenade(const CVector&, const CVector&, i32, i32, i32, CBody**, u32, u8, u8);
	EXPORT virtual ~CGrenade(void);
	EXPORT virtual void AI(void);

	PADDING(4);
	i32 mStartTime;
	u8 mSettled;
	PADDING(3);
	CGlow* mpGlow;
	u8 mRed;
	u8 mGreen;
	u8 mBlue;
	u8 mExplodeOnContact;
	u8 field_15C;
	PADDING(3);
};

class CThug : public CBaddy {
public:

	EXPORT CThug(i16*, i32);
	EXPORT virtual ~CThug();

	EXPORT virtual void Victorious(void);
	EXPORT virtual i32 Hit(SHitInfo*);
	EXPORT virtual void AI(void);
	EXPORT void FlyAcrossRoom(void);
	EXPORT void TossGrenade(void);
	EXPORT void GetYankedBySpidey(void);
	EXPORT void ProcessMessages(void);
	EXPORT void GettingGrabbed(void);
	EXPORT void ShootPlayer(void);
	EXPORT void ShootHostage(void);
	EXPORT void DoAISwitchLogic(void);
	EXPORT void SlideFromHit(i32, i32, CVector&);
	EXPORT void DieThug(i32);
	EXPORT void Fall(void);
	EXPORT void GetTrapped(void);
	EXPORT void RotateTorsoToAimAtPlayer(CVector&);
	EXPORT void AutoAimPlease(CVector*, CVector*, CVector*, i32);
	EXPORT i32 DrawLaserSiteThingie(i32, CBaddy*);
	EXPORT i32 DrawLaserSiteThingieForAlternateModel(i32);
	EXPORT i32 LaserCollision(SLineInfo*, CVector*, i32);
	EXPORT void GetWhippedLikeTheWhoreYouAre(void);
	EXPORT void ChasePlayer(i32);
	EXPORT void FollowWaypoints(void);
	EXPORT void AttackPlayer(void);
	EXPORT i32 TooCloseToSpidey(void);
	EXPORT void GetAttackPosition(CVector*);
	EXPORT i32 SetAttackFlags(void);
	EXPORT void ClearAttackFlags(void);
	EXPORT i32 SetAnimMode(i32, i32);
	EXPORT void HelpOutBuddy(CMessage*);
	EXPORT void RunToWhereTheActionIs(CVector*);
	EXPORT void PlayHitWallSound(void);
	EXPORT void StandStill(void);
	EXPORT void DieAfterFlyingAcrossRoom(void);
	EXPORT void RunAppropriateHitAnim(void);
	EXPORT void StopShooting(void);
	EXPORT i32 TryAddingCollidePointToPath(CVector*);
	EXPORT void SetHitDirectionFlag(CVector*);
	EXPORT void GetReadyToShootHostage(CMessage*);
	EXPORT void CheckFallBack(void);
	EXPORT i32 GetLaunched(CVector*, i32, i32, i32);
	EXPORT u8 TugImpulse(CVector*, CVector*, CVector*);
	EXPORT void LookConfused(void);
	EXPORT void Acknowledge(void);
	EXPORT void StrikeUpConversation(void);
	EXPORT void Guard(void);
	EXPORT void CycleOrContinueAnim(i32, i32, i32, i32);
	EXPORT i32 MonitorSpitPlease(void);
	EXPORT void PlaySounds(void);
	EXPORT i32 CanAck(i32, i32, i32, CThug*);
	EXPORT i32 WallHitCheck(CVector*, CVector*, i32);
	EXPORT i32 SpideyAnimUppercut(void);
	EXPORT void SetAttacker(void);
	EXPORT i32 AdjustPosPlaySound(i32);
	EXPORT void WarnOtherThugs(void);
	EXPORT void CheckToShoot(i32, i32);
	EXPORT void DrawBarrelFlash(CVector*, CVector *, SLineInfo *, u8, u8, u8);
	EXPORT void SetUpLaser(CGPolyLine**, CVector*, CVector*);
	EXPORT i32 ShouldIShootPlayer(void);
	EXPORT void Caution(void);
	EXPORT i32 DetermineFightState(void);
	EXPORT void LookForPlayer(void);
	EXPORT void BackpedalPlease(void);
	EXPORT void TakeHit(void);


	EXPORT virtual u8 Grab(CVector*);
	EXPORT virtual void SetParamByIndex(i32, i32);
	EXPORT virtual void CreateCombatImpactEffect(CVector*, i32);
	EXPORT virtual void SetThugType(i32);


	i32 field_324;
	i32 field_328;

	// Read (never written) by CThug::DetermineFightState (0x4d9cbd) as a
	// gate flag ("this->mType != 304 && field_32C == 0 && ..."). Carved
	// out of what was PADDING(0x330-0x324); same byte range.
	i32 field_32C;

	i32 field_330;

	i32 field_334;
	i32 field_338;

	i32 field_33C;

	i32 field_340;
	i32 field_344;

	// Written by CThug::DetermineFightState (0x4d9af2): a "just got close
	// to the player" timer, set to 60 once when field_330==0 and the
	// distance to the player is under 300 and this is 0 and the y delta
	// to the player is under 819200. Carved out of what was
	// PADDING(0x354-0x33C-4); same byte range.
	i32 field_348;

	i32 field_34C;
	i32 field_350;

	SHandle mHandle;
	SHandle mHandleTwo;

	i32 field_364;
	i32 field_368;

	// Written by CThug::DetermineFightState (0x4d9ab1): a poll interval,
	// set to 3/15/31 depending on distance to the player. Carved out of
	// what was PADDING(0x370-0x35C-sizeof(SHandle)); same byte range.
	i32 field_36C;

	i32 field_370;
	i32 field_374;
	i32 field_378;
	i32 field_37C;
	i32 field_380;
	i32 field_384;
	i32 field_388;
	i32 field_38C;

	PADDING(0x394 - 0x38C - 4);

	i32 field_394;
	i32 field_398;
	i32 field_39C;

	CGPolyLine *field_3A0;
	CVector* field_3A4;

	i32 field_3A8;
	i32 field_3AC;

	i32 field_3B0;

	i32 field_3B4;

	i32 field_3B8;

	u8 field_3BC;
	u8 field_3BD;

	u8 field_3BE;
	PADDING(1);
};

class CThugBulletTracer : public CNonRenderedBit
{
	public:
		EXPORT CThugBulletTracer(const CVector&, const CVector&, CSuper*, SLineInfo*, u8, u8, u8);
		EXPORT virtual ~CThugBulletTracer(void);
		EXPORT virtual void Move(void);
		EXPORT void SetWidth(void);

		CGouraudRibbon* mpRibbon;
		CGouraudRibbon* mpRibbon2;
		u32 mMaxWidth;
};

class CThugLaserPing : public CQuadBit
{
	public:
		EXPORT CThugLaserPing(const CVector&, CVector&, CVector&, u8, u8, u8);
		EXPORT virtual void Move(void);

		i32 field_84;
		CVector field_88;
		CVector field_94;
		CVector field_A0;
};

class CThugPing : public CSimpleAnim
{
	public:
		EXPORT CThugPing(CSuper*, SHook*);
		EXPORT void Move(void);
		EXPORT void SetPosition(void);

		SHandle field_70;
		SHook field_78;
};

void validate_CThug(void);
void validate_CThugPing(void);

EXPORT void CreateThugRicochet(SLineInfo*, u8, u8, u8);

EXPORT void Thug_CreateThug(const u32 *stack, u32 *result);
EXPORT void Thug_RelocatableModuleInit(reloc_mod*);
EXPORT void Thug_RelocatableModuleClear(void);

void patch_thug(void);

#endif
