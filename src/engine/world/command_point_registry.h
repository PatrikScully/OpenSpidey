#pragma once
#include "engine/world/pending_trigger_queue.h"
#include "core/memory_allocator.h"

namespace spidey {
namespace world {
struct CommandPointTriggerResult {
    SCommandPoint* point;
    bool queueOverflow;
};

// Borrow list/hash storage and queue slots; allocate records through the supplied
// heap. Explicit clear releases records. A temporary view does not clear them.
class CommandPointRegistry {
public:
    static const unsigned HashBucketCount = 256;
    CommandPointRegistry(SCommandPoint*& head, SCommandPoint** buckets,
        PendingTriggerQueue queue, core::MemoryAllocator& allocator)
        : head_(head), buckets_(buckets), queue_(queue), allocator_(allocator) {}
    SCommandPoint* create(u32 checksum, u16 node, u16* commands);
    SCommandPoint* findByNode(i32 node) const;
    CommandPointTriggerResult trigger(u32 checksum);
    bool receivePulse(SCommandPoint& point, u16 node);
    void resetCollisionFlags();
    void rearmUncollidedPoints();
    void clear();
private:
    SCommandPoint*& head_;
    SCommandPoint** buckets_;
    PendingTriggerQueue queue_;
    core::MemoryAllocator& allocator_;
};
} // namespace world
} // namespace spidey
