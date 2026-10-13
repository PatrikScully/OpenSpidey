#include "game/cinematics/script_command_list.h"
#include <limits>

namespace spidey {
namespace cinematics {

i32* ScriptCommandList::create(i32 type, u32 parameterCount) {
    if (parameterCount > std::numeric_limits<u32>::max() / sizeof(i32) - 3)
        return nullptr;
    i32* command = allocate(parameterCount + 3);
    if (command)
        ScriptCommandView(command).setType(type);
    return command;
}

i32* ScriptCommandList::allocate(u32 wordCount) {
    if (wordCount < 3 || wordCount > std::numeric_limits<u32>::max() / sizeof(i32))
        return nullptr;
    i32* command = static_cast<i32*>(allocator_.allocate(sizeof(i32) * wordCount));
    if (!command)
        return nullptr;

    // The legacy callers fill the type/parameters immediately after allocation.
    // Initialize the length before linking so every linked record is traversable.
    command[1] = static_cast<i32>(wordCount);
    ScriptCommandView(command).setNext(nullptr);
    if (!head_) {
        head_ = command;
    } else {
        i32* tail = head_;
        while (ScriptCommandView(tail).next())
            tail = ScriptCommandView(tail).next();
        ScriptCommandView(tail).setNext(command);
    }
    return command;
}

i32* ScriptCommandList::remove(i32* command) {
    if (!command)
        return nullptr;
    i32* next = ScriptCommandView(command).next();
    if (head_ == command) {
        head_ = next;
    } else {
        i32* previous = head_;
        while (previous && ScriptCommandView(previous).next() != command)
            previous = ScriptCommandView(previous).next();
        // A record owned by a different list must not be freed here.
        if (!previous)
            return nullptr;
        ScriptCommandView(previous).setNext(next);
    }
    allocator_.release(command);
    return next;
}

void ScriptCommandList::removeByType(i32 type) {
    i32* command = head_;
    while (command) {
        if (ScriptCommandView(command).type() == type)
            command = remove(command);
        else
            command = ScriptCommandView(command).next();
    }
}

void ScriptCommandList::clear() {
    while (head_)
        remove(head_);
}

} // namespace cinematics
} // namespace spidey
