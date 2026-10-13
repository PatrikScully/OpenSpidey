#include "game/levels/l5a6lsc.h"
#include "game/player/spidey.h"
#include "engine/world/ob.h"
#include "engine/assets/spool.h"
#include "game/actors/baddy.h"
#include "engine/world/trig.h"

// pool water level cache and drown-grace timer for l5a6lsc, addresses from
// the original binary (used by L5A6LSC_MonitorSpideyinWater, 0x448e50).
static i32 * const gL5A6PoolWaterLevel = (i32*)0x005FCDA8;
static i32 * const gL5A6DrownTimer = (i32*)0x005FB65C;

// @Ok
// @AlmostMatching: 74 mnemonic diffs, all one cluster. The original binary
// tail-merges the obtainWaterLevelInPool call site for pool index 0 with the
// one for pool index 2 (index 0's success path jumps straight into index 2's
// call/shl/cmp/store/jle tail with its own pushed argument already on the
// stack), but index 1 and index 3 each keep their own separate copy of the
// same call+check code. Our build keeps 4 separate copies (163 instructions
// vs the original's 150), so index 0 never shares with index 2. 15 source
// hypotheses tried, none reproduced this specific cross-jump: shared
// poolIndex var with one post-chain call, duplicated per-branch calls
// (kept, closest), goto-based vs inline-duplicated failure cleanup (fixed a
// separate tail-duplication bug), MechList->mPos = v vs 3 field stores
// (fixed 4 diffs), timer update as local/delta/plain-add variants (3 tries,
// neutral), block-scoped vs shared "level" local, reversed vy comparison
// operands, CVector-based position load vs 3 plain derefs, fused assignment
// expression for level vs 2 statements (both directions tried), swapped
// vx/vy/vz declaration order, a cached MechList local pointer vs the
// original's repeated reload, u32 vs i32 for level (confirmed i32 is
// correct, u32 flips jg/jle to ja/jbe). Rest of the function (prologue, all
// 4 pool boundary box tests, the drowning trigger node search loop, the
// found-node CVector position store, the final print_if_false) is an exact
// instruction match.
void L5A6LSC_MonitorSpideyinWater(const u32 *,u32 *)
{
	G_MECHLIST_PLAYER->mFlags &= ~8;

	CVector pos = G_MECHLIST_PLAYER->mPos >> 12;
	i32 vx = pos.vx;
	i32 vy = pos.vy;
	i32 vz = pos.vz;

	if (vx > -1140 && vx < 2000 && vz > 4000 && vz < 4640)
	{
		if (vy <= 2700 || vy >= 3400)
		{
			*gL5A6DrownTimer = 0;
			return;
		}

		G_MECHLIST_PLAYER->mFlags |= 8;
		i32 level = obtainWaterLevelInPool(0);
		*gL5A6PoolWaterLevel = level << 12;

		if (vy <= level)
		{
			*gL5A6DrownTimer = 0;
			return;
		}
	}
	else if (vx > 2000 && vx < 5900 && vz > 4000 && vz < 4640)
	{
		if (vy <= 2700 || vy >= 3400)
		{
			*gL5A6DrownTimer = 0;
			return;
		}

		G_MECHLIST_PLAYER->mFlags |= 8;
		i32 level = obtainWaterLevelInPool(1);
		*gL5A6PoolWaterLevel = level << 12;

		if (vy <= level)
		{
			*gL5A6DrownTimer = 0;
			return;
		}
	}
	else if (vx > -360 && vx < 1600)
	{
		if (vz > 6800 && vz < 10900)
		{
			if (vy <= 1800 || vy >= 3700)
			{
				*gL5A6DrownTimer = 0;
				return;
			}

			G_MECHLIST_PLAYER->mFlags |= 8;
			i32 level = obtainWaterLevelInPool(2);
			*gL5A6PoolWaterLevel = level << 12;

			if (vy <= level)
			{
				*gL5A6DrownTimer = 0;
				return;
			}
		}
		else if (vx < 1600 && vz > -2100 && vz < 2000)
		{
			if (vy <= 1800 || vy >= 3700)
			{
				*gL5A6DrownTimer = 0;
				return;
			}

			G_MECHLIST_PLAYER->mFlags |= 8;
			i32 level = obtainWaterLevelInPool(3);
			*gL5A6PoolWaterLevel = level << 12;

			if (vy <= level)
			{
				*gL5A6DrownTimer = 0;
				return;
			}
		}
		else
		{
			*gL5A6DrownTimer = 0;
			return;
		}
	}
	else
	{
		*gL5A6DrownTimer = 0;
		return;
	}

	*gL5A6DrownTimer += G_MECHLIST_PLAYER->mElapsedTicks;

	if (*gL5A6DrownTimer <= 20)
		return;

	{
		bool found = false;

		for (i32 i = 1; i < NumNodes; i++)
		{
			if (*G_OFFSETLIST[i] == 1)
			{
				CVector v;
				Trig_GetPosition(&v, i);

				if (v.vx == 0xFEAB8000 && v.vy == 0x532000 && v.vz == 0xFE330000)
				{
					found = true;
					*gL5A6DrownTimer = 0;
					Trig_SendPulseToNode(i);
					G_MECHLIST_PLAYER->mPos = v;
					break;
				}
			}
		}

		print_if_false(found, "No TRG_Drowning node");
	}
}

// @Ok
// @Matching
void L5A6LSC_RelocatableModuleClear(void)
{
}

// @Ok
// @Matching
void L5A6LSC_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = L5A6LSC_RelocatableModuleClear;
	pMod->field_C[0] = L5A6LSC_MonitorSpideyinWater;
	Spidey_SetUserFunction("l5a6lsc", 1u);
}

// @Ok
// @Matching
EXPORT i32 obtainWaterLevelInPool(i32 poolIndex)
{
	u32 model;

	if (poolIndex == 0)
		model = Spool_GetModel(0x8D1EE7F8, gObjFileRegion);
	else if (poolIndex == 1)
		model = Spool_GetModel(0x6A6967FF, gObjFileRegion);
	else if (poolIndex == 2)
		model = Spool_GetModel(0xD7B2D512, gObjFileRegion);
	else
		model = Spool_GetModel(0xD7B2D512, gObjFileRegion);

	CBody *node = G_ENVIRONMENTAL_OBJECT_LIST;

	while (node)
	{
		u8 region = gObjFileRegion;

		if (node->mType == 0x192 && region == node->mRegion)
		{
			switch (poolIndex)
			{
				case 0:
				case 1:
					if (node->mModel == model)
						goto found;
					break;

				case 2:
					if (node->mModel == model)
					{
						i32 x = node->mPos.vx >> 12;
						i32 z = node->mPos.vz >> 12;

						if (x > -360 && x < 1600 && z > 6800 && z < 10900)
							goto found;
					}
					break;

				case 3:
					if (node->mModel == model)
					{
						i32 x = node->mPos.vx >> 12;
						i32 z = node->mPos.vz >> 12;

						if (x > -360 && x < 1600 && z > -2100 && z < 2000)
							goto found;
					}
					break;
			}
		}

		node = reinterpret_cast<CBody*>(node->mNextItem);
	}

	print_if_false(0, "Pool object not found");
	return 0;

found:
	return node->mPos.vy >> 12;
}
