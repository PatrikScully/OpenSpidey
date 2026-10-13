#pragma once

#include "core/tick_clock.h"

namespace spidey {
namespace application {

// Game physics assumes two display ticks per frame (30 Hz on a 60 Hz clock).
// The experimental 60 Hz mode uses one tick and keeps the existing policy.
class FramePacer {
public:
    FramePacer(core::TickClock& clock, bool allow60FramesPerSecond)
        : clock_(clock), minimumFrameTicks_(allow60FramesPerSecond ? 1u : 2u) {}

    u32 currentTick() const { return clock_.currentTick(); }

    void waitForFrameEnd(u32 frameStartTick) {
        while (clock_.currentTick() - frameStartTick < minimumFrameTicks_)
            clock_.waitForTick();
    }

private:
    core::TickClock& clock_;
    const u32 minimumFrameTicks_;
};

} // namespace application
} // namespace spidey
