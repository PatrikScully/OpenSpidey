#pragma once

#include "core/types.h"

namespace spidey {
namespace rendering {

// Transformed screen-space vertex, matching the original 28-byte TL vertex
// record consumed by the renderer. Depth is normalized; reciprocalW enables
// perspective-correct texture interpolation after CPU projection.
struct ScreenVertex {
    f32 x;
    f32 y;
    f32 depth;
    f32 reciprocalW;
    u32 colorArgb;
    f32 u;
    f32 v;
};

} // namespace rendering
} // namespace spidey
