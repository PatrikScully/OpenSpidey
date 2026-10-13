#pragma once
#include "core/fixed_vector.h"
class CPlayer;
class CBody;
struct SLineInfo;

namespace spidey {
namespace player {

// Borrow the original policy cells so changes remain visible at each decision.
struct PlayerPhysicsPolicy {
    const i32& limitVerticalMoveAfterCollision;
    const i32& limitVerticalFallAfterGroundHit;
    const float& groundProbeSpread;
    const i32& wideGroundProbeEnabled;
};

class PlayerPhysicsController {
public:
    PlayerPhysicsController(CPlayer& player, PlayerPhysicsPolicy policy) : player_(player), policy_(policy) {}
    void update();

private:
    bool collisionFreeZipWindow() const;
    void constrainArenaMovement();
    void sweepMovement(SLineInfo& hit);
    void updateWallProbeDirection();
    void clipVerticalTravel(CBody* previousSupport, i32 initialY);
    void probeWideGround(SLineInfo& ground, float x, float z, float startY, float endY);
    void resolveGroundFall(i32 savedFallVelocity);
    void respondToGround(SLineInfo& ground, i32 fall, i32 savedFallVelocity);
    void notifySupportBody(const SLineInfo& ground);
    CPlayer& player_;
    PlayerPhysicsPolicy policy_;
    CVector initialPosition_;
    CVector move_;
};

} // namespace player
} // namespace spidey
