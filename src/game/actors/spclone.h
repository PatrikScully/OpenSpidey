#pragma once

#ifndef SPCLONE_H
#define SPCLONE_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"

#include "engine/assets/reloc.h"

class CSpClone : public CBaddy {
	public:
		EXPORT void AI(void);
		EXPORT CSpClone(i16 *,i32);
		EXPORT void DoMGSShadow(void);
		EXPORT i32* GetNewCommandBlock(u32);
		EXPORT void KillCommandBlockByID(i32);
		EXPORT void SynthesizeAnalogueInput(void);
		EXPORT ~CSpClone(void);
		EXPORT void Shouldnt_DoPhysics_Be_Virtual(void);
		EXPORT void DoPhysics(void);
		EXPORT void KillAllCommandBlocks(void);
		EXPORT i32* KillCommandBlock(i32*);

		i32 mLookTargetNode;

		// Downward acceleration used while descending onto the ledge.
		i32 mGravityAcceleration;
		i32 mPreviousElapsedTicks;
		i32 mOlderElapsedTicks;
		// Smoothed timestep with twelve fractional bits (4096 is one tick).
		u32 mPhysicsStepScale;
		CQuadBit *field_338;

		u8 mScriptRunning;
		PADDING(3);

		i32 mScriptElapsedTicks;
		u8 mScriptBytecodeActive;
		PADDING(3);

		i32 mScriptCursorAddress;
		i32* mScriptCommandHead;
};

void validate_CSpClone(void);
EXPORT void SpClone_CreateSpClone(const u32 *,u32 *);
EXPORT void SpClone_RelocatableModuleClear(void);
EXPORT void SpClone_RelocatableModuleInit(reloc_mod *);
void patch_spclone(void);

#endif
