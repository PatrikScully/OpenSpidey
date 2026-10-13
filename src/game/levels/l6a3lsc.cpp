#include "game/levels/l6a3lsc.h"

#include "game/player/spidey.h"
#include "engine/world/trig.h"
#include <string.h>

// @Ok
void L6A3LSC_MonitorSpideyinWater(const u32 *,u32 *)
{
    static i32 * const gWaterTimer = (i32*)0x5FB88C;
    static i32 * const gWaterTargetY = (i32*)0x5FCDA8;
    static u32 ** const gTrigNodes = (u32**)0x6B466C;

    G_MECHLIST_PLAYER->mFlags &= ~8u;
    if (G_MECHLIST_PLAYER->mPos.vy >> 12 <= 6800)
    {
        *gWaterTimer = 0;
    }
    else
    {
        G_MECHLIST_PLAYER->mFlags |= 8u;
        *gWaterTargetY = 6800;
        *gWaterTimer += G_MECHLIST_PLAYER->mElapsedTicks;
        if (*gWaterTimer > 10)
        {
            u8 found = 0;
            if (NumNodes > 1)
            {
                i32 nodeIdx = 1;
                CVector pos;
                while (1)
                {
                    if (gTrigNodes[nodeIdx][0] == 1)
                    {
                        memset(&pos, 0, sizeof(pos));
                        Trig_GetPosition(&pos, nodeIdx);
                        if (pos.vx == -22315008 && pos.vy == 0x532000 && pos.vz == -30212096)
                            break;
                    }
                    if (++nodeIdx >= NumNodes)
                        break;
                }
                if (nodeIdx < NumNodes)
                {
                    found = 1;
                    *gWaterTimer = 0;
                    Trig_SendPulseToNode(nodeIdx);
                    G_MECHLIST_PLAYER->mPos = pos;
                }
            }
            print_if_false(found, "No TRG_Drowning node");
        }
    }
}

// @Ok
// @Matching
void L6A3LSC_RelocatableModuleClear(void)
{
}

// @Ok
// @Matching
void L6A3LSC_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = L6A3LSC_RelocatableModuleClear;
	pMod->field_C[0] = L6A3LSC_MonitorSpideyinWater;
	Spidey_SetUserFunction("l6a3lsc", 1u);
}
