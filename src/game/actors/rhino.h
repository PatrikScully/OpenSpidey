#pragma once

#ifndef RHINO_H
#define RHINO_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "engine/assets/reloc.h"

struct SRhinoData
{
	i16 field_0;
	i16 field_2;

	u16 field_4;
	i16 field_6;

	i32 field_8;
};

class CRhino : public CBaddy {
public:
	EXPORT CRhino(i16*, i32);
	EXPORT CRhino(void);
	EXPORT virtual ~CRhino(void);

	EXPORT void PlaySingleAnim(u32, i32, i32);
	EXPORT u32 GetNextFootstepSFX(void);
	EXPORT i32 GetShockDamage(void);
	EXPORT void ShakePad(void);
	EXPORT void StandStill(void);
	EXPORT void FuckUpSomeBarrels(void);
	EXPORT void RhinoInit(void);
	EXPORT void Laugh(void);
	EXPORT void HitWall(void);
	EXPORT void TakeHit(void);
	EXPORT i32 DetermineFightState(i32);

	EXPORT void AI(void);
	EXPORT void AttackPlayer(void);
	EXPORT void ChargePlayer(void);
	EXPORT void ChasePlayer(i32);
	EXPORT i32 CheckIfPlayerHit(void);
	EXPORT void DieRhino(void);
	EXPORT void DoDazedEffect(void);
	EXPORT void DoMGSShadow(void);
	EXPORT void FollowWaypoints(void);
	EXPORT void GetLaunched(void);
	EXPORT void GetShocked(void);
	EXPORT void GetTrapped(void);
	EXPORT i32 GonnaHitWall(i32);
	EXPORT u8 LineOfSightCheck(CVector const *,i32);
	EXPORT void PlaySounds(void);
	EXPORT void PlayXAPlease(i32,i32,i32);
	EXPORT void SetUpStuckHorn(SLineInfo *,i32);
	EXPORT void SlideFromHit(i32,i32,CVector *);
	EXPORT void StompGround(void);
	EXPORT void StuckInWall(void);

	i32 field_324;
	i32 field_328;

	i32 field_32C;

	i32 field_330;

	i32 field_334;

	i32 field_338;

	PADDING(0x344 - 0x338 - 4);

	i32 field_344;
	i32 field_348;

	i32 field_34C;
	i32 field_350;

	i32 field_354;

	i32 field_358;

	i32 field_35C;

	i32 field_360;

	PADDING(0x388 - 0x360 - 4);

	i32 field_388;

	PADDING(0x3D0 - 0x388 - 4);

	i32 field_3D0;

	i32 field_3D4;
	u8 field_3D8;

	PADDING(3);

	i32 field_3DC;
	u32 field_3E0;
	u32 field_3E4[5];
	u32 field_3F8[5];
	u32 field_40C[5];

	i32 field_420;
};

class CRhinoNasalSteam : public CFlatBit
{
	public:
		EXPORT CRhinoNasalSteam(CVector*, CVector*);
		EXPORT ~CRhinoNasalSteam(void);
		EXPORT void Move(void);
};

void validate_CRhino(void);
void validate_SRhinoData(void);
void validate_CRhinoNasalSteam(void);

EXPORT void Rhino_CreateRhino(const u32 *stack, u32 *result);
EXPORT void Rhino_RelocatableModuleClear(void);
EXPORT void Rhino_RelocatableModuleInit(reloc_mod *);


void patch_rhino(void);

#endif
