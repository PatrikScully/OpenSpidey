#include "game/application/baddy_frame_trace.h"
#ifdef SPIDEY_STANDALONE
#include "game/actors/baddy.h"
#include "game/actors/blackcat.h"
#include "platform/plat.h"
#include <cstdlib>
#include <cstdio>
#endif

namespace spidey {
namespace application {

void BaddyFrameTrace::emit() {
#ifdef SPIDEY_STANDALONE
    static const bool enabled = std::getenv("SPIDEY_TRACE_BADDY") != 0;
    if (!enabled)
        return;

    static unsigned frameCount = 0;
    if (++frameCount < 30)
        return;
    frameCount = 0;

    for (CBaddy* enemy = G_BADDY_LIST; enemy; enemy = static_cast<CBaddy*>(enemy->mNextItem))
        std::fprintf(stderr, "BADDY t=%u type=%d pos=(%d,%d,%d) anim=%d frame=%d flags=%#x model=%p\n",
                Plat_Ticks(), (i32)enemy->mType, enemy->mPos.vx, enemy->mPos.vy, enemy->mPos.vz,
                (i32)enemy->mAnim, (i32)enemy->mFrame, (u32)enemy->mFlags, (void*)enemy->mModel);
    for (CBaddy* enemy = G_BADDY_LIST; enemy; enemy = static_cast<CBaddy*>(enemy->mNextItem))
    {
        if (enemy->mType != 319)
            continue;
        CBlackCat* cat = static_cast<CBlackCat*>(enemy);
        i32* scriptBlock = cat->mScriptCommandHead;
        std::fprintf(stderr, "CAT state=%d sub=%d f340=%d f348=%d block=%d tgt=(%d,%d,%d) vel=(%d,%d,%d) stream=%p\n",
                (i32)cat->field_31C.bothFlags, (i32)cat->dumbAssPad, (i32)cat->mScriptRunning, (i32)cat->mScriptBytecodeActive,
                scriptBlock ? scriptBlock[0] : -1, scriptBlock ? scriptBlock[2] : 0, scriptBlock ? scriptBlock[3] : 0, scriptBlock ? scriptBlock[4] : 0,
                cat->mVel.vx, cat->mVel.vy, cat->mVel.vz, (void*)cat->mScriptCursorAddress);
    }
#endif

}

} // namespace application
} // namespace spidey
