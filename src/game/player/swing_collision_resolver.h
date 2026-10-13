#pragma once
#include "core/fixed_vector.h"

namespace spidey {
namespace player {

struct SwingSurfaceHit {
    CVector position;
    CSVector normal;
};

// Query ownership belongs to the world. Implementations may reuse the player's
// collision record; the resolver consumes only the copied hit position/normal.
class SwingCollisionProbe {
public:
    virtual ~SwingCollisionProbe() {}
    virtual bool trace(const CVector& start, const CVector& end, SwingSurfaceHit& hit) = 0;
};

class SwingCollisionResolver {
public:
    SwingCollisionResolver(CVector& position, CVector& velocity, u16& collisionFlags,
        SwingCollisionProbe& probe)
        : position_(position), velocity_(velocity), collisionFlags_(collisionFlags), probe_(probe) {}
    void resolve(const CVector& previousPosition, const CVector& hookBefore,
        const CVector& hookAfter, u16 surfaceClearance);

private:
    void stopAtSurface(const SwingSurfaceHit& hit, u16 flags, u16 clearance);
    CVector& position_;
    CVector& velocity_;
    u16& collisionFlags_;
    SwingCollisionProbe& probe_;
};

} // namespace player
} // namespace spidey
