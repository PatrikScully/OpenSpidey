#include "game/cinematics/script_command_list.h"
#include "core/game_heap.h"

namespace spidey {
namespace cinematics {
namespace {
class GameScriptAllocator : public core::MemoryAllocator {
public:
    void* allocate(std::size_t byteCount) override {
        return DCMem_New(static_cast<u32>(byteCount), 0, 1, nullptr, true);
    }
    void release(void* allocation) override {
        Mem_Delete(allocation);
    }
};
} // namespace

core::MemoryAllocator& scriptCommandAllocator() {
    static GameScriptAllocator allocator;
    return allocator;
}

} // namespace cinematics
} // namespace spidey
