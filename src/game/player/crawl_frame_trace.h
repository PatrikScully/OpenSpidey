#pragma once
class CPlayer;
class CVector;
struct SLineInfo;

namespace spidey {
namespace player {
class CrawlFrameTrace {
public:
    static void beginFrame(const CPlayer& player);
    static void forwardProbe(const CPlayer& player, const CVector& hook, const CVector& backward);
    static void sideHit(const CPlayer& player, bool right);
    static void cornerHit(const CPlayer& player);
    static void beginGroundQuery(const CPlayer& player);
    static void endGroundQuery(const CPlayer& player, const SLineInfo& ground);
};
}
}
