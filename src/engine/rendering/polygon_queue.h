#pragma once

namespace spidey {
namespace rendering {

struct PolygonPacket;

// A view over the original bucket storage. Packet allocation stays with the
// frame arena; this class manages links and preserves last-submitted-first
// traversal within each depth bucket.
class PolygonQueue {
public:
    PolygonQueue(PolygonPacket** buckets, int bucketCount)
        : buckets_(buckets), bucketCount_(bucketCount) {}
    void clear();
    bool enqueue(int bucket, PolygonPacket& packet);
    PolygonPacket* first(int bucket) const;
    int bucketCount() const { return bucketCount_; }

private:
    PolygonPacket** buckets_;
    int bucketCount_;
};

} // namespace rendering
} // namespace spidey
