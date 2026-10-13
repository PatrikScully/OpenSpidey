#include "game/levels/l6a1lsc.h"

#include "game/player/spidey.h"
#include "game/actors/baddy.h"
#include "engine/assets/spool.h"
#include "engine/world/trig.h"

// pool water level cache and drown-grace-timer for l6a1lsc, addresses from the
// original binary (0x4497a0).
static i32 * const gL6A1PoolWaterLevel = (i32*)0x005FCDA8;
static i32 * const gL6A1DrownTimer = (i32*)0x005FB86C;

// @Ok
// @AlmostMatching: 7 mnemonic diffs in one cluster right after the commonTail label (the
// drown-timer accumulate and the node-search loop guard). Original uses `add eax,[ecx+80h]`
// for the accumulate and a register-register compare for the loop guard; ours produces
// `mov ecx,[eax+80h]; lea eax,[ecx+edx]` and a memory-operand compare instead. Same
// instruction count on both sides, functionally identical, not a missing/extra instruction.
// Everything else (both pool bounds checks, both obtainWaterLevelInPoolL6A1 calls, the whole
// node-search loop body, the exact 3-field CVector match against the hardcoded drowning
// trigger position, Trig_SendPulseToNode, MechList->mPos = pos, print_if_false tail, final
// ret) matches byte for byte. 16 hypotheses tried, see l6a1lsc.attempts.md.
void L6A1LSC_MonitorSpideyinWater(u32 const *,u32 *)
{
	G_MECHLIST_PLAYER->mFlags &= ~8;

	i32 x = G_MECHLIST_PLAYER->mPos.vx >> 12;
	i32 y = G_MECHLIST_PLAYER->mPos.vy >> 12;
	i32 z = G_MECHLIST_PLAYER->mPos.vz >> 12;

	if (x > -29500 && x < -12250 && z > 1100 && z < 1700)
	{
		if (y > 2460 && y < 4000)
		{
			G_MECHLIST_PLAYER->mFlags |= 8;

			i32 waterLevel = obtainWaterLevelInPoolL6A1(0);
			*gL6A1PoolWaterLevel = waterLevel << 12;

			if (y <= waterLevel)
				goto falseExit;

			goto commonTail;
		}

		goto falseExit;
	}

	if (x > -40000 && x < -32000 && z > -800 && z < 3600)
	{
		if (y > 2500 && y < 6000)
		{
			G_MECHLIST_PLAYER->mFlags |= 8;

			i32 waterLevel = obtainWaterLevelInPoolL6A1(1);
			*gL6A1PoolWaterLevel = waterLevel << 12;

			if (y <= waterLevel)
				goto falseExit;

			goto commonTail;
		}

		goto falseExit;
	}

falseExit:
	*gL6A1DrownTimer = 0;
	return;

commonTail:
	u8 found = 0;
	i32 i = 1;

	*gL6A1DrownTimer += G_MECHLIST_PLAYER->mElapsedTicks;

	if (*gL6A1DrownTimer <= 20)
		return;

	if (i < NumNodes)
	{
		do
		{
			u16 *node = reinterpret_cast<u16*>(G_OFFSETLIST[i]);

			if (*node == 1)
			{
				CVector pos;
				Trig_GetPosition(&pos, i);

				if (pos.vx == static_cast<i32>(0xFEAB8000) &&
					pos.vy == 0x532000 &&
					pos.vz == static_cast<i32>(0xFE330000))
				{
					found = 1;
					*gL6A1DrownTimer = 0;
					Trig_SendPulseToNode(i);
					G_MECHLIST_PLAYER->mPos = pos;
					break;
				}
			}

			i++;
		} while (i < NumNodes);
	}

	print_if_false(found, "No TRG_Drowning node");
}

// @Ok
// @Matching
void L6A1LSC_RelocatableModuleClear(void)
{
}

// @Ok
// @Matching
void L6A1LSC_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = L6A1LSC_RelocatableModuleClear;
	pMod->field_C[0] = L6A1LSC_MonitorSpideyinWater;
	Spidey_SetUserFunction("l6a1lsc", 1u);
}

// @Ok
// @Matching
EXPORT i32 obtainWaterLevelInPoolL6A1(i32 level)
{
	i32 model;

	if (level == 0)
	{
		model = Spool_GetModel(0x6A6967FF, gObjFileRegion);
	}
	else if (level == 1)
	{
		model = Spool_GetModel(0x9E0EE08E, gObjFileRegion);
	}
	else
	{
		// forces a real reload from the parameter's stack home instead of the
		// already-cached register, matches the original exactly (see attempts file)
		model = *(volatile i32*)&level;
	}

	for (CItem *item = G_ENVIRONMENTAL_OBJECT_LIST; item; item = item->mNextItem)
	{
		if (item->mType == 0x192 && item->mRegion == gObjFileRegion)
		{
			switch (level)
			{
				case 0:
					if (item->mModel == model)
						return item->mPos.vy >> 12;
					break;
				case 1:
					if (item->mModel == model)
						return item->mPos.vy >> 12;
					break;
			}
		}
	}

	print_if_false(0, "Pool object not found");
	return 0;
}
