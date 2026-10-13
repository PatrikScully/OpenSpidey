#include "game/cinematics/script_command_list.h"
#include <cassert>
#include <cstdlib>
#include <cstring>
#include <limits>
#include <set>

class TrackingAllocator : public spidey::core::MemoryAllocator {
public:
    bool failNext = false;
    unsigned allocationCalls = 0;
    std::set<void*> live;
    ~TrackingAllocator() { assert(live.empty()); }
    void* allocate(std::size_t bytes) override {
        ++allocationCalls;
        if (failNext) {
            failNext = false;
            return nullptr;
        }
        void* block = std::malloc(bytes);
        assert(block);
        // Newly allocated memory has no valid header until the list sets it.
        std::memset(block, 0xA5, bytes);
        live.insert(block);
        return block;
    }
    void release(void* block) override {
        assert(live.erase(block) == 1);
        std::free(block);
    }
};

int main() {
    using spidey::cinematics::ScriptCommandList;
    using spidey::cinematics::ScriptCommandView;
    // The asset format encodes links in 32-bit words, matching the game ABI.
    static_assert(sizeof(void*) == sizeof(i32), "Run this fixture for the 32-bit game ABI.");
    TrackingAllocator allocator;
    i32* head = nullptr;
    ScriptCommandList commands(head, allocator);
    commands.clear();
    assert(!commands.remove(nullptr));
    assert(!commands.allocate(0));
    assert(!commands.allocate(2));
    assert(!commands.allocate(std::numeric_limits<u32>::max()));
    assert(!commands.create(7, std::numeric_limits<u32>::max()));
    assert(allocator.allocationCalls == 0);

    i32* first = commands.allocate(5);
    first[0] = 2;
    ScriptCommandView(first).parameter(0) = 123;
    ScriptCommandView(first).parameter(1) = 456;
    i32* middle = commands.allocate(3);
    middle[0] = 3;
    i32* last = commands.allocate(7);
    last[0] = 2;
    assert(head == first);
    assert(ScriptCommandView(first).wordCount() == 5);
    assert(ScriptCommandView(first).next() == middle);
    assert(ScriptCommandView(middle).next() == last);
    assert(!ScriptCommandView(last).next());

    allocator.failNext = true;
    assert(!commands.allocate(4));
    assert(allocator.live.size() == 3);
    assert(!ScriptCommandView(last).next());
    assert(commands.remove(middle) == last);
    assert(ScriptCommandView(first).next() == last);
    assert(ScriptCommandView(first).parameter(0) == 123);
    assert(ScriptCommandView(first).parameter(1) == 456);

    i32* otherHead = nullptr;
    ScriptCommandList other(otherHead, allocator);
    i32* outsider = other.allocate(3);
    outsider[0] = 9;
    assert(!commands.remove(outsider));
    assert(otherHead == outsider && allocator.live.count(outsider));
    assert(commands.remove(last) == nullptr);
    assert(!ScriptCommandView(first).next());
    assert(commands.remove(first) == nullptr);
    assert(!head);
    other.clear();
    assert(allocator.live.empty());

    // Remove a head and consecutive matches without reading a freed link.
    const int types[] = {2, 2, 3, 2, 3, 2};
    i32* survivors[2] = {};
    int survivorIndex = 0;
    for (int type : types) {
        i32* block = commands.allocate(3);
        block[0] = type;
        if (type == 3)
            survivors[survivorIndex++] = block;
    }
    commands.removeByType(99);
    assert(allocator.live.size() == 6);
    commands.removeByType(2);
    assert(allocator.live.size() == 2);
    assert(head == survivors[0]);
    assert(ScriptCommandView(head).next() == survivors[1]);
    assert(!ScriptCommandView(survivors[1]).next());
    commands.removeByType(3);
    assert(!head && allocator.live.empty());
    commands.clear();

    // Temporary adapters must leave actor-owned records alive.
    {
        ScriptCommandList temporary(head, allocator);
        i32* typed = temporary.create(15, 2);
        assert(ScriptCommandView(typed).type() == 15);
        assert(ScriptCommandView(typed).wordCount() == 5);
        ScriptCommandView(typed).parameter(1) = 42;
        assert(typed[3] == 42);
    }
    assert(head && allocator.live.size() == 1);
    commands.clear();
}
