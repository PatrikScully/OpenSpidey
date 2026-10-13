#include "game/player/crawl_frame_trace.h"
#include "game/player/crawl_motion.h"
#include "game/player/spidey.h"
#include <cstdio>
#include <cstdlib>

extern i32 gDbgColij;

namespace spidey {
namespace player {
#ifdef SPIDEY_STANDALONE
namespace {
bool tracing(const CPlayer& player) {
    return std::getenv("SPIDEY_TRACE_CRAWL") && player.mActionState == 16;
}
}
#endif

void CrawlFrameTrace::beginFrame(const CPlayer& player) {
#ifdef SPIDEY_STANDALONE
    if (!tracing(player)) return;
    std::fprintf(stderr, "CRAWLIN pos=(%d,%d,%d) vel=(%d,%d,%d) acc=(%d,%d,%d) f80=%d anim=%d frame=%d\n",
        player.mPos.vx, player.mPos.vy, player.mPos.vz,
        player.mVel.vx, player.mVel.vy, player.mVel.vz,
        player.mAcc.vx, player.mAcc.vy, player.mAcc.vz,
        player.mElapsedTicks, player.mAnim, player.mFrame);
#endif
}

void CrawlFrameTrace::forwardProbe(const CPlayer& player, const CVector& hook, const CVector& backward) {
#ifdef SPIDEY_STANDALONE
    if (!tracing(player)) return;
    const SLineInfo& line = player.mLineInfo;
    const i32 alignment = line.pItem ? CrawlMotion::normalAlignment(line.Normal, player.crawlSurfaceNormal()) : 0;
    std::fprintf(stderr, "CRAWL hook=(%d,%d,%d) along=(%d,%d,%d) start=(%d,%d,%d) end=(%d,%d,%d) hit=%p normal=(%d,%d,%d) dot=%d dist=%d face3=%#x pos=(%d,%d,%d)\n",
        hook.vx, hook.vy, hook.vz, backward.vx, backward.vy, backward.vz,
        line.StartCoords.vx, line.StartCoords.vy, line.StartCoords.vz,
        line.EndCoords.vx, line.EndCoords.vy, line.EndCoords.vz,
        static_cast<void*>(line.pItem), line.Normal.vx, line.Normal.vy, line.Normal.vz, alignment,
        line.pItem ? line.Distance : -1, line.pItem ? line.pFace[3] : 0u,
        core::arithmeticShiftRight(player.mPos.vx, 12), core::arithmeticShiftRight(player.mPos.vy, 12),
        core::arithmeticShiftRight(player.mPos.vz, 12));
#endif
}

void CrawlFrameTrace::sideHit(const CPlayer& player, bool right) {
#ifdef SPIDEY_STANDALONE
    if (tracing(player)) std::fprintf(stderr, "CRAWL SIDEHIT %s\n", right ? "right" : "left");
#endif
}

void CrawlFrameTrace::cornerHit(const CPlayer& player) {
#ifdef SPIDEY_STANDALONE
    if (!tracing(player)) return;
    const SLineInfo& line = player.mLineInfo;
    std::fprintf(stderr, "CRAWL CORNERHIT dist=%d normal=(%d,%d,%d)\n",
        line.Distance, line.Normal.vx, line.Normal.vy, line.Normal.vz);
#endif
}

void CrawlFrameTrace::beginGroundQuery(const CPlayer& player) {
#ifdef SPIDEY_STANDALONE
    const i32 height = core::arithmeticShiftRight(player.mPos.vy, 12);
    static i32 detailedQueryCount = 0;
    gDbgColij = tracing(player) && height >= 4530 && height <= 4552 ? 1 : 0;
    if (gDbgColij && height <= 4540 && detailedQueryCount < 1) {
        gDbgColij = 2;
        ++detailedQueryCount;
    }
    if (gDbgColij) std::fprintf(stderr, "CRAWL GROUNDQUERY py=%d\n", height);
#endif
}

void CrawlFrameTrace::endGroundQuery(const CPlayer& player, const SLineInfo& ground) {
#ifdef SPIDEY_STANDALONE
    gDbgColij = 0;
    if (!tracing(player)) return;
    std::fprintf(stderr, "CRAWL GROUND start=(%d,%d,%d) end=(%d,%d,%d) hit=%p normal=(%d,%d,%d) dist=%d face3=%#x\n",
        ground.StartCoords.vx, ground.StartCoords.vy, ground.StartCoords.vz,
        ground.EndCoords.vx, ground.EndCoords.vy, ground.EndCoords.vz,
        static_cast<void*>(ground.pItem), ground.pItem ? ground.Normal.vx : 0,
        ground.pItem ? ground.Normal.vy : 0, ground.pItem ? ground.Normal.vz : 0,
        ground.pItem ? ground.Distance : -1, ground.pItem ? ground.pFace[3] : 0u);
#endif
}

} // namespace player
} // namespace spidey
