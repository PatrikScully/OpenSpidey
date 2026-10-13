#pragma once
#include "core/types.h"

// Per-axis shift counts: damping subtracts component >> count.
class CFriction {
public:
    u8 vx, vy, vz;
    EXPORT CFriction() : vx(0), vy(0), vz(0) {}
    EXPORT CFriction(u8 x, u8 y, u8 z) : vx(x), vy(y), vz(z) {}
    EXPORT void Set(u8 x, u8 y, u8 z) { vx = x; vy = y; vz = z; }
};
