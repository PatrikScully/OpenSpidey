#include "engine/rendering/render_buffer_pool.h"
#include <cassert>
#include <cstdlib>
#include <set>
#include <vector>

class TrackingAllocator : public spidey::core::MemoryAllocator {
public:
    explicit TrackingAllocator(int failAt = -1) : failAt_(failAt), calls_(0) {}
    ~TrackingAllocator() { assert(live.empty()); }
    void* allocate(std::size_t bytes) override {
        sizes.push_back(bytes);
        if (calls_++ == failAt_) return nullptr;
        void* allocation = std::malloc(bytes);
        assert(allocation);
        live.insert(allocation);
        return allocation;
    }
    void release(void* allocation) override {
        assert(live.erase(allocation) == 1);
        std::free(allocation);
    }
    std::set<void*> live;
    std::vector<std::size_t> sizes;
private:
    int failAt_;
    int calls_;
};

int main() {
    using spidey::rendering::RenderBufferPool;
    SDoubleBuffer buffers[2] = {};
    TrackingAllocator allocator;
    RenderBufferPool pool(buffers, allocator);
    assert(pool.allocate());
    assert(allocator.live.size() == 4);
    assert(allocator.sizes[0] == 0x4000 && allocator.sizes[1] == 0x4000);
    assert(allocator.sizes[2] == 0x17000 && allocator.sizes[3] == 0x17000);
    assert(!pool.allocate()); // A second request cannot overwrite live storage.
    pool.release();
    pool.release(); // The legacy sentinel must never be passed to the allocator.
    assert(allocator.live.empty());
    assert(pool.allocate());
    pool.release();

    for (int failAt = 0; failAt < 4; ++failAt) {
        SDoubleBuffer failedBuffers[2] = {};
        TrackingAllocator failingAllocator(failAt);
        RenderBufferPool failedPool(failedBuffers, failingAllocator);
        assert(!failedPool.allocate());
        assert(failingAllocator.live.empty());
        failedPool.release();
    }
}
