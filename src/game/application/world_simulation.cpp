#include "game/application/world_simulation.h"
#include "game/application/player_frame_trace.h"
#include "game/application/input_update_trace.h"
#include "game/application/main.h"
#include "game/application/PCTimer.h"
#include "game/application/DXinit.h"
#include "game/application/SpideyDX.h"
#include "game/application/tweak.h"
#include "engine/input/PCInput.h"
#include "engine/input/ps2pad.h"
#include "game/player/spidey.h"
#include "game/actors/baddy.h"
#include "game/objects/powerup.h"
#include "game/objects/bullet.h"
#include "game/ui/front.h"
#include "game/ui/pshell.h"
#include "game/ui/mess.h"
#include "game/ui/PCShell.h"
#include "game/cinematics/camera.h"
#include "game/effects/flash.h"
#include "game/effects/shatter.h"
#include "engine/world/actor_list_updater.h"
#include "engine/world/trig.h"
#include "engine/world/bit.h"
#include "engine/rendering/backgrnd.h"
#include "core/utils.h"
#include <cstdlib>
#include <cstdio>

static i32 * const gEndTrainingFlag = reinterpret_cast<i32*>(0x0060CFB0);
static u8 * const gSubmarinerDieRelated = reinterpret_cast<u8*>(0x0060CFC4);
static i32 * const gLogicDebugHookFlag = reinterpret_cast<i32*>(0x0060CFE4);
static i32 * const gTrainingActive = reinterpret_cast<i32*>(0x00682950);

namespace {
void updateActorList(CBody** listHead) {
#ifdef SPIDEY_STANDALONE
    if (!*listHead) return;
    spidey::application::InputUpdateTrace inputTrace;
    spidey::world::ActorListUpdater updater(SuspendedDistance,
        getenv("SPIDEY_TRACE_PAD") ? &inputTrace : nullptr);
    updater.update(listHead, G_MECHLIST->mPos);
#else
    Ob_AI(listHead, 0);
#endif
}
} // namespace

namespace spidey {
namespace application {

void WorldSimulation::update() {
    if (gRenderTest & (InputOnly | InputWithCamera)) {
        Pad_Update();
        return;
    }
    if (gRenderTest & SingleStep) waitForDebugStep();

    ++gAttackRelated;
    ++TTime;
    Pad_Update();

    if (!G_POST_WATER_EFFECT && !*gEndTrainingFlag) updateActiveWorld();
    updateTriggersAndUi();
    updateCameraMode();
}

void WorldSimulation::waitForDebugStep() {
    PCTIMER_Pause();

    // wait for the key to come up, then for the next press. Both loops
    // also give up if the single step bit is cleared while they spin.
    do
    {
        PCINPUT_PollKeyboard();
    }
    while (PCINPUT_IsKeyPressed(57, 0) && (gRenderTest & SingleStep));

    do
    {
        PCINPUT_PollKeyboard();
    }
    while (!PCINPUT_IsKeyPressed(57, 0) && (gRenderTest & SingleStep));

    PCTIMER_Resume();
}

void WorldSimulation::updateActiveWorld() {
#ifdef SPIDEY_STANDALONE
    u8 upAfterPad = G_SCONTROL[0].Up.Pressed;
#endif
    Flash_Update();

    Trig_ResetCPCollisionFlags();
#ifdef SPIDEY_STANDALONE
    if (upAfterPad && !G_SCONTROL[0].Up.Pressed && getenv("SPIDEY_TRACE_PAD"))
        printf("PADCLR before Ob_AI\n");
#endif
    updateActorList(reinterpret_cast<CBody**>(&G_MECHLIST_PLAYER));
    Trig_ResetCPExecutedFlags();

    PlayerFrameTrace::emit();

    updateActorList(&PowerUpList);
    updateActorList(&BulletList);
    updateActorList(&MiscList);
    updateActorList(&G_ENVIRONMENTAL_OBJECT_LIST);
    updateActorList(reinterpret_cast<CBody**>(&BackgroundList));
    updateActorList(reinterpret_cast<CBody**>(&BaddyList));
    updateActorList(&ControlBaddyList);

    Ob_MaybeUnSuspendOrCull();

    Bit_Move();
    Bit_RemoveDeadBits();
}

void WorldSimulation::updateTriggersAndUi() {
    Shatter_MaybeMakeGlassShatterSound();
    Trig_DoPendingCommandLists();

    if (*gSubmarinerDieRelated)
        G_MECHLIST_PLAYER->CutSceneSkipCleanup();

    gsub_430880();

    Front_Update();

    if (*gTrainingActive)
        PShell_EndTrainingUpdate();

    Mess_Update();

    if (!*gLogicDebugHookFlag)
        gsub_430880();
}

void WorldSimulation::updateCameraMode() {
    // no null check on CameraList in this branch, the original does not have
    // one either.
    if (G_GAMESTATE[8])
    {
        G_GAMESTATE[8] = 1;
        G_CAMERA_LIST->SetMode(CAMERAMODE_ITSYLOOKDOWN);
    }
    else if (G_CAMERA_LIST && G_CAMERA_LIST->mCameraMode == CAMERAMODE_ITSYLOOKDOWN)
    {
        G_CAMERA_LIST->SetMode(CAMERAMODE_DEMO);
    }
}

void WorldSimulation::updateCamera() {
    if (!(gRenderTest & InputOnly) || (gRenderTest & InputWithCamera))
        updateActorList(reinterpret_cast<CBody**>(&G_CAMERA_LIST));
}

} // namespace application
} // namespace spidey

void Logic(void) {
    spidey::application::WorldSimulation simulation;
    simulation.update();
}
