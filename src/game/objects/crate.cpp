#include "game/objects/crate.h"
#include "game/effects/shatter.h"
#include "engine/assets/spool.h"
#include "engine/world/trig.h"
#include "game/effects/exp.h"
#include "core/utils.h"
#include "core/game_heap.h"

// @FIXME
// is empty according to pc??
EXPORT i32 gCrateRecursion = 0;

// @FIXME find real size and shit
#ifndef SPIDEY_STANDALONE
u32* gCommandPointRelated[1];
#else
extern u32* gCommandPointRelated[1];
#endif

// @Ok
// the shr 0x18 happening here is optimized in most cases, not sure what the devs did to
// not trip the compiler
void Crate_Destroy(CItem* pItem)
{
	print_if_false(gCrateRecursion++ < 10, "Woops, loads of crate_destroy recursions");

	if (gCrateRecursion >= 10)
	{
		gCrateRecursion--;
		return;
	}

	if (Shatter_Item(pItem, 1, 1))
	{
		i32 EnvIndex = Spool_GetEnvIndex(pItem->mRegion);
		print_if_false(EnvIndex != -1, "Crate not in the environment???");

		Trig_TriggerCommandPoint(gCommandPointRelated[EnvIndex][pItem->mModel], false);

		if (!LowMemory)
		{
			CVector vec = pItem->mPos;

			if (Rnd(5))
			{
				Exp_SmallExplosion(&vec);
			}
			else
			{
				Exp_BigExplosion(&vec);
			}
		}

		for (CItem* i = EnviroList; i; i = i->mNextItem)
		{
			if (i != pItem)
			{
				u16 v7 = *reinterpret_cast<u16*>(PSXRegion[i->mRegion].ppModels[i->mModel]);
				if (v7 & 2)
				{
					if ((v7 >> 0x18) != 0)
					{
						if ( spidey::math::approximateDistance(pItem->mPos, i->mPos) < 0x200)
						{
							i32 yDiff = pItem->mPos.vy - i->mPos.vy;
							if (yDiff > 409600 && yDiff < 1638400)
							{
								Crate_Destroy(i);
							}
						}
					}
				}
			}

		}

		gCrateRecursion--;
	}

}
