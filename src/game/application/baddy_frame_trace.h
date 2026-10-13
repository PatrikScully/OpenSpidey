#pragma once

namespace spidey {
namespace application {

// Optional actor diagnostics, sampled every thirty display frames.
class BaddyFrameTrace {
public:
    static void emit();
};

} // namespace application
} // namespace spidey
