#pragma once

#ifndef LIZMAN_H
#define LIZMAN_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "core/game_heap.h"
#include "engine/assets/reloc.h"

class CMessage;

class CLizMan : public CBaddy {
public:
	EXPORT CLizMan(i16*, i32);

	EXPORT void PlaySingleAnim(i16, i32, i32);
	EXPORT void StandStill(void);
	EXPORT void SwitchFromMatrixToEuler(void);
	EXPORT void StopClimbing(void);
	EXPORT int IsSafeToSwitchToFollowWaypoints(void);
	EXPORT void FlyAcrossRoom(void);
	EXPORT void ClearAttackFlags(void);
	EXPORT void HelpOutBuddy(CMessage*);
	EXPORT void RunToWhereActionIs(CVector*);
	EXPORT void SwitchFromEulerToMatrix(void);
	EXPORT void Acknowledge(void);
	EXPORT void Guard(void);
	EXPORT i32 ScanNearbyNodesForJumpTarget(void);
	EXPORT void CalculateJumpPositionArray(CVector*);
	EXPORT void DoLizmanPhysics(void);
	EXPORT void CheckFallBack(void);

	PADDING(0x4);

	i32 field_328;
	i32 field_32C;

	i16 field_330;
	PADDING(2);
	i16 field_334;
	PADDING(2);

	i32 field_338;

	PADDING(0x340-0x338-4);

	i32 field_340;
	PADDING(0x36C-0x340-4);

	SHandle hLizHandle;
	u32 field_374;

	PADDING(0x390-0x374-4);

	u8 field_390;

	PADDING(3);
	i32 field_394;

	i32 field_398;
	u8 field_39C;
	u8 field_39D;

	PADDING(0x3AC - 0x39C - 4);

	i32 field_3AC;
	i32 field_3B0;
	CVector* field_3B4;
};

void validate_CLizMan(void);
EXPORT void LizMan_RelocatableModuleInit(reloc_mod *);
EXPORT void LizMan_RelocatableModuleClear(void);
EXPORT void LizMan_CreateLizMan(const u32*, u32*);

void patch_lizman(void);

#endif
