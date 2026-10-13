#pragma once
#include "core/fixed_vector.h"

namespace spidey {
namespace player {

// Original web-swing phase policy. The half-turn begins at 2048 and finishes
// at 4096; sampling masks to a full turn, but completion uses the full phase.
class SwingArc {
public:
    static i32 advancePhase(i32 phase, i32 elapsedTicks, i32 phaseSpeed) {
        return core::wrappingAdd(phase, core::wrappingMultiply(elapsedTicks, phaseSpeed));
    }
    static i16 pitchAngle(i16 amplitude, i16 phaseCosine) {
        return core::signedHalfWord(core::arithmeticShiftRight(
            core::wrappingMultiply(amplitude, i32(phaseCosine) + 4096), 12));
    }
    static i32 animationFrame(i32 frameScale, i32 phase) {
        return core::arithmeticShiftRight(core::wrappingMultiply(frameScale,
            core::wrappingSubtract(phase, 2048)), 11);
    }
    static bool completed(i32 phase) { return phase >= 4096; }
    static CVector endPosition(const CVector& anchor, i32 length, const CVector& down) {
        return anchor - length * down;
    }
};

} // namespace player
} // namespace spidey
