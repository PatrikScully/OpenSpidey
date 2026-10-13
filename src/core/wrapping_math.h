#pragma once
#include "core/types.h"
#include <cstring>

namespace spidey {
namespace core {

static_assert(sizeof(i32) == 4 && sizeof(u32) == 4 && sizeof(i16) == 2 && sizeof(u16) == 2,
    "Game word arithmetic requires 32-bit words and 16-bit half words.");

// Original integer physics uses x86 low-word arithmetic. Keep that behavior
// explicitly rather than depending on signed overflow or narrowing casts.
inline i32 signedWord(u32 bits) {
    i32 value;
    std::memcpy(&value, &bits, sizeof(value));
    return value;
}
inline i16 signedHalfWord(u32 bits) {
    u16 low = static_cast<u16>(bits);
    i16 value;
    std::memcpy(&value, &low, sizeof(value));
    return value;
}
inline i32 wrappingAdd(i32 left, i32 right) {
    return signedWord(static_cast<u32>(left) + static_cast<u32>(right));
}
inline i32 wrappingSubtract(i32 left, i32 right) {
    return signedWord(static_cast<u32>(left) - static_cast<u32>(right));
}
inline i32 wrappingMultiply(i32 left, i32 right) {
    return signedWord(static_cast<u32>(left) * static_cast<u32>(right));
}
inline i32 wrappingShiftLeft(i32 value, unsigned count) {
    return signedWord(static_cast<u32>(value) << (count & 31));
}
inline i32 arithmeticShiftRight(i32 value, unsigned count) {
    count &= 31; // Original variable x86 shift-count mask.
    if (count == 0) return value;
    u32 result = static_cast<u32>(value) >> count;
    if (value < 0) result |= ~u32(0) << (32 - count);
    return signedWord(result);
}

} // namespace core
} // namespace spidey
