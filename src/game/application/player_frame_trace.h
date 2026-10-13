#pragma once

namespace spidey {
namespace application {

// Optional process diagnostics, kept outside gameplay update decisions.
class PlayerFrameTrace {
public:
    static void emit();
};

} // namespace application
} // namespace spidey
