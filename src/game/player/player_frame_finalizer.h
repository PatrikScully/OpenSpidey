#pragma once
#include "core/fixed_vector.h"

class CPlayer;
struct SLineInfo;

namespace spidey {
namespace player {

struct PlayerHandHooks {
    CVector left;
    CVector right;
};

// Shared updates run after one action handler, including unknown action words.
// The player is borrowed; hook positions and query storage live for one update.
class PlayerFrameFinalizer {
public:
    explicit PlayerFrameFinalizer(CPlayer& player) : player_(player) {}
    void update();

private:
    void updateCameraFacing(u8* input);
    i32 updateTorsoAim();
    void updateHeadingAndGravity();
    void updateMovementVelocity(i32 cameraYaw);
    void rideSupportBody(SLineInfo& lineInfo);
    void updateVerticalImpulse();
    void resolveActorContacts();
    void collectPowerUps();
    void separateStandingPlayerFromActors();
    void sweepTriggerZones(SLineInfo& lineInfo);
    void buildPose();
    void updateHeadTracking();
    void updateAdditionalBodyParts(PlayerHandHooks& hooks);
    void updateFlamingFists(const PlayerHandHooks& hooks);
    void updateAttachments(PlayerHandHooks& hooks);
    void syncFistsAndFinishInput(u8* input);
    CPlayer& player_;
};

} // namespace player
} // namespace spidey
