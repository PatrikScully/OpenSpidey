#include "engine/world/command_point_registry.h"

namespace spidey {
namespace world {
SCommandPoint* CommandPointRegistry::create(u32 checksum, u16 node, u16* commands) {
    SCommandPoint* point = static_cast<SCommandPoint*>(allocator_.allocate(sizeof(SCommandPoint)));
    if (!point) return nullptr;
    point->pCommands = commands;
    point->Collision = 0;
    point->Executed = 0;
    point->NumPulsesSet = 0;
    point->PulsesReceived = 0;
    point->NumPulses = 0;
    point->NodeIndex = node;
    point->Checksum = checksum;
    point->pNext = head_;
    point->pNextSimilar = buckets_[checksum & 0xFF];
    head_ = point;
    buckets_[checksum & 0xFF] = point;
    return point;
}

SCommandPoint* CommandPointRegistry::findByNode(i32 node) const {
    for (SCommandPoint* point = head_; point; point = point->pNext)
        if (point->NodeIndex == node) return point;
    return nullptr;
}

CommandPointTriggerResult CommandPointRegistry::trigger(u32 checksum) {
    for (SCommandPoint* point = buckets_[checksum & 0xFF]; point; point = point->pNextSimilar) {
        if (point->Checksum != checksum) continue;
        point->Collision = 1;
        if (point->Executed) continue;
        if (!queue_.append(point->NodeIndex, point->pCommands)) return {nullptr, true};
        point->Executed = 1;
        return {point, false};
    }
    return {nullptr, false};
}

bool CommandPointRegistry::receivePulse(SCommandPoint& point, u16 node) {
    ++point.PulsesReceived; // Original byte counter wraps at 256.
    return queue_.append(node, point.pCommands);
}

void CommandPointRegistry::resetCollisionFlags() {
    for (SCommandPoint* point = head_; point; point = point->pNext) point->Collision = 0;
}
void CommandPointRegistry::rearmUncollidedPoints() {
    for (SCommandPoint* point = head_; point; point = point->pNext)
        if (point->Executed && !point->Collision) point->Executed = 0;
}
void CommandPointRegistry::clear() {
    for (unsigned bucket = 0; bucket < HashBucketCount; ++bucket) buckets_[bucket] = nullptr;
    for (SCommandPoint* point = head_; point;) {
        SCommandPoint* next = point->pNext;
        allocator_.release(point);
        point = next;
    }
    head_ = nullptr;
    queue_.clear();
}
} // namespace world
} // namespace spidey
