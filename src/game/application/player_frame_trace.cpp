#include "game/application/player_frame_trace.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#include "game/player/spidey.h"
#include "game/cinematics/camera.h"
#include "engine/input/ps2pad.h"
#include "engine/world/trig.h"
#include "engine/world/ob.h"
#include "engine/rendering/m3dinit.h"
#include "engine/assets/spool.h"
#include <cstdio>
#include <cstdlib>
#endif

namespace spidey {
namespace application {

void PlayerFrameTrace::emit() {
#ifdef SPIDEY_STANDALONE
    // SPIDEY_TRACE_PLAYER=1: one line per frame with the player's state,
    // for comparing against the original game (gdb breakpoints distort the
    // wall-clock frame step, this does not).
    static const bool traceEnabled = std::getenv("SPIDEY_TRACE_PLAYER") != nullptr;
    if (!traceEnabled || !G_MECHLIST) return;
    CPlayer* player = static_cast<CPlayer*>(G_MECHLIST);
    const CCamera* camera = G_CAMERA_LIST;
    fprintf(stderr, "PLAYER t=%u status=%d f80=%d pos=(%d,%d,%d) vel=(%d,%d,%d) ang=(%d,%d,%d) coll=%#x mode=%#x anim=%d frame=%d crawl=%d E2D=%d E2E=%d EBC=%d ctl=%d/%d/%d/%d t1B0=%d pad=%d%d%d%d E0C=%p | cam pos=(%d,%d,%d) mode=%d yaw=%d\n",
        Plat_Ticks(), gLevelStatus, player->mElapsedTicks, player->mPos.vx, player->mPos.vy, player->mPos.vz,
        player->mVel.vx, player->mVel.vy, player->mVel.vz,
        player->mAngles.vx, player->mAngles.vy, player->mAngles.vz,
        player->mCollision, player->mActionState, player->mAnim, player->mFrame, player->mIsCrawling,
        player->field_E2D, player->field_E2E, player->field_EBC,
        (i32)player->mSynthesizedInputActive, static_cast<i32>(player->mScriptBytecodeActive), static_cast<i32>(reinterpret_cast<const u8*>(&player->mScriptCommandHead)[0]), (i32)player->field_E18, (i32)player->mScriptElapsedTicks,
        (i32)G_SCONTROL[0].Left.Pressed, (i32)G_SCONTROL[0].Right.Pressed, (i32)G_SCONTROL[0].Up.Pressed, (i32)G_SCONTROL[0].Down.Pressed, (void*)player->field_E0C,
        camera ? camera->mPos.vx >> 12 : 0, camera ? camera->mPos.vy >> 12 : 0, camera ? camera->mPos.vz >> 12 : 0,
        camera ? (i32)camera->mCameraMode : -1, camera ? (i32)camera->field_23A : 0);
    if (player->mActionState == 16)
        fprintf(stderr, "  WALL C6C=(%d,%d,%d) C78=(%d,%d,%d) C84=(%d,%d,%d) A8=(%d,%d,%d) T=[%d %d %d|%d %d %d|%d %d %d] t=(%d,%d,%d) surf=%d wall=%d AD5=%d AD7=%d E32=%d heading=%d\n",
            player->mForwardDirection.vx, player->mForwardDirection.vy, player->mForwardDirection.vz, player->mRightDirection.vx, player->mRightDirection.vy, player->mRightDirection.vz, player->mUpDirection.vx, player->mUpDirection.vy, player->mUpDirection.vz,
            player->field_A8.vx, player->field_A8.vy, player->field_A8.vz,
            player->mTransform.m[0][0], player->mTransform.m[0][1], player->mTransform.m[0][2], player->mTransform.m[1][0], player->mTransform.m[1][1], player->mTransform.m[1][2], player->mTransform.m[2][0], player->mTransform.m[2][1], player->mTransform.m[2][2],
            player->mTransform.t[0], player->mTransform.t[1], player->mTransform.t[2],
            (i32)player->field_8E8, (i32)player->mIsCrawling, (i32)player->mUsesHardSurfaceTransitions, (i32)player->field_AD7, (i32)player->field_E32, (i32)player->GetEffectiveHeading());

    // once, about a second in: the environmental object list in the
    // same layout the wine oracle prints it (oracle.py ENVOBJ lines)
    static i32 tracedFrames = 0;
    if (++tracedFrames == 60)
    {
        const CItem* environmentItem = G_ENVIRONMENTAL_OBJECT_LIST;
        i32 itemCount = 0;
        while (environmentItem && itemCount < 200) {
            fprintf(stderr, "ENVOBJ type=%d model=%d region=%d pos=(%d,%d,%d) flags=%#x\n",
                environmentItem->mType, environmentItem->mModel, environmentItem->mRegion,
                environmentItem->mPos.vx >> 12, environmentItem->mPos.vy >> 12,
                environmentItem->mPos.vz >> 12, environmentItem->mFlags);
            environmentItem = environmentItem->mNextItem;
            ++itemCount;
        }
        fprintf(stderr, "ENVOBJ count=%d\n", itemCount);
        for (i32 regionIndex = 0; regionIndex < MAXPSX; ++regionIndex)
            if (PSXRegion[regionIndex].Filename[0])
                fprintf(stderr, "PSXREGION %d %s usable=%d\n", regionIndex, PSXRegion[regionIndex].Filename, PSXRegion[regionIndex].Usable);
    }
#endif
}

} // namespace application
} // namespace spidey
