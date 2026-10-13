#include "game/player/player_physics_controller.h"
#include "game/player/player_motion.h"
#include "game/player/sweep_geometry.h"
#include "game/player/ground_probe_fan.h"
#include "game/player/spidey.h"
#include "game/actors/baddy.h"
#include "game/objects/platform.h"
#include "engine/world/m3dzone.h"
#include "engine/rendering/ps2funcs.h"
#include "core/utils.h"
#include "core/floating_word.h"

namespace spidey {
namespace player {

bool PlayerPhysicsController::collisionFreeZipWindow() const {
    return player_.mActionState == 0x40000
        && ((player_.mAnim == 270 && player_.mFrame >= 13) || player_.mAnim == 271);
}

void PlayerPhysicsController::update() {
    player_.mCollision = 0;
    player_.mExteriorCornerDetected = 0;
    player_.mLineInfo.pItem = nullptr;
    player_.mLineInfo2.pItem = nullptr;
    CBody* previousSupport = player_.mSupportBody;
    player_.mSupportBody = nullptr;
    if (player_.field_E64) { player_.DoSwingingPhysics(); return; }
    if (player_.mIsCrawling) { player_.DoCrawlingPhysics(); return; }

    initialPosition_ = player_.mPos;
    PlayerMotion::updateVelocity(player_.mVel, player_.mAcc, player_.mFric);
    if (collisionFreeZipWindow()) {
        player_.mPos += PlayerMotion::collisionFreeDisplacement(player_.mVel, player_.mElapsedTicks);
        return;
    }
    i32 savedFallVelocity = -1;
    if (player_.mVel.vy >= 0) {
        savedFallVelocity = player_.mVel.vy;
        player_.mVel.vy = 0;
    }
    move_ = PlayerMotion::displacement(player_.mVel, player_.mElapsedTicks);
    constrainArenaMovement();
    SLineInfo hit;
    hit.pItem = nullptr; // No horizontal sweep means no sweep hit.
    sweepMovement(hit);
    player_.mVel = PlayerMotion::velocityFromDisplacement(move_, player_.mElapsedTicks);
    const i32 initialY = player_.mPos.vy;
    if (!hit.pItem) player_.mPos += move_;
    else if (!policy_.limitVerticalMoveAfterCollision || move_.vy < 0)
        player_.mPos.vy = core::wrappingAdd(initialY, move_.vy);
    updateWallProbeDirection();
    clipVerticalTravel(previousSupport, initialY);
    if (savedFallVelocity >= 0) resolveGroundFall(savedFallVelocity);
    player_.mAngles.Mask();
}

void PlayerPhysicsController::constrainArenaMovement() {
    if (!player_.mArenaConstraintActive) return;
    const CVector relative = player_.mPos - reinterpret_cast<CItem*>(gBossRelated)->mPos;
    CVector radial(core::wrappingAdd(relative.vx, move_.vx), 0, core::wrappingAdd(relative.vz, move_.vz));
    if (radial.Length() == player_.mArenaConstraintRadius) return;
    radial >>= 8;
    VectorNormal(reinterpret_cast<VECTOR*>(&radial), reinterpret_cast<VECTOR*>(&radial));
    radial *= player_.mArenaConstraintRadius;
    move_.vx = core::wrappingSubtract(radial.vx, relative.vx);
    move_.vz = core::wrappingSubtract(radial.vz, relative.vz);
}

namespace {
void traceWorld(SLineInfo& hit, const CVector& start, const CVector& end) {
    hit.StartCoords = start;
    hit.EndCoords = end;
    M3dColij_InitLineInfo(&hit);
    M3dZone_LineToItem(&hit, 1);
}
}

void PlayerPhysicsController::sweepMovement(SLineInfo& hit) {
    i32 attempts = 0;
    i32 length = M3dMaths_SquareRoot0(SweepGeometry::scaledSquaredLength(move_));
    while (length != 0) {
        const i32 horizontalLength = M3dMaths_SquareRoot0(SweepGeometry::scaledSquaredLength(move_, true));
        if (horizontalLength == 0) break;
        const i32 reach = player_.mCollisionProbeReach;
        const CVector ray = SweepGeometry::mainRay(move_, reach, player_.mElapsedTicks, length);
        CVector origin = player_.mPos;
        origin.vy = core::wrappingSubtract(origin.vy, 0x40000);
        if (player_.mHeldObject) {
            CVector lowerOrigin = origin;
            lowerOrigin.vy = core::wrappingSubtract(lowerOrigin.vy, 0x80000);
            traceWorld(hit, lowerOrigin, lowerOrigin + ray);
            if (hit.pItem) player_.mCollision |= 0x40;
        }
        traceWorld(hit, origin - ray / 4, origin + ray);
        ++attempts;
        if (hit.pItem) {
            player_.mCollision = (player_.mCollision & 0xFFBF) | 1;
            // Preserve the selective copy: distance and other query data stay intact.
            player_.mLineInfo.pFace = hit.pFace;
            player_.mLineInfo.pItem = hit.pItem;
            player_.mLineInfo.Position = hit.Position;
            player_.mLineInfo.Normal = hit.Normal;
            print_if_false((hit.Normal.vx | hit.Normal.vy | hit.Normal.vz) != 0, "Bad normal");
            CVector horizontalNormal(hit.Normal.vx, 0, hit.Normal.vz);
            VectorNormal(reinterpret_cast<VECTOR*>(&horizontalNormal), reinterpret_cast<VECTOR*>(&horizontalNormal));
            PlayerMotion::slideHorizontally(move_, horizontalNormal);
        } else {
            const CVector side = SweepGeometry::shoulderSide(move_, reach, length);
            const CVector forward = SweepGeometry::shoulderForward(move_, reach, player_.mElapsedTicks, length, horizontalLength);
            origin = player_.mPos;
            origin.vy = core::wrappingAdd(origin.vy, 0x10000);
            CVector start = origin + side / 4 - forward / 2;
            start.vy = origin.vy; // Shoulders start horizontally even when moving upward.
            traceWorld(hit, start, origin + side + forward);
            if (!hit.pItem) {
                start = origin - forward / 2 - side / 4;
                start.vy = origin.vy;
                traceWorld(hit, start, origin - side + forward);
                if (!hit.pItem) break;
            }
            player_.mCollision = (player_.mCollision & 0xFFBF) | 1;
            // Shoulder hits use the raw normal, unlike the normalized main hit.
            PlayerMotion::slideHorizontally(move_, CVector(hit.Normal.vx, 0, hit.Normal.vz));
        }
        if (!hit.pItem || attempts >= 2) break;
        length = M3dMaths_SquareRoot0(SweepGeometry::scaledSquaredLength(move_));
    }
}

void PlayerPhysicsController::updateWallProbeDirection() {
    if (player_.mActionState != 1) return;
    SLineInfo& hit = player_.mLineInfo2;
    const CVector horizontalDirection(player_.mWallProbeDirection.vx, 0, player_.mWallProbeDirection.vz);
    traceWorld(hit, player_.mPos, player_.mPos + horizontalDirection);
    if (hit.pItem && hit.Normal.vy >= -2600 && hit.Normal.vy <= 3400) {
        player_.mVel.vx = core::wrappingAdd(player_.mVel.vx, 4 * hit.Normal.vx);
        player_.mVel.vz = core::wrappingAdd(player_.mVel.vz, 4 * hit.Normal.vz);
        player_.mWallProbeDirection.vx = -32 * hit.Normal.vx;
        player_.mWallProbeDirection.vz = -32 * hit.Normal.vz;
    } else {
        player_.mWallProbeHeading = static_cast<u16>(player_.mWallProbeHeading + Rnd(64) + 256);
        const i32 heading = player_.mWallProbeHeading & 0xFFF;
        player_.mWallProbeDirection.vx = core::wrappingShiftLeft(G_RCOSSIN_TBL[heading].sin, 5);
        player_.mWallProbeDirection.vz = core::wrappingShiftLeft(G_RCOSSIN_TBL[heading].cos, 5);
    }
}

void PlayerPhysicsController::clipVerticalTravel(CBody* previousSupport, i32 initialY) {
    if (player_.mVel.vy >= 0 && (!previousSupport || previousSupport->mVel.vy >= 0)) return;
    CVector start = player_.mPos, end = player_.mPos;
    start.vy = core::wrappingAdd(core::wrappingSubtract(player_.mPos.vy, move_.vy), 0x40000);
    end.vy = core::wrappingSubtract(player_.mPos.vy, 0xB8000);
    SLineInfo& hit = player_.mLineInfo2;
    traceWorld(hit, start, end);
    if (hit.pItem && core::wrappingSubtract(player_.mPos.vy, 0x78000) < hit.Position.vy) {
        player_.mPos.vy = initialY;
        player_.mVel.vy = 0;
        player_.mCollision = (player_.mCollision & 0xFFBF) | 0x100;
    }
}

void PlayerPhysicsController::probeWideGround(SLineInfo& ground, float x, float z, float startY, float endY) {
    const i32 fixedStartY = core::truncateToGameWord(startY * 4096.0f);
    const i32 fixedEndY = core::truncateToGameWord(endY * 4096.0f);
    for (unsigned index = 0; index <= 8; ++index) {
        const GroundProbePoint point = GroundProbeFan::point(x, z, policy_.groundProbeSpread, index);
        const i32 fixedX = core::truncateToGameWord(point.x * 4096.0f), fixedZ = core::truncateToGameWord(point.z * 4096.0f);
        traceWorld(ground, CVector(fixedX, fixedStartY, fixedZ), CVector(fixedX, fixedEndY, fixedZ));
        if (ground.pItem) break;
    }
}

void PlayerPhysicsController::resolveGroundFall(i32 savedFallVelocity) {
    const i32 fall = PlayerMotion::fallDisplacement(savedFallVelocity, player_.mElapsedTicks);
    SLineInfo ground;
    ground.StartCoords = player_.mPos;
    ground.StartCoords.vy = core::wrappingSubtract(player_.mPos.vy, 0x28000);
    ground.EndCoords = ground.StartCoords;
    ground.EndCoords.vy = core::wrappingAdd(core::wrappingAdd(fall, ground.StartCoords.vy), 0x8C000);
    const float x = float(ground.StartCoords.vx) / 4096.0f;
    const float z = float(ground.StartCoords.vz) / 4096.0f;
    const float startY = float(ground.StartCoords.vy) / 4096.0f;
    const float endY = float(ground.EndCoords.vy) / 4096.0f;
    M3dColij_InitLineInfo(&ground);
    M3dZone_LineToItem(&ground, 1);
    if (policy_.wideGroundProbeEnabled && !ground.pItem)
        probeWideGround(ground, x, z, startY, endY);
    if (!ground.pItem) {
        player_.mPos.vy = core::wrappingAdd(player_.mPos.vy, fall);
        player_.mVel.vy = savedFallVelocity;
        return;
    }
    respondToGround(ground, fall, savedFallVelocity);
}

void PlayerPhysicsController::respondToGround(SLineInfo& ground, i32 fall, i32 savedFallVelocity) {
    player_.mGroundContactGraceUpdates = 4;

    if (player_.mHeldObject != 0 && (ground.pFace[3] & 0x4000000) != 0)
    {
        // carrying something onto a surface flagged as no-go: undo the whole
        // frame's movement.
        player_.mPos = initialPosition_;
        player_.mCollision |= 2;
    }
    else if (ground.Normal.vy < -2600)
    {
        // Snap to a surface with a steep negative-y normal.
        ground.Position.vy = core::signedWord(static_cast<u32>(ground.Position.vy) & 0xFFFFF000u);
        player_.mPos.vy = core::wrappingSubtract(ground.Position.vy, i32(player_.mSurfaceClearance) << 12);
        player_.mVel.vy = 0;
        player_.mCollision |= 2;
    }
    else if (ground.Distance > 0)
    {
        // landed: sit on the surface and let the leftover fall slide along it.
        player_.mPos.vy = core::wrappingSubtract(ground.Position.vy, i32(player_.mSurfaceClearance) << 12);
        player_.mPos.vx = core::wrappingAdd(player_.mPos.vx, core::wrappingMultiply(
            core::arithmeticShiftRight(PlayerMotion::dampedValue(fall, player_.mFric.vx), 12), ground.Normal.vx));
        player_.mPos.vz = core::wrappingAdd(player_.mPos.vz, core::wrappingMultiply(
            core::arithmeticShiftRight(PlayerMotion::dampedValue(fall, player_.mFric.vz), 12), ground.Normal.vz));
        fall = PlayerMotion::dampedValue(fall, player_.mFric.vy);
        player_.mPos.vy = core::wrappingAdd(player_.mPos.vy, fall);
        player_.mVel.vy = savedFallVelocity;
    }
    else
    {
        if (policy_.limitVerticalFallAfterGroundHit == 0 || fall < 0)
            player_.mPos.vy = core::wrappingAdd(player_.mPos.vy, fall);

        player_.mVel.vy = savedFallVelocity;
    }

    player_.crawlSurfaceNormal().Set(0, -4096, 0);

    player_.mShadowPos = ground.Position;

    notifySupportBody(ground);

    if ((ground.pFace[3] & 0x800000) != 0)
        player_.AdjustBrightness(player_.field_574);
    else
        player_.AdjustBrightness(player_.field_578);

}

void PlayerPhysicsController::notifySupportBody(const SLineInfo& ground) {
    if (!(ground.pItem->mFlags & 0x100)) return;
    CPlatform* platform = reinterpret_cast<CPlatform*>(ground.pItem);
    platform->NotifyTrodUpon(&player_, &player_.mPos, &player_.crawlSurfaceNormal());
    platform->AdjustBruceHealth();
    player_.mSupportBody = platform;
}

} // namespace player
} // namespace spidey
