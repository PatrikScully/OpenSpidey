#pragma once
#include "core/wrapping_math.h"
#include <cstdint>

namespace spidey {
namespace core {

// Original MSVC _ftol truncates through an x87 signed 64-bit result and callers
// keep its low 32 bits. Invalid/out-of-range conversion produces the x87
// indefinite integer (INT64_MIN), whose low word is zero.
inline i32 truncateToGameWord(float value) {
    const float limit = 9223372036854775808.0f;
    if (!(value >= -limit && value < limit)) return 0;
    return signedWord(static_cast<u32>(static_cast<std::int64_t>(value)));
}

} // namespace core
} // namespace spidey
