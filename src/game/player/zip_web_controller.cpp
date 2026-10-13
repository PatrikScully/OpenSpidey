#include "game/player/zip_web_controller.h"
#include "game/player/player_animation_controller.h"
#include "game/player/spidey.h"
#include "game/player/web.h"
#include "game/cinematics/camera.h"
#include "engine/world/collision.h"
#include "engine/world/m3dzone.h"
#include "core/utils.h"

namespace spidey {
namespace player {
namespace {
enum {
    GroundedState = 1,
    SwingingOrZippingStates = 6,
    ZipWebState = 0x40000,
    RestrictedFaceFlag = 0x40000,
    MinimumCeilingNormalY = -2600,
    StraightZipReach = 3072,
    SwingZipRange = 2048,
    StraightZipButtonOffset = 0x60,
    SwingButtonOffset = 0x70,
    PracticeSwingButtonOffset = 0x100,
    StandingZipAnimation = 0xFA,
    CrouchedZipAnimation = 0x104,
    AirborneZipAnimation = 0x10E
};
}

bool ZipWebController::canStart() const {
    return player_.field_8EA == 0 && player_.mHeldObject == nullptr && player_.field_550 == 0;
}

bool ZipWebController::findTarget(const CVector& rayEnd, int availabilityRange, SLineInfo& target) {
    target.StartCoords = player_.mPos;
    target.EndCoords = rayEnd;
    target.MinCoords.Set(0, 0, 0);
    target.MaxCoords.Set(0, 0, 0);
    target.Position.Set(0, 0, 0);
    target.Normal.Set(0, 0, 0);
    M3dColij_InitLineInfo(&target);
    M3dZone_LineToItem(&target, 1);
    if (!target.pItem) return false;
    if ((target.pFace[3] & RestrictedFaceFlag) && target.Normal.vy >= MinimumCeilingNormalY) {
        return false;
    }
    return player_.CheckZipWebAvailability(&target, availabilityRange) != 0;
}

void ZipWebController::startZip(const SLineInfo& target, bool releaseLookaroundTarget) {
    player_.mInteractionTargetPosition = target.Position;
    player_.field_DA0.Set(target.Normal.vx, target.Normal.vy, target.Normal.vz);
    player_.field_8ED = 0;
    player_.field_558 = player_.mPos;
    player_.mHeadingTurnTicksRemaining = 0;

    PlayerAnimationController animations(player_);
    if (player_.mActionState & GroundedState) {
        animations.play(player_.mIsCrawling ? CrouchedZipAnimation : StandingZipAnimation);
    } else {
        if (releaseLookaroundTarget && player_.field_E64) {
            i32* previousTarget = player_.field_E64;
            (*(void(**)(i32*, i32))*previousTarget)(previousTarget, 1);
            player_.field_E64 = nullptr;
            player_.field_54C = 0;
            G_CAMERA_LIST->field_12C = -1;
        }
        CWeb* web = new CWeb();
        player_.field_E6C = reinterpret_cast<i32*>(web);
        web->field_102 = 0;
        web->field_F8 = static_cast<u8>(player_.field_5E8);
        CSVector surfaceNormal(target.Normal.vx, target.Normal.vy, target.Normal.vz);
        player_.field_8F8 = 8;
        player_.field_E10 = 1;
        player_.FireWeb(false, 128, &player_.mInteractionTargetPosition, true, &surfaceNormal);
        player_.field_E10 = 0;
        animations.play(AirborneZipAnimation, 13);
    }
    player_.mActionState = ZipWebState;
}

bool ZipWebController::tryStraightZip() {
    if (!canStart()) return false;
    const u8* controls = reinterpret_cast<const u8*>(player_.field_E0C);
    if (!controls[StraightZipButtonOffset]) return false;
    const CVector rayEnd = player_.mPos + player_.mUpDirection * StraightZipReach;
    SLineInfo target;
    if (!findTarget(rayEnd, StraightZipReach, target)) return false;
    startZip(target, true);
    return true;
}

bool ZipWebController::trySwingZip() {
    if (!canStart()) return false;
    const u8* controls = reinterpret_cast<const u8*>(player_.field_E0C);
    const bool practiceMode = *reinterpret_cast<const u8*>(0x0060CFC7) != 0;
    const bool directInput = controls[SwingButtonOffset] && (!practiceMode || player_.mSynthesizedInputActive != 0);
    const bool practiceInput = practiceMode && (player_.mActionState & SwingingOrZippingStates) &&
                               player_.field_E8D != 0 && controls[PracticeSwingButtonOffset];
    if (!directInput && !practiceInput) return false;
    const int forwardReach = player_.mActionState & SwingingOrZippingStates ? 256 - Rnd(512) : 512;
    const CVector rayEnd = player_.mPos - player_.mForwardDirection * SwingZipRange +
                          player_.mUpDirection * forwardReach;
    SLineInfo target;
    if (!findTarget(rayEnd, SwingZipRange, target)) return false;
    startZip(target, false);
    return true;
}

} // namespace player
} // namespace spidey
