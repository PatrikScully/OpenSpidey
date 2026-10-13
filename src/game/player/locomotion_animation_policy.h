#pragma once
#include "core/types.h"

namespace spidey {
namespace player {

struct FallAnimationPlan {
    bool resetReference;
    u16 nextAnimation;
    bool clearWebRelease;
};

class LocomotionAnimationPolicy {
public:
    static const u16 KeepAnimation = 0xFFFF;

    static FallAnimationPlan falling(u16 animation, i32 previousVelocity,
        i32 velocity, bool pendingWebRelease)
    {
        const bool directionChanged =
            ((static_cast<u32>(previousVelocity) ^ static_cast<u32>(velocity)) & 0x80000000u) != 0;
        if (!directionChanged && maintainsFallAnimation(animation) &&
            !(pendingWebRelease && velocity > 0)) {
            return {false, KeepAnimation, false};
        }
        // Flailing preserves its animation and pending release, but still resets
        // the fall reference. Reference updates do not imply animation playback.
        if (animation == 0xAF || animation == 0xB0) {
            return {true, KeepAnimation, false};
        }
        if (pendingWebRelease) {
            return {true, static_cast<u16>(animation == 0x118 ? 0x116 : 0x114), true};
        }
        switch (animation) {
        case 0xE7: return {true, 0xE8, false};
        case 0xE0: return {true, 0xE1, false};
        case 0xDA: return {true, 0xDB, false};
        case 0xDD: return {true, 0xDE, false};
        case 0xD7: return {true, 0xD8, false};
        case 0xE8: case 0xD8: return {true, KeepAnimation, false};
        default: return {true, 0xD4, false};
        }
    }

    static u16 reverseMovement(u16 animation)
    {
        switch (animation) {
        case 0x15: return 0x19;
        case 0x34: return 0x5B;
        case 0x32: case 0x33: return 0x59;
        default: return KeepAnimation;
        }
    }

private:
    static bool maintainsFallAnimation(u16 animation)
    {
        switch (animation) {
        case 0xD3: case 0xD4: case 0xE0: case 0xE1: case 0xE7:
        case 0xE8: case 0xE2: case 0xE4: case 0xE9: case 0xEB:
        case 0xAF: case 0xB0: case 0x7F: case 0xEE: case 0xF1:
        case 0xF0: case 0xEF: case 0xD7: case 0xD8: case 0xDD:
        case 0xDA: case 0xDE: case 0xDB: case 0x116: case 0x114:
            return true;
        default: return false;
        }
    }
};

} // namespace player
} // namespace spidey
