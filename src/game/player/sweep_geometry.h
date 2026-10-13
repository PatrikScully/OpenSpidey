#pragma once
#include "core/fixed_vector.h"

namespace spidey {
namespace player {

class SweepGeometry {
public:
    static i32 scaledSquaredLength(const CVector& move, bool horizontalOnly = false) {
        using namespace core;
        const i32 x = arithmeticShiftRight(move.vx, 9);
        const i32 y = horizontalOnly ? 0 : arithmeticShiftRight(move.vy, 9);
        const i32 z = arithmeticShiftRight(move.vz, 9);
        return wrappingAdd(wrappingAdd(wrappingMultiply(x, x), wrappingMultiply(z, z)), wrappingMultiply(y, y));
    }
    static i32 reachForTicks(i32 reach, i32 elapsedTicks) {
        if (elapsedTicks < 5) return reach;
        return core::wrappingAdd(reach, core::wrappingMultiply(core::wrappingSubtract(elapsedTicks, 4),
            core::arithmeticShiftRight(reach, 1)));
    }
    static CVector mainRay(const CVector& move, i32 reach, i32 elapsedTicks, i32 length) {
        const i32 adjustedReach = reachForTicks(reach, elapsedTicks);
        CVector ray(scale(move.vx, 8, adjustedReach, length), scale(move.vy, 8, reach, length),
            scale(move.vz, 8, adjustedReach, length));
        ray += ray >> 3;
        return ray;
    }
    static CVector shoulderSide(const CVector& move, i32 reach, i32 length) {
        return CVector(scale(move.vz, 4, reach, length), 0,
            core::wrappingSubtract(0, scale(move.vx, 4, reach, length)));
    }
    static CVector shoulderForward(const CVector& move, i32 reach, i32 elapsedTicks,
        i32 length, i32 horizontalLength) {
        const i32 adjustedReach = reachForTicks(reach, elapsedTicks);
        return CVector(scale(move.vx, 8, adjustedReach, horizontalLength), scale(move.vy, 8, reach, length),
            scale(move.vz, 8, adjustedReach, horizontalLength));
    }
private:
    // Keep both low-word products before the signed division, as in the executable.
    static i32 scale(i32 component, i32 multiplier, i32 reach, i32 length) {
        return core::wrappingMultiply(core::wrappingMultiply(component, multiplier), reach) / length;
    }
};

} // namespace player
} // namespace spidey
