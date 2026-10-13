#include "engine/rendering/polygon_queue.h"
#include "engine/rendering/polygon_pipeline.h"

namespace spidey {
namespace rendering {

void PolygonQueue::clear() {
    for (int bucket = 0; bucket < bucketCount_; ++bucket) buckets_[bucket] = nullptr;
}

bool PolygonQueue::enqueue(int bucket, PolygonPacket& packet) {
    if (bucket < 0 || bucket >= bucketCount_) return false;
    packet.next = buckets_[bucket];
    buckets_[bucket] = &packet;
    return true;
}

PolygonPacket* PolygonQueue::first(int bucket) const {
    if (bucket < 0 || bucket >= bucketCount_) return nullptr;
    return buckets_[bucket];
}

} // namespace rendering
} // namespace spidey
