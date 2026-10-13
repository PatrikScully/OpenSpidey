#include "engine/world/command_point_registry.h"
#include <cassert>
#include <cstdlib>
#include <cstring>
#include <vector>
#include <algorithm>
using namespace spidey::world;

class Allocator : public spidey::core::MemoryAllocator {
public:
    std::vector<void*> live;
    bool fail = false;
    void* allocate(std::size_t size) override {
        assert(size == sizeof(SCommandPoint));
        if (fail) return nullptr;
        void* result = std::malloc(size);
        std::memset(result, 0xCD, size);
        live.push_back(result);
        return result;
    }
    void release(void* allocation) override {
        auto found = std::find(live.begin(), live.end(), allocation);
        assert(found != live.end());
        live.erase(found);
        std::free(allocation);
    }
};
class Executor : public TriggerCommandExecutor {
public:
    PendingTriggerQueue& queue;
    u16* commands;
    std::vector<u16> order;
    Executor(PendingTriggerQueue& queue, u16* commands) : queue(queue), commands(commands) {}
    void execute(u16* stream, u16 node) override {
        assert(stream == commands);
        order.push_back(node);
        if (node == 1) assert(queue.append(3, commands));
    }
};
int main() {
    u16 commands[] = {0xFFFF};
    struct Storage { PendingListEntry entries[3]; u32 guard; } storage = {};
    storage.guard = 0xCAFEBABE;
    for (auto& entry : storage.entries) entry.field_2 = 0x1234;
    PendingTriggerQueue queue(storage.entries, 3);
    assert(queue.append(1, commands) && queue.append(2, commands));
    Executor executor(queue, commands);
    queue.execute(executor);
    assert((executor.order == std::vector<u16>{1, 2, 3}));
    for (const auto& entry : storage.entries)
        assert(entry.NodeIndex == 0 && !entry.pCommands && entry.field_2 == 0x1234);
    assert(queue.append(10, commands) && queue.append(11, commands) && queue.append(12, commands));
    assert(!queue.append(13, commands) && storage.guard == 0xCAFEBABE);
    queue.clear();
    PendingTriggerQueue empty(nullptr, 0);
    assert(!empty.append(1, commands));
    empty.execute(executor);

    SCommandPoint* head = nullptr;
    SCommandPoint* buckets[256] = {};
    Allocator allocator;
    CommandPointRegistry registry(head, buckets, queue, allocator);
    auto first = registry.create(0xAB12, 10, commands);
    auto collision = registry.create(0xCD12, 11, commands);
    auto duplicate = registry.create(0xAB12, 12, commands);
    assert(head == duplicate && duplicate->pNext == collision && collision->pNext == first);
    assert(buckets[0x12] == duplicate && duplicate->pNextSimilar == collision);
    assert(registry.findByNode(10) == first && !registry.findByNode(-1));
    assert(!first->Collision && !first->Executed && !first->NumPulsesSet && !first->NumPulses);
    auto result = registry.trigger(0xAB12);
    assert(result.point == duplicate && !result.queueOverflow && duplicate->Executed);
    result = registry.trigger(0xAB12);
    assert(result.point == first && duplicate->Collision && first->Collision);
    result = registry.trigger(0xAB12);
    assert(!result.point && !result.queueOverflow && !collision->Collision);
    registry.rearmUncollidedPoints();
    assert(first->Executed && duplicate->Executed);
    registry.resetCollisionFlags();
    registry.rearmUncollidedPoints();
    assert(!first->Executed && !duplicate->Executed);
    assert(queue.append(99, commands)); // Fill the last available slot.
    result = registry.trigger(0xCD12);
    assert(result.queueOverflow && !result.point && !collision->Executed && collision->Collision);
    assert(storage.guard == 0xCAFEBABE);
    queue.clear();
    result = registry.trigger(0xCD12);
    assert(result.point == collision && collision->Executed);
    collision->PulsesReceived = 255;
    assert(registry.receivePulse(*collision, 11) && collision->PulsesReceived == 0);
    assert(registry.receivePulse(*collision, 11) && collision->PulsesReceived == 1);
    assert(!registry.receivePulse(*collision, 11) && collision->PulsesReceived == 2);
    allocator.fail = true;
    assert(!registry.create(0x1000, 20, commands) && head == duplicate && !buckets[0]);
    registry.clear();
    assert(!head && allocator.live.empty());
    for (auto bucket : buckets) assert(!bucket);
    for (auto entry : storage.entries) assert(!entry.pCommands && !entry.NodeIndex);
    registry.clear(); // Explicit cleanup can be repeated safely.
}
