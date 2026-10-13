#pragma once

#include "core/types.h"

namespace spidey {
namespace core {

// A monotonic tick counter. Unsigned subtraction handles counter rollover.
class TickClock {
public:
    virtual ~TickClock() {}
    virtual u32 currentTick() const = 0;
    virtual void waitForTick() = 0;
};

} // namespace core
} // namespace spidey
