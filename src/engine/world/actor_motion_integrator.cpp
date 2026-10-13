#include "engine/world/actor_motion_integrator.h"
#include "core/wrapping_math.h"

namespace spidey {
namespace world {
using core::signedWord;
using core::signedHalfWord;
using core::wrappingAdd;
using core::wrappingSubtract;
using core::wrappingMultiply;
using core::arithmeticShiftRight;

namespace {
i16 dampAngularVelocity(i16 value, unsigned friction) {
    i16 damped = signedHalfWord(static_cast<u32>(value)
        - static_cast<u32>(arithmeticShiftRight(value, friction)));
    return damped >= -1 && damped <= 1 ? 0 : damped;
}

i32 twoTickDisplacement(i32 velocity, i32 acceleration) {
    return wrappingAdd(wrappingMultiply(2, velocity),
        wrappingMultiply(4, acceleration) / 2);
}

i32 fixedStepDisplacement(i32 velocity, i32 acceleration,
    i32 stepScale, i32 squaredStepScale) {
    i32 velocityPart = arithmeticShiftRight(wrappingMultiply(stepScale, velocity), 12);
    i32 accelerationPart = arithmeticShiftRight(
        wrappingMultiply(squaredStepScale, acceleration) / 2, 12);
    return wrappingAdd(velocityPart, accelerationPart);
}
} // namespace

void ActorMotionIntegrator::updateAcceleration(i32 gravityAcceleration) {
    state_.acceleration.vx = 0;
    state_.acceleration.vy = wrappingSubtract(gravityAcceleration, state_.velocity.vy / 16);
    state_.acceleration.vz = 0;
}

void ActorMotionIntegrator::advanceAngles(u32 yawDelta, u32 angularVelocityDelta) {
    state_.angles.vy = signedHalfWord(static_cast<u32>(state_.angles.vy) + yawDelta);
    state_.angles.vx = static_cast<i16>(static_cast<u16>(state_.angles.vx) & 0xFFF);
    state_.angles.vy = static_cast<i16>(static_cast<u16>(state_.angles.vy) & 0xFFF);
    state_.angles.vz = static_cast<i16>(static_cast<u16>(state_.angles.vz) & 0xFFF);
    state_.angularVelocity.vy = signedHalfWord(
        static_cast<u32>(state_.angularVelocity.vy) + angularVelocityDelta);
    state_.angularVelocity.vx = dampAngularVelocity(state_.angularVelocity.vx, state_.angularFriction.vx);
    state_.angularVelocity.vy = dampAngularVelocity(state_.angularVelocity.vy, state_.angularFriction.vy);
    state_.angularVelocity.vz = dampAngularVelocity(state_.angularVelocity.vz, state_.angularFriction.vz);
}

void ActorMotionIntegrator::advanceTwoTickSteps(i32 elapsedTicks, i32 gravityAcceleration) {
    for (u32 tick = 0; elapsedTicks > 0 && tick < static_cast<u32>(elapsedTicks); tick += 2) {
        updateAcceleration(gravityAcceleration);
        state_.velocity.vx = wrappingAdd(state_.velocity.vx, wrappingMultiply(2, state_.acceleration.vx));
        state_.velocity.vy = wrappingAdd(state_.velocity.vy, wrappingMultiply(2, state_.acceleration.vy));
        state_.velocity.vz = wrappingAdd(state_.velocity.vz, wrappingMultiply(2, state_.acceleration.vz));
        state_.position.vx = wrappingAdd(state_.position.vx,
            twoTickDisplacement(state_.velocity.vx, state_.acceleration.vx));
        state_.position.vy = wrappingAdd(state_.position.vy,
            twoTickDisplacement(state_.velocity.vy, state_.acceleration.vy));
        state_.position.vz = wrappingAdd(state_.position.vz,
            twoTickDisplacement(state_.velocity.vz, state_.acceleration.vz));
        advanceAngles(static_cast<u32>(state_.angularVelocity.vy) * 2,
            static_cast<u32>(state_.angularAcceleration.vy) * 2);
    }
}

void ActorMotionIntegrator::advanceFixedStep(u32 stepScale, i32 gravityAcceleration) {
    updateAcceleration(gravityAcceleration);
    i32 scale = signedWord(stepScale);
    i32 squaredScale = signedWord((stepScale * stepScale) >> 12);
    state_.velocity.vx = wrappingAdd(state_.velocity.vx,
        arithmeticShiftRight(wrappingMultiply(scale, state_.acceleration.vx), 12));
    state_.velocity.vy = wrappingAdd(state_.velocity.vy,
        arithmeticShiftRight(wrappingMultiply(scale, state_.acceleration.vy), 12));
    state_.velocity.vz = wrappingAdd(state_.velocity.vz,
        arithmeticShiftRight(wrappingMultiply(scale, state_.acceleration.vz), 12));
    state_.position.vx = wrappingAdd(state_.position.vx,
        fixedStepDisplacement(state_.velocity.vx, state_.acceleration.vx, scale, squaredScale));
    state_.position.vy = wrappingAdd(state_.position.vy,
        fixedStepDisplacement(state_.velocity.vy, state_.acceleration.vy, scale, squaredScale));
    state_.position.vz = wrappingAdd(state_.position.vz,
        fixedStepDisplacement(state_.velocity.vz, state_.acceleration.vz, scale, squaredScale));
    // The original multiplication and shift here are unsigned, before the low
    // sixteen bits are added to the angle/angular-velocity records.
    advanceAngles((static_cast<u32>(state_.angularVelocity.vy) * stepScale) >> 12,
        (static_cast<u32>(state_.angularAcceleration.vy) * stepScale) >> 12);
}

} // namespace world
} // namespace spidey
