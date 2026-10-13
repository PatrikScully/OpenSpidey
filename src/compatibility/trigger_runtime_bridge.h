#pragma once
#include "engine/world/command_point_registry.h"

namespace spidey {
namespace compatibility {
world::PendingTriggerQueue pendingTriggerQueue();
world::CommandPointRegistry triggerCommandPoints();
} // namespace compatibility
} // namespace spidey
