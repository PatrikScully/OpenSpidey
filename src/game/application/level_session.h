#pragma once

#include "core/types.h"

namespace spidey {
namespace application {

class FramePacer;

// Runs one loaded level. Scene preparation, world updates, presentation and
// level-exit cleanup stay in their original order behind the PlayAway export.
class LevelSession {
public:
    explicit LevelSession(FramePacer& framePacer) : framePacer_(framePacer) {}
    void run();

private:
    bool begin();
    bool advanceFrame();
    void drawFrameRate();
    void presentFrame(u32 frameStartTick);
    void finish();

    FramePacer& framePacer_;
};

} // namespace application
} // namespace spidey
