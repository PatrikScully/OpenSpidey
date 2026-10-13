#include "game/player/swing_collision_resolver.h"

namespace spidey {
namespace player {
namespace {
enum ContactFlag { FloorContact = 1, CeilingContact = 2 };
const i32 ceilingNormalThreshold = -2600;
const i32 positiveVerticalProbeLength = 0x80000;
const i32 negativeVerticalProbeLength = 0x20000;
}


void SwingCollisionResolver::stopAtSurface(const SwingSurfaceHit& hit, u16 flags, u16 clearance) {
    collisionFlags_ |= flags;
    position_ = hit.position + i32(clearance) * CVector(hit.normal.vx, hit.normal.vy, hit.normal.vz);
    velocity_.Zero();
}

void SwingCollisionResolver::resolve(const CVector& previousPosition, const CVector& hookBefore,
    const CVector& hookAfter, u16 surfaceClearance) {
    using namespace core;
    CVector extendedHook = hookAfter;
    extendedHook.vx = wrappingSubtract(wrappingMultiply(2, hookAfter.vx), hookBefore.vx);
    extendedHook.vz = wrappingSubtract(wrappingMultiply(2, hookAfter.vz), hookBefore.vz);

    SwingSurfaceHit hit;
    if (probe_.trace(hookBefore, extendedHook, hit)) {
        stopAtSurface(hit, hit.normal.vy >= ceilingNormalThreshold ? FloorContact : CeilingContact, surfaceClearance);
        return;
    }
    CVector end = extendedHook;
    end.vy = wrappingAdd(extendedHook.vy, positiveVerticalProbeLength);
    if (probe_.trace(extendedHook, end, hit)) {
        stopAtSurface(hit, CeilingContact, surfaceClearance);
        return;
    }
    end.vy = wrappingSubtract(extendedHook.vy, negativeVerticalProbeLength);
    if (probe_.trace(extendedHook, end, hit)) {
        stopAtSurface(hit, FloorContact, surfaceClearance);
        return;
    }
    velocity_ = position_ - previousPosition;
}

} // namespace player
} // namespace spidey
