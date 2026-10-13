#include "game/actors/zrhinog.h"
#include "compatibility/export.h"

#include "game/effects/effects.h"
#include "engine/rendering/m3dutils.h"
#include "game/player/web.h"

// @Ok
// Functional decompile of 0x43A400 (333 bytes). Notes on the pieces, since the
// earlier passes on this file could not name them:
//  - sub_43A0F0 is CFootprint::CFootprint, already @Ok in effects.cpp.
//  - sub_43A300 is Effects_FootStomp. The Mac symbol list settles this: the
//    effects.cpp functions sit in the same order in both builds, and
//    Effects_FootStomp is exactly the entry between CFootprint::Move and
//    Effects_RhinoStomp there (0006f0a0 on Mac, 0x43A300 here). It is now
//    decompiled in effects.cpp together with the CPingLine dust particle class
//    it allocates (Mac 0006e7d0..0006eb60, PC 0x439E20..0x43A040), which was
//    the real blocker on this function.
//  - hook part 3 is the left foot and part 6 the right foot, both taken 400
//    units up the part (SHook.Part), and the checksum handed to
//    Effects_FootStomp is the constant 0xC16175F4 the original pushes.
// The two feet get their Y replaced by the ground height under the rhino, so
// the footprints and the dust always sit on the floor, and the dust burst
// happens at the midpoint of the two feet.
void Effects_RhinoStomp(CSuper *pRhino)
{
	print_if_false(pRhino != NULL, "NULL pRhino sent to Effects_RhinoStomp");

	CVector LeftFoot;
	SHook Hook;

	Hook.Part.vx = 0;
	Hook.Part.vy = 400;
	Hook.Part.vz = 0;
	Hook.Offset = 3;

	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&LeftFoot), pRhino, &Hook);

	CVector RightFoot;

	Hook.Part.vx = 0;
	Hook.Part.vy = 400;
	Hook.Part.vz = 0;
	Hook.Offset = 6;

	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&RightFoot), pRhino, &Hook);

	i32 GroundY = Web_GetGroundY(&pRhino->mPos);

	RightFoot.vy = GroundY;
	LeftFoot.vy = GroundY;

	CVector Middle;

	Middle.vx = (RightFoot.vx + LeftFoot.vx) >> 1;
	Middle.vy = GroundY;
	Middle.vz = (RightFoot.vz + LeftFoot.vz) >> 1;

	Effects_FootStomp(&Middle, 0xC16175F4);

	new CFootprint(&RightFoot, pRhino->mAngles.vy);
	new CFootprint(&LeftFoot, pRhino->mAngles.vy);
}
