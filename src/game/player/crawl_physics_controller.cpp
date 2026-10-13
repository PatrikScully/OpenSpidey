#include "game/player/crawl_physics_controller.h"
#include "game/player/crawl_motion.h"
#include "game/player/crawl_frame_trace.h"
#include "game/player/spidey.h"
#include "engine/world/m3dzone.h"
#include "engine/rendering/m3dutils.h"
#include "game/objects/platform.h"

namespace spidey {
namespace player {
namespace {
const i32 sameSurfaceAlignment = 3271;
const i16 positiveVerticalNormalThreshold = 3400;
const u32 nonCrawlableFace = 0x40000;
enum SurfaceFlags { NonCrawlable = 4, DarkSurface = 0x80, HardTransition = 0x100, Fence = 0x800 };
CVector longNormal(const CSVector& normal) { return CVector(normal.vx, normal.vy, normal.vz); }
}

void CrawlPhysicsController::trace(SLineInfo& line, const CVector& start, const CVector& end) {
    line.StartCoords = start;
    line.EndCoords = end;
    M3dColij_InitLineInfo(&line);
    M3dZone_LineToItem(&line, 1);
}

void CrawlPhysicsController::update() {
    print_if_false(player_.mIsCrawling, "Error");
    CrawlFrameTrace::beginFrame(player_);
    player_.mCollision = 0;
    player_.mUsesHardSurfaceTransitions = 0;
    player_.mFenceSurfaceDetected = 0;
    previousPosition_ = player_.mPos;
    const bool moved = CrawlMotion::advance(player_.mPos, player_.mVel, player_.mAcc,
        player_.mFric, player_.mElapsedTicks);
    CVector hook;
    M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hook), &player_, 2);
    if (moved) probeForwardAndCorners(hook);

    SLineInfo ground;
    traceGround(ground);
    if (!ground.pItem) {
        recoverMissingSurface(ground);
        return;
    }
    respondToSurface(ground, moved);
}

void CrawlPhysicsController::probeForwardAndCorners(const CVector& hook) {
    SLineInfo& line = player_.mLineInfo;
    const CVector backward = -96 * player_.mForwardDirection;
    trace(line, hook - backward / 4, hook + backward);
    CrawlFrameTrace::forwardProbe(player_, hook, backward);
    if (!line.pItem) {
        probeSideCorners(hook);
        return;
    }
    if (CrawlMotion::normalAlignment(line.Normal, player_.crawlSurfaceNormal()) > sameSurfaceAlignment) {
        line.pItem = nullptr;
        return; // A rejected forward hit does not run side-corner probes.
    }
    player_.mCollision |= 1;
    player_.mPos = previousPosition_;
    if ((line.pFace[3] & nonCrawlableFace) && (!player_.field_8E8 || line.Normal.vy >= -2600))
        line.pItem = nullptr;
    if (line.pItem)
        player_.mForwardCollisionClearance = line.Distance >= 80 ? 0 : core::wrappingSubtract(88, line.Distance);
}

void CrawlPhysicsController::probeSideCorners(const CVector& hook) {
    SLineInfo side;
    trace(side, hook, hook + player_.mRightDirection * 16);
    if (side.pItem) {
        CrawlFrameTrace::sideHit(player_, true);
        player_.mPos = previousPosition_;
    } else {
        trace(side, hook, hook - player_.mRightDirection * 16);
        if (side.pItem) {
            CrawlFrameTrace::sideHit(player_, false);
            player_.mPos = previousPosition_;
        }
    }
    SLineInfo& line = player_.mLineInfo;
    const CVector base = line.EndCoords - 70 * player_.mUpDirection;
    trace(line, base, base + player_.mForwardDirection * 128);
    if (line.pItem && !(line.pFace[3] & nonCrawlableFace)
        && CrawlMotion::normalAlignment(line.Normal, player_.crawlSurfaceNormal()) <= sameSurfaceAlignment) {
        player_.mExteriorCornerDetected = 1;
        CrawlFrameTrace::cornerHit(player_);
        player_.mPos = previousPosition_;
        player_.mCornerTransitionDistance = line.Distance <= 16 ? 0 : core::wrappingSubtract(line.Distance, 8);
    } else {
        line.pItem = nullptr;
    }
}

void CrawlPhysicsController::traceGround(SLineInfo& line) {
    line.StartCoords = player_.mPos + 40 * player_.mUpDirection;
    line.EndCoords = player_.mPos - 140 * player_.mUpDirection;
    M3dColij_InitLineInfo(&line);
    CrawlFrameTrace::beginGroundQuery(player_);
    M3dZone_LineToItem(&line, 1);
    CrawlFrameTrace::endGroundQuery(player_, line);
}

void CrawlPhysicsController::recoverMissingSurface(SLineInfo& ground) {
    player_.mCollision |= 2;
    const CVector start = player_.mPos - 96 * player_.mForwardDirection - 70 * player_.mUpDirection;
    trace(ground, start, start + (player_.mForwardDirection << 7));
    if (ground.pItem && !(ground.pFace[3] & nonCrawlableFace)) {
        // Only these hit fields were copied originally; leave face/distance intact.
        player_.mLineInfo.pItem = ground.pItem;
        player_.mLineInfo.Position = ground.Position;
        player_.mLineInfo.Normal = ground.Normal;
        player_.mExteriorCornerDetected = 1;
    }
    player_.mPos = previousPosition_;
    trace(ground, player_.mPos + 40 * player_.mUpDirection, player_.mPos - 140 * player_.mUpDirection);
    if (!ground.pItem) player_.mCollision &= ~2;
    // Recovery stops here even if its final ground probe hits.
}

void CrawlPhysicsController::updateSurfaceControls(const SLineInfo& ground) {
    const bool newPositiveVertical = ground.Normal.vy > positiveVerticalNormalThreshold;
    const bool oldPositiveVertical = player_.crawlSurfaceNormal().vy > positiveVerticalNormalThreshold;
    if (newPositiveVertical == oldPositiveVertical) return;
    const CSVector previousNormal = player_.mLineInfo.Normal;
    if (newPositiveVertical) player_.mOrientationReferenceDirection = player_.mForwardDirection;
    player_.mLineInfo.Normal = ground.Normal;
    player_.HandleControlsForSurfaceTransition(true);
    player_.mLineInfo.Normal = previousNormal;
}

void CrawlPhysicsController::respondToSurface(SLineInfo& ground, bool moved) {
    M3dColij_LineInfoFixup(&ground);
    player_.mGroundContactGraceUpdates = 4;
    const u32 flags = ground.pFace[3] >> 16;
    player_.AdjustBrightness(flags & DarkSurface ? player_.field_574 : player_.field_578);
    player_.mCollision |= 2;
    if (flags & NonCrawlable) {
        if (flags & Fence) player_.mFenceSurfaceDetected = 1;
        else player_.mPos = previousPosition_;
        return;
    }
    print_if_false((~(flags >> 11)) & 1, "FU_FENCE not flagged FU_NONCRAWLABLE");
    updateSurfaceControls(ground);
    if (flags & HardTransition) player_.mUsesHardSurfaceTransitions = 1;
    const CVector target = ground.Position + i32(player_.mSurfaceClearance) * longNormal(ground.Normal);
    if (moved) player_.mPos = target;
    else {
        const CVector correction = target - player_.mPos;
        CVector lengthInput = correction; // Original GTE API is non-const.
        if (lengthInput.LengthSquared() > 8) player_.mPos += correction;
    }
    player_.crawlSurfaceNormal() = ground.Normal;
    player_.mShadowPos = ground.Position;
    notifySupportBody(ground);
}

void CrawlPhysicsController::notifySupportBody(const SLineInfo& ground) {
    if (!(ground.pItem->mFlags & 0x100)) return;
    CPlatform* platform = reinterpret_cast<CPlatform*>(ground.pItem);
    platform->NotifyTrodUpon(&player_, &player_.mPos, &player_.crawlSurfaceNormal());
    platform->AdjustBruceHealth();
    player_.mSupportBody = reinterpret_cast<CBody*>(ground.pItem);
}

} // namespace player
} // namespace spidey
