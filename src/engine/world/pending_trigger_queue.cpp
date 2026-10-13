#include "engine/world/pending_trigger_queue.h"

namespace spidey {
namespace world {
bool PendingTriggerQueue::append(u16 node, u16* commands) {
    for (std::size_t slot = 0; slot < capacity_; ++slot) {
        if (!entries_[slot].pCommands) {
            entries_[slot].NodeIndex = node;
            entries_[slot].pCommands = commands;
            return true;
        }
    }
    return false;
}

void PendingTriggerQueue::execute(TriggerCommandExecutor& executor) {
    // Read the live tail: callbacks can append commands for this same pass.
    // Consumed slots stay occupied until the pass ends, as in the original.
    for (std::size_t slot = 0; slot < capacity_ && entries_[slot].pCommands; ++slot)
        executor.execute(entries_[slot].pCommands, entries_[slot].NodeIndex);
    clear();
}

void PendingTriggerQueue::clear() {
    for (std::size_t slot = 0; slot < capacity_; ++slot) {
        entries_[slot].NodeIndex = 0;
        entries_[slot].pCommands = nullptr;
    }
}
} // namespace world
} // namespace spidey
