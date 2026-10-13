#pragma once

#include "core/memory_allocator.h"
#include "core/types.h"
#include <cstdint>

namespace spidey {
namespace cinematics {

// Borrowed view of an original variable-length command record. Parameters
// occupy words 2..count-2; the final word is a 32-bit encoded next pointer.
class ScriptCommandView {
public:
    explicit ScriptCommandView(i32* words) : words_(words) {}
    i32 type() const { return words_[0]; }
    void setType(i32 type) { words_[0] = type; }
    i32 wordCount() const { return words_[1]; }
    i32& parameter(unsigned index) { return words_[index + 2]; }
    i32* next() const {
        return reinterpret_cast<i32*>(static_cast<std::uintptr_t>(
            static_cast<u32>(words_[wordCount() - 1])));
    }
    void setNext(i32* command) {
        words_[wordCount() - 1] = static_cast<i32>(
            reinterpret_cast<std::uintptr_t>(command));
    }

private:
    i32* words_;
};

// Manages records owned by an existing player/actor head. Allocation policy is
// injected. The wrapper does not clear on destruction: actor lifetime owns that
// decision, and short-lived compatibility adapters may share the same head.
class ScriptCommandList {
public:
    ScriptCommandList(i32*& head, core::MemoryAllocator& allocator)
        : head_(head), allocator_(allocator) {}
    i32* create(i32 type, u32 parameterCount);
    // Original GetNewCommandBlock adapters supply a total word count.
    i32* allocate(u32 wordCount);
    i32* remove(i32* command);
    void removeByType(i32 type);
    void clear();

private:
    ScriptCommandList(const ScriptCommandList&);
    ScriptCommandList& operator=(const ScriptCommandList&);
    i32*& head_;
    core::MemoryAllocator& allocator_;
};

// Bridge to the original game heap, kept out of the record/list implementation.
core::MemoryAllocator& scriptCommandAllocator();

} // namespace cinematics
} // namespace spidey
