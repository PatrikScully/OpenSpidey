#pragma once
#include "core/fixed_vector.h"
#include "core/friction.h"

namespace spidey {
namespace world {

// Borrowed motion fields; works with an original body or independent simulation
// state. It changes no original actor layout or virtual dispatch.
struct ActorMotionState {
    CVector& position;
    CVector& velocity;
    CVector& acceleration;
    CSVector& angles;
    CSVector& angularVelocity;
    CSVector& angularAcceleration;
    const CFriction& angularFriction;
};

class ActorMotionIntegrator {
public:
    explicit ActorMotionIntegrator(ActorMotionState state) : state_(state) {}
    void advanceTwoTickSteps(i32 elapsedTicks, i32 gravityAcceleration);
    void advanceFixedStep(u32 stepScale, i32 gravityAcceleration);

private:
    void updateAcceleration(i32 gravityAcceleration);
    void advanceAngles(u32 yawDelta, u32 angularVelocityDelta);
    ActorMotionState state_;
};

} // namespace world
} // namespace spidey
