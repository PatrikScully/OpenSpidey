#pragma once

#include "core/memory_allocator.h"
#include "engine/rendering/render_buffers.h"

namespace spidey {
namespace rendering {

// Owns the ordering tables and polygon storage of a pair of frame buffers.
// Metadata stays in the original layout; allocation policy is injected.
class RenderBufferPool {
public:
    enum { BufferCount = 2, OrderingTableBytes = 0x4000, PolygonBufferBytes = 0x17000 };

    RenderBufferPool(SDoubleBuffer* buffers, core::MemoryAllocator& allocator)
        : buffers_(buffers), allocator_(allocator) {}

    bool allocate();
    void release();

private:
    RenderBufferPool(const RenderBufferPool&);
    RenderBufferPool& operator=(const RenderBufferPool&);

    static bool isAllocation(const void* pointer);
    SDoubleBuffer* buffers_;
    core::MemoryAllocator& allocator_;
};

} // namespace rendering
} // namespace spidey
