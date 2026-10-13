#pragma once
#include "game/player/player_motion.h"

namespace spidey {
namespace player {

class CrawlMotion {
public:
    // Acceleration and friction apply once per update. Extra elapsed ticks
    // extend displacement using half the damped velocity, with original rounding.
    static bool advance(CVector& position, CVector& velocity, const CVector& acceleration,
        const CFriction& friction, i32 elapsedTicks) {
        PlayerMotion::updateVelocity(velocity, acceleration, friction);
        if ((velocity.vx | velocity.vy | velocity.vz) == 0) return false;
        position += PlayerMotion::displacement(velocity, elapsedTicks);
        return true;
    }
    static i32 normalAlignment(const CSVector& left, const CSVector& right) {
        using namespace core;
        return wrappingAdd(wrappingAdd(arithmeticShiftRight(wrappingMultiply(left.vx, right.vx), 12),
            arithmeticShiftRight(wrappingMultiply(left.vy, right.vy), 12)),
            arithmeticShiftRight(wrappingMultiply(left.vz, right.vz), 12));
    }
};

} // namespace player
} // namespace spidey
