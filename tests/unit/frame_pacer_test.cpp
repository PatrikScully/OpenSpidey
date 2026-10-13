#include "game/application/frame_pacer.h"
#include <cassert>
#include <limits>

class TestTickClock : public spidey::core::TickClock {
public:
    explicit TestTickClock(u32 tick = 0) : tick(tick), waits(0), ticksPerWait(1) {}
    u32 currentTick() const override { return tick; }
    void waitForTick() override { tick += ticksPerWait; ++waits; }

    u32 tick;
    unsigned waits;
    u32 ticksPerWait;
};

int main() {
    using spidey::application::FramePacer;
    TestTickClock clock(100);
    FramePacer normal(clock, false);
    normal.waitForFrameEnd(normal.currentTick());
    assert(clock.tick == 102 && clock.waits == 2);

    // Rendering already consumed one tick: wait for only the remaining one.
    const u32 frameStart = clock.tick;
    ++clock.tick;
    normal.waitForFrameEnd(frameStart);
    assert(clock.tick == 104 && clock.waits == 3);

    // Slow frames and a delayed wake-up must not add another frame delay.
    clock.tick += 5;
    normal.waitForFrameEnd(frameStart);
    assert(clock.waits == 3);
    clock.ticksPerWait = 3;
    normal.waitForFrameEnd(clock.tick);
    assert(clock.waits == 4);

    TestTickClock fastClock;
    FramePacer fast(fastClock, true);
    fast.waitForFrameEnd(0);
    assert(fastClock.tick == 1 && fastClock.waits == 1);
    fast.waitForFrameEnd(0);
    assert(fastClock.waits == 1);

    // A long session may cross the 32-bit display tick boundary.
    TestTickClock rolloverClock(std::numeric_limits<u32>::max());
    FramePacer rollover(rolloverClock, false);
    rollover.waitForFrameEnd(rolloverClock.tick);
    assert(rolloverClock.tick == 1 && rolloverClock.waits == 2);
}
