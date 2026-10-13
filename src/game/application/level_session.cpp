#include "game/application/level_session.h"
#include "game/application/frame_pacer.h"
#include "game/application/level_exit_reason.h"
#include "game/application/level_session_state.h"
#include "game/application/main.h"
#include "game/ui/FontTools.h"
#include "game/ui/front.h"
#include "game/ui/mess.h"
#include "game/ui/screen.h"
#include "game/ui/PCShell.h"
#include "game/player/spidey.h"
#include "game/objects/powerup.h"
#include "game/actors/baddy.h"
#include "engine/rendering/PCGfx.h"
#include "engine/rendering/render_buffers.h"
#include "engine/rendering/m3dinit.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/input/ps2pad.h"
#include "engine/audio/music.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/audio/ps2redbook.h"
#include "engine/assets/spool.h"
#include "engine/world/trig.h"
#include "compatibility/my_assert.h"
#include "core/utils.h"
#include <cstdlib>

EXPORT extern i32 gWaterEffect;

namespace {

// Shell/level data remain at their original executable addresses. The two
// reset-only counters have no verified reader, so retain their legacy names.
i32* const gDoShellForceLevelExit = reinterpret_cast<i32*>(0x0068293C);
i32* const gShellMenuAbort = reinterpret_cast<i32*>(0x0054D38C);
i32* const gWaterEffectTwo = reinterpret_cast<i32*>(0x0060FAA0);
i32* const gPlayAwayCounter = reinterpret_cast<i32*>(0x005FCD18);
i32* const gShowFrameRate = reinterpret_cast<i32*>(0x0060CFFC);
i16* const gOtPushback = reinterpret_cast<i16*>(0x00660F78);

class VblankClock : public spidey::core::TickClock {
public:
    u32 currentTick() const override { return G_VBLANKS; }
    void waitForTick() override { Pause(1); }
};

bool allow60FramesPerSecond() {
#ifdef SPIDEY_STANDALONE
    // Preserve the native game's opt-in setting and read it only once.
    static const bool enabled = std::getenv("SPIDEY_60FPS") != nullptr;
    return enabled;
#else
    return true;
#endif
}

} // namespace

namespace spidey {
namespace application {

void LevelSession::run() {
    if (!begin()) return;
    while (gLevelStatus == 0 && advanceFrame()) {}
    finish();
}

bool LevelSession::begin() {
    FontManager::ResetCharMaps();

    *gFrontUseAltTriggerMask = 1;

    Bruce_Sync();
    PCGfx_BeginScene(3, -1);

    G_VBLANKS = 0;
    TTime = 0;

    Pad_ClearAll();

    gWaterEffect = 0;
    *gWaterEffectTwo = 0;
    gAttackRelated = 0;
    *gPlayAwayCounter = 0;

    if (*gShellMenuAbort) {
        // storyboard and comic viewers: no frame loop at all, just repaint the
        // sky and report end code 11, which sends SpideyMain back to the shell.
        M3d_FadeColour = 0xFFFFFF;
        M3dInit_SetFoggingParams(0, 6000, 2048);

        G_DB_SKY_COLOR = 0;
        G_BFOGGING_RELATED = 1;
        Db_UpdateSky();

        gLevelStatus = static_cast<i32>(LevelExitReason::ReturnToMenu);
        return false;
    }

    gLevelStatus = static_cast<i32>(LevelExitReason::Running);

    if (*gDoShellForceLevelExit)
        Utils_InitialRand(0x12A6CC58);

    Pause(1);
    Screen_StartCircularFadeIn(32, 8);

    return true;
}

bool LevelSession::advanceFrame() {
    const u32 frameStartTick = framePacer_.currentTick();

    Db_FlipClear();
    CalcPolyBufferEnd();

    Logic();

    if (gLevelStatus) return false;

    Music_MusicUpdate();
    Display();
    gsub_430880();

    drawFrameRate();
    presentFrame(frameStartTick);

    if (G_MECHLIST_PLAYER->IsDead()) {
        gLevelStatus = static_cast<i32>(LevelExitReason::PlayerDied);
        return false;
    }
    return true;
}

void LevelSession::drawFrameRate() {
    if (!*gShowFrameRate) return;

    char frameRateText[8] = "XX FPS";
    const i32 framesPerSecond = 60 / G_MECHLIST_PLAYER->mElapsedTicks;

    if (framesPerSecond <= 9) {
        frameRateText[0] = '0';
        frameRateText[1] = static_cast<char>(framesPerSecond + '0');
    } else {
        frameRateText[0] = static_cast<char>(framesPerSecond / 10 + '0');
        frameRateText[1] = static_cast<char>(framesPerSecond % 10 + '0');
    }

    Mess_SetScale(192);
    Mess_SetTextJustify(1);
    Mess_DrawText(90, 104, frameRateText, 0, 0x1000);
}

void LevelSession::presentFrame(u32 frameStartTick) {
    framePacer_.waitForFrameEnd(frameStartTick);

    DoVblankProcessing = 0;

    DrawSync();

    print_if_false(
            (reinterpret_cast<u32>(G_PPOLY) & 0x7FFFFFFF)
                <= (reinterpret_cast<u32>(G_POLY_BUFFER_END) & 0x7FFFFFFF),
            "Undetected Poly Buffer Overflow");

    gsub_430680();

    if (!DoVblankProcessing) {
        Utils_VblankProcessing();
        DoVblankProcessing = 1;
    }

    PCGfx_EndScene(1);
}

void LevelSession::finish() {
    G_DB_SKY_COLOR = 0;
    Db_UpdateSky();

    if (G_SCENE_RELATED)
        PCGfx_EndScene(1);

    Spool_Sync();

    *gOtPushback = 1;

    if (gLevelStatus == static_cast<i32>(LevelExitReason::LevelCompleted))
        Front_SaveGameState();

    SFX_StopAll();
    Redbook_XAStop();
}

} // namespace application
} // namespace spidey

// Original executable and game scenario tools enter through this adapter.
void PlayAway(void) {
    VblankClock clock;
    spidey::application::FramePacer framePacer(clock, allow60FramesPerSecond());
    spidey::application::LevelSession session(framePacer);
    session.run();
}
