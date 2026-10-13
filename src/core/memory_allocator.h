#pragma once

#include <cstddef>

namespace spidey {
namespace core {

// Resource owners depend on this narrow allocation contract. The original
// game heap and test allocators can supply it without changing asset layouts.
class MemoryAllocator {
public:
    virtual ~MemoryAllocator() {}
    virtual void* allocate(std::size_t byteCount) = 0;
    virtual void release(void* allocation) = 0;
};

} // namespace core
} // namespace spidey
