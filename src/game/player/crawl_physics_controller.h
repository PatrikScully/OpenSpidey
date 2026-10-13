#pragma once
#include "core/fixed_vector.h"
class CPlayer;
struct SLineInfo;

namespace spidey {
namespace player {

class CrawlPhysicsController {
public:
    explicit CrawlPhysicsController(CPlayer& player) : player_(player) {}
    void update();

private:
    void probeForwardAndCorners(const CVector& hook);
    void probeSideCorners(const CVector& hook);
    void recoverMissingSurface(SLineInfo& ground);
    void respondToSurface(SLineInfo& ground, bool moved);
    void updateSurfaceControls(const SLineInfo& ground);
    void notifySupportBody(const SLineInfo& ground);
    void trace(SLineInfo& line, const CVector& start, const CVector& end);
    void traceGround(SLineInfo& line);
    CPlayer& player_;
    CVector previousPosition_;
};

} // namespace player
} // namespace spidey
