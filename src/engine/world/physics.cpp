// physics.cpp: the player's movement code. The Mac build's own physics.cpp
// holds exactly these four functions, in this order:
//   Physics_SetGravity          Mac 0x0A7270  PC 0x466C70
//   CPlayer::DoPhysics          Mac 0x0A7340  PC 0x466CE0  (0x1017 bytes)
//   CPlayer::DoSwingingPhysics  Mac 0x0A82A0  PC 0x467D20  (0x2A8 bytes)
//   CPlayer::DoCrawlingPhysics  Mac 0x0A8640  PC 0x467FD0  (0xD6F bytes)
// Both ends of the run are named the same way on both builds and nothing
// else sits between them, so the two middle slots follow.
//
// Two names are wrong in tools/names.json AND in the maintainer's IDB
// (idbs/spideypc_names.txt), worth reporting upstream:
//   0x4BFEC0 is labelled "CPlayer_DoPhysics" in both. It is really
//   CPlayer::CheckStickToWall (Mac orders CheckStickToCeiling 0x1194B0,
//   CheckStickToWall 0x1196B0, CheckKick 0x1198B0; the PC has 0x4BFCE0,
//   <0x4BFEC0>, 0x4C00B0). It is decompiled under the right name in
//   spidey.cpp.
//   0x466CE0 (DoPhysics) and 0x467D20 (DoSwingingPhysics) are missing from
//   both name sources.
// A third, found while doing 0x467FD0: the unnamed sub_4BEA90 is
// CPlayer::HandleControlsForSurfaceTransition(bool). Mac orders
// SwitchToStandMode / CheckFenceSurfaceTransition /
// HandleControlsForSurfaceTransition / CheckInteriorSurfaceTransition, the
// PC has 0x4BE4B0 / 0x4BE8C0 / 0x4BEA90 / 0x4BEB70, and DoCrawlingPhysics
// calls sub_4BEA90(1) right after swapping a surface normal into
// mLineInfo.Normal, which is exactly what that function reads.

#include "engine/world/physics.h"
#include "game/player/player_physics_controller.h"
#include "game/player/crawl_physics_controller.h"
#include "game/player/web.h"
#include "game/player/swing_collision_resolver.h"

#include "game/player/spidey.h"
#include "engine/rendering/m3dutils.h"
#include "engine/world/collision.h"
#include "engine/world/m3dzone.h"
#include "game/objects/platform.h"
#include "engine/rendering/ps2funcs.h"
#include "game/actors/baddy.h"
#include "core/utils.h"

EXPORT void Physics_SetGravity(CVector *);

// @Ok
void Physics_SetGravity(CVector *pVec)
{
    static CVector * const gGravity = (CVector*)0x60F7B0;
    static i32 * const gGravityLength = (i32*)0x60F7BC;
    static CVector * const gNormalizedGravity = (CVector*)0x60F888;

    *gGravity = *pVec;
    *gGravityLength = gGravity->Length();
    *gNormalizedGravity = *gGravity / (-*gGravityLength);
}

// Original 0x4F7270 free adapter retained for recovered callers.
void CSwinger_GetCurrentParams(i32* swinger, CVector* output)
{
    *output = reinterpret_cast<CSwinger*>(swinger)->currentPosition();
}

namespace {
class WorldSwingProbe : public spidey::player::SwingCollisionProbe {
public:
    explicit WorldSwingProbe(SLineInfo& line) : line_(line) {}
    bool trace(const CVector& start, const CVector& end, spidey::player::SwingSurfaceHit& hit) override {
        line_.StartCoords = start;
        line_.EndCoords = end;
        M3dColij_InitLineInfo(&line_);
        M3dZone_LineToItem(&line_, 1);
        if (!line_.pItem) return false;
        hit.position = line_.Position;
        hit.normal = line_.Normal;
        return true;
    }
private:
    SLineInfo& line_;
};
}

// Original 0x467D20: first sample the current hook, follow the swing arc,
// then sample its new hook before resolving the three ordered collision rays.
void CPlayer::DoSwingingPhysics()
{
    print_if_false(field_E64 != 0, "Error");
    const CVector previousPosition = mPos;
    CVector hookBefore, hookAfter;
    M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookBefore), this, 2);
    mPos = reinterpret_cast<CSwinger*>(field_E64)->currentPosition();
    M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookAfter), this, 2);
    WorldSwingProbe probe(mLineInfo);
    spidey::player::SwingCollisionResolver resolver(mPos, mVel, mCollision, probe);
    resolver.resolve(previousPosition, hookBefore, hookAfter, mSurfaceClearance);
}

// @Ok
// Original 0x467FD0. The per-frame physics for Spidey while he is crawling on
// a surface (walls, ceilings, floors). Integrates mVel/mAcc, then fires four
// short rays: one along the crawl surface from the hook position to catch a
// wall he ran into, two sideways to catch a corner, one from the hook back
// down onto the surface, and finally the "ground" ray from 40 units above the
// player to 140 below, which is the one that actually parks him on the
// surface, sets field_A8 (the surface normal he is aligned to) and reports
// treading on a moving platform.
void CPlayer::DoCrawlingPhysics()
{
    spidey::player::CrawlPhysicsController(*this).update();
}

// Original 0x466CE0 entry point. Read executable policy through this boundary.
void CPlayer::DoPhysics()
{
    spidey::player::PlayerPhysicsPolicy policy = {
        *reinterpret_cast<const i32*>(0x0054EC7C),
        *reinterpret_cast<const i32*>(0x0054EC80),
        *reinterpret_cast<const float*>(0x0054EC84),
        *reinterpret_cast<const i32*>(0x0060F894)
    };
    spidey::player::PlayerPhysicsController(*this, policy).update();
}
