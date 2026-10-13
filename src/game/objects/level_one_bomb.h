#pragma once

#ifndef L1A3BOMB_H
#define L1A3BOMB_H

#include "compatibility/export.h"
#include "game/objects/manipob.h"

class CL1A3Bomb : public CManipOb
{
	public:
		EXPORT CL1A3Bomb(i16*, i32);
		EXPORT void DoPhysics(void);

		EXPORT virtual void Die(void);
		EXPORT virtual void Smash(void);
		EXPORT virtual void AI(void);

		EXPORT virtual ~CL1A3Bomb(void);

		u8 field_128;
		u8 field_129;

		// set to 1 the first time DoPhysics lands the bomb on the ground
		// (Utils_GetGroundHeight != -1) and reset to 0 whenever it is
		// airborne again; gates the one-shot camera shake + landing SFX in
		// DoPhysics (0x4470a0). Confirmed real by disassembly: read/written
		// at this+0x12A, one byte past field_129.
		u8 field_12A;

		PADDING(0x12C-0x12A-1);
};

EXPORT extern u32 gBombRelated;
EXPORT extern u8 gBombDieRelatedOne;
EXPORT extern u8 gBombDieRelatedTwo;
EXPORT extern u32 gBombDieTimerRelated;
EXPORT extern u32 gBombAIRelated;

void validate_CL1A3Bomb(void);
#endif
