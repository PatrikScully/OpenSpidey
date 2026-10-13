#pragma once
#include "core/types.h"
class CPlayer;

namespace spidey {
namespace player {

// Pre-action updates borrow the player. The suspension gate follows timer/effect
// work and precedes physics, targeting and analog/lookaround input.
class PlayerFramePreparation {
public:
    explicit PlayerFramePreparation(CPlayer& player) : player_(player) {}
    bool update(i32& verticalVelocityBeforePhysics);

private:
    void updateCostumeVisibility();
    void updateWaterEffect();
    void updateDamageTimers();
    void updateSenseEffect();
    void updateFistBonus();
    void initializeGroundContact();
    void regenerateWebbing();
    void updateSwingLine();
    void prepareSupportAndCountdown();
    void updateFramePhysics(i32& verticalVelocityBeforePhysics);
    void selectSurfaceCamera();
    void updateFriction();
    void updateTargetingAndAirborneClock();
    void updateLookaroundInput();
    void updateMovementRamp();
    CPlayer& player_;
};

} // namespace player
} // namespace spidey
