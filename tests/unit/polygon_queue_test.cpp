#include "engine/rendering/polygon_queue.h"
#include "engine/rendering/polygon_pipeline.h"
#include <cassert>

int main() {
    using spidey::rendering::PolygonPacket;
    using spidey::rendering::PolygonQueue;
    PolygonPacket* buckets[4] = {};
    PolygonQueue queue(buckets, 4);
    PolygonPacket first = {}, second = {}, otherDepth = {};
    assert(queue.bucketCount() == 4);
    assert(queue.enqueue(2, first));
    assert(queue.enqueue(2, second));
    assert(queue.enqueue(3, otherDepth));
    assert(queue.first(2) == &second && second.next == &first && first.next == nullptr);
    assert(queue.first(3) == &otherDepth && otherDepth.next == nullptr);
    assert(queue.first(1) == nullptr);
    assert(!queue.enqueue(-1, first) && !queue.enqueue(4, first));
    assert(queue.first(-1) == nullptr && queue.first(4) == nullptr);
    assert(queue.first(2) == &second); // Invalid requests do not alter the queue.
    queue.clear();
    for (int index = 0; index < queue.bucketCount(); ++index) assert(queue.first(index) == nullptr);
    assert(queue.enqueue(0, first) && first.next == nullptr);
}
