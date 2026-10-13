#pragma once
#include "core/fixed_vector.h"

namespace spidey {
namespace player {

// Integer movement policies shared by the original player physics modes.
class PlayerMotion {
public:
    static void updateVelocity(CVector& velocity, const CVector& acceleration, const CFriction& friction) {
        velocity += acceleration;
        velocity %= friction;
        velocity.KillSmall();
    }
    static CVector displacement(const CVector& velocity, i32 elapsedTicks) {
        if (elapsedTicks <= 2) return velocity;
        return velocity + (velocity >> 1) * core::wrappingSubtract(elapsedTicks, 2);
    }
    static CVector collisionFreeDisplacement(const CVector& velocity, i32 elapsedTicks) {
        if (elapsedTicks <= 2) return velocity;
        return velocity + velocity * core::wrappingSubtract(elapsedTicks, 2);
    }
    static CVector velocityFromDisplacement(const CVector& move, i32 elapsedTicks) {
        return elapsedTicks <= 2 ? move : (move << 1) / elapsedTicks;
    }
    static i32 fallDisplacement(i32 velocity, i32 elapsedTicks) {
        if (elapsedTicks <= 2) return velocity;
        return core::wrappingAdd(velocity, core::wrappingMultiply(
            core::wrappingSubtract(elapsedTicks, 2), core::arithmeticShiftRight(velocity, 1)));
    }
    static i32 dampedValue(i32 value, u8 shift) {
        return core::wrappingSubtract(value, core::arithmeticShiftRight(value, shift));
    }
    static void slideHorizontally(CVector& move, const CVector& normal) {
        using namespace core;
        const i32 dot = wrappingAdd(wrappingMultiply(arithmeticShiftRight(move.vz, 6), normal.vz),
            wrappingMultiply(arithmeticShiftRight(move.vx, 6), normal.vx));
        if (dot > 0) return;
        const i32 scale = arithmeticShiftRight(dot, 12);
        move.vx = arithmeticShiftRight(wrappingSubtract(move.vx,
            arithmeticShiftRight(wrappingMultiply(scale, normal.vx), 6)), 2);
        move.vz = arithmeticShiftRight(wrappingSubtract(move.vz,
            arithmeticShiftRight(wrappingMultiply(scale, normal.vz), 6)), 2);
    }
};

} // namespace player
} // namespace spidey
