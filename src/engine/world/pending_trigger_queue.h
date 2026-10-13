#pragma once
#include "engine/world/trigger_records.h"
#include <cstddef>

namespace spidey {
namespace world {
class TriggerCommandExecutor {
public:
    virtual ~TriggerCommandExecutor() {}
    virtual void execute(u16* commands, u16 node) = 0;
};

// Borrow queue slots and command bytes; neither is freed by this view.
class PendingTriggerQueue {
public:
    PendingTriggerQueue(PendingListEntry* entries, std::size_t capacity)
        : entries_(entries), capacity_(capacity) {}
    bool append(u16 node, u16* commands);
    void execute(TriggerCommandExecutor& executor);
    void clear();
private:
    PendingListEntry* entries_;
    std::size_t capacity_;
};
} // namespace world
} // namespace spidey
