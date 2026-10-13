#include "engine/rendering/render_buffer_pool.h"
#include <cstdint>

namespace spidey {
namespace rendering {

bool RenderBufferPool::isAllocation(const void* pointer) {
    return pointer && pointer != reinterpret_cast<const void*>(static_cast<std::uintptr_t>(-1));
}

bool RenderBufferPool::allocate() {
    // Preserve the original heap allocation order: tables first, then polygons.
    for (int index = 0; index < BufferCount; ++index) {
        if (isAllocation(buffers_[index].OrderingTable) || isAllocation(buffers_[index].Polys)) {
            return false;
        }
    }
    for (int index = 0; index < BufferCount; ++index) {
        buffers_[index].OrderingTable = static_cast<u32*>(allocator_.allocate(OrderingTableBytes));
        if (!buffers_[index].OrderingTable) {
            release();
            return false;
        }
    }
    for (int index = 0; index < BufferCount; ++index) {
        buffers_[index].Polys = static_cast<u8*>(allocator_.allocate(PolygonBufferBytes));
        if (!buffers_[index].Polys) {
            release();
            return false;
        }
    }
    return true;
}

void RenderBufferPool::release() {
    for (int index = 0; index < BufferCount; ++index) {
        if (isAllocation(buffers_[index].OrderingTable)) {
            allocator_.release(buffers_[index].OrderingTable);
        }
        buffers_[index].OrderingTable = reinterpret_cast<u32*>(static_cast<std::uintptr_t>(-1));
    }
    for (int index = 0; index < BufferCount; ++index) {
        if (isAllocation(buffers_[index].Polys)) {
            allocator_.release(buffers_[index].Polys);
        }
        buffers_[index].Polys = reinterpret_cast<u8*>(static_cast<std::uintptr_t>(-1));
    }
}

} // namespace rendering
} // namespace spidey
