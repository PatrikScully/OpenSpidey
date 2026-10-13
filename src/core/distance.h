#pragma once

#include "core/fixed_vector.h"
#include <utility>

namespace spidey {
namespace math {

enum { PositionFractionBits = 12 };

// Matches the game's inexpensive distance metric: largest axis plus one
// half of the middle and one quarter of the smallest, in whole world units.
inline u32 approximateDistance(const CVector& first, const CVector& second) {
    i32 largest = first.vx - second.vx;
    i32 middle = first.vy - second.vy;
    i32 smallest = first.vz - second.vz;
    if (largest < 0) largest = -largest;
    if (middle < 0) middle = -middle;
    if (smallest < 0) smallest = -smallest;
    if (largest < middle) std::swap(largest, middle);
    if (middle < smallest) std::swap(middle, smallest);
    if (largest < middle) std::swap(largest, middle);
    return (largest + (middle >> 1) + (smallest >> 2)) >> PositionFractionBits;
}

inline u32 approximateHorizontalDistance(const CVector& first, const CVector& second) {
    CVector horizontalPosition = first;
    horizontalPosition.vy = second.vy;
    return approximateDistance(horizontalPosition, second);
}

} // namespace math
} // namespace spidey
