#include "engine/world/actor_motion_integrator.h"
#include "core/wrapping_math.h"
#include <cassert>
#include <limits>

struct Motion {
    CVector position, velocity, acceleration;
    CSVector angles, angularVelocity, angularAcceleration;
    CFriction angularFriction;
    Motion() : angularFriction(2, 2, 2) {}
    spidey::world::ActorMotionIntegrator integrator() {
        spidey::world::ActorMotionState state = {position, velocity, acceleration,
            angles, angularVelocity, angularAcceleration, angularFriction};
        return spidey::world::ActorMotionIntegrator(state);
    }
};

int main() {
    using namespace spidey::core;
    const i32 maximum = std::numeric_limits<i32>::max();
    const i32 minimum = std::numeric_limits<i32>::min();
    assert(wrappingAdd(maximum, 1) == minimum);
    assert(wrappingSubtract(minimum, 1) == maximum);
    assert(wrappingMultiply(maximum, 2) == -2);
    assert(signedHalfWord(0xFFFF) == -1);
    assert(arithmeticShiftRight(-17, 4) == -2);
    assert(arithmeticShiftRight(minimum, 31) == -1);
    assert(arithmeticShiftRight(minimum, 32) == minimum);

    Motion twoTicks;
    twoTicks.angles.Set(-1, 4000, -2);
    twoTicks.angularVelocity.Set(-1, 100, 1);
    twoTicks.angularAcceleration.vy = 10;
    twoTicks.integrator().advanceTwoTickSteps(2, 8192);
    assert(twoTicks.acceleration.vy == 8192);
    assert(twoTicks.velocity.vy == 16384);
    assert(twoTicks.position.vy == 49152);
    assert(twoTicks.angles.vx == 4095 && twoTicks.angles.vy == 104 && twoTicks.angles.vz == 4094);
    assert(twoTicks.angularVelocity.vx == 0 && twoTicks.angularVelocity.vy == 90
        && twoTicks.angularVelocity.vz == 0);

    Motion oddTicks;
    oddTicks.velocity.Set(3, -17, -4);
    oddTicks.integrator().advanceTwoTickSteps(3, 0);
    assert(oddTicks.position.vx == 12 && oddTicks.position.vy == -58 && oddTicks.position.vz == -16);
    assert(oddTicks.velocity.vy == -15);

    Motion stopped;
    stopped.acceleration.Set(99, -101, 103);
    stopped.angles.vy = -17;
    stopped.angularVelocity.vy = 100;
    stopped.integrator().advanceTwoTickSteps(0, 8192);
    stopped.integrator().advanceTwoTickSteps(-1, 8192);
    assert(stopped.acceleration.vy == -101 && stopped.angles.vy == -17);
    assert(stopped.angularVelocity.vy == 100);

    Motion oneFixedTick;
    oneFixedTick.integrator().advanceFixedStep(4096, 8192);
    assert(oneFixedTick.velocity.vy == 8192 && oneFixedTick.position.vy == 12288);
    Motion twoFixedTicks;
    twoFixedTicks.integrator().advanceFixedStep(8192, 8192);
    assert(twoFixedTicks.velocity.vy == 16384 && twoFixedTicks.position.vy == 49152);
    Motion negativeVelocity;
    negativeVelocity.velocity.vy = -17;
    negativeVelocity.integrator().advanceFixedStep(4096, 0);
    assert(negativeVelocity.acceleration.vy == 1);
    assert(negativeVelocity.velocity.vy == -16 && negativeVelocity.position.vy == -16);

    Motion zeroScale;
    zeroScale.position.vx = 123;
    zeroScale.angles.vy = -1;
    zeroScale.angularVelocity.vy = 100;
    zeroScale.integrator().advanceFixedStep(0, 8192);
    assert(zeroScale.position.vx == 123 && zeroScale.velocity.vy == 0);
    assert(zeroScale.acceleration.vy == 8192);
    assert(zeroScale.angles.vy == 4095 && zeroScale.angularVelocity.vy == 75);

    Motion wrapped;
    wrapped.velocity.vx = maximum;
    wrapped.integrator().advanceFixedStep(8192, 0);
    assert(wrapped.position.vx == -2);
}
