#include "compatibility/trigger_runtime_bridge.h"
#include "core/game_heap.h"

namespace spidey {
namespace compatibility {
namespace {
class TriggerPointAllocator : public core::MemoryAllocator {
public:
    void* allocate(std::size_t size) override { return DCMem_New(size, 0, 1, 0, true); }
    void release(void* allocation) override { Mem_Delete(allocation); }
};
TriggerPointAllocator allocator;
}
world::PendingTriggerQueue pendingTriggerQueue() {
    return world::PendingTriggerQueue(reinterpret_cast<PendingListEntry*>(0x006B4688), 16);
}
world::CommandPointRegistry triggerCommandPoints() {
    return world::CommandPointRegistry(*reinterpret_cast<SCommandPoint**>(0x006B4708),
        reinterpret_cast<SCommandPoint**>(0x006B4214), pendingTriggerQueue(), allocator);
}
} // namespace compatibility
} // namespace spidey
