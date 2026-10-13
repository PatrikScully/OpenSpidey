#include "game/application/gameplay_renderer.h"
#include "game/application/baddy_frame_trace.h"
#include "game/application/world_simulation.h"
#include "game/ui/mess.h"
#include "game/ui/panel.h"
#include "game/ui/screen.h"
#include "game/ui/front.h"
#include "game/ui/pshell.h"
#include "game/effects/flash.h"
#include "game/effects/exp.h"
#include "game/player/spidey.h"
#include "game/player/web.h"
#include "game/cinematics/camera.h"
#include "game/actors/chopper.h"
#include "game/actors/docock.h"
#include "game/actors/superock.h"
#include "game/actors/scorpion.h"
#include "game/objects/manipob.h"
#include "game/objects/powerup.h"
#include "game/objects/bullet.h"
#include "core/utils.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/m3dinit.h"
#include "engine/rendering/render_buffers.h"
#include "engine/rendering/backgrnd.h"
#include "engine/rendering/post.h"
#include "engine/world/ob.h"
#include "engine/world/bit.h"
#include "engine/audio/music.h"
#include "compatibility/my_assert.h"
#include <cstdlib>
#include <cstring>
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif

namespace {
// ---------------------------------------------------------------------------
// Compatibility bindings the recovered display routine needs and
// that have no repo variable yet. File-local fixed-address pointers with
// tentative descriptive names. Every address was checked against
// ~/Documents/spidey-work/idbs/idb_globals.txt and against the known-size repo
// objects first, so none of them names a slot inside a bigger global. Where
// another file already invented a name for the same address the same name is
// reused and that file is named in the comment.
// ---------------------------------------------------------------------------

// pshell.cpp uses this name for the same address. Gates the two end of
// training screen calls, PShell_EndTrainingUpdate and PShell_EndTrainingDisplay.
static i32 * const gTrainingActive = reinterpret_cast<i32*>(0x00682950);

// Display draws this string in red at 256,60 when it is not null, so it is a
// debug banner. Nothing in the repo writes it.
static char ** const gDebugBanner = reinterpret_cast<char**>(0x0060CF9C);

// ten dwords in a row, each one a "draw this list" switch that Display reads
// once, right before the matching M3d_Render call. Kept as one indexed block
// rather than ten names because they are contiguous and used identically:
// 0 EnviroList, 1 G_ENVIRONMENTAL_OBJECT_LIST, 2 the player, 3 the player's extra
// body parts, 4 MiscellaneousRenderingList, 5 MiscList, 6 BaddyList,
// 7 BulletList, 8 PowerUpList, 9 BackgroundList.
enum RenderList { Environment, InteractiveObjects, Player, PlayerAttachments,
    AdditionalObjects, Miscellaneous, Enemies, Projectiles, Pickups, Background };
static i32 * const gRenderListFlags = reinterpret_cast<i32*>(0x0054D350);

// idb_globals.txt: GrenadeExplosionRegion. A wibbly texture region id, only
// preprocessed while g3DExplosions is set.
static i32 * const gGrenadeExplosionRegion = reinterpret_cast<i32*>(0x0054A37C);

// idb_globals.txt: gPsxRingIndex / FireRingRegion. Display picks between the
// two for the fire ring depending on gFireRingObject's field_10C.
static i32 * const gPsxRingIndex = reinterpret_cast<i32*>(0x0055AA34);
static i32 * const gFireRingRegion = reinterpret_cast<i32*>(0x0055AA3C);

// idb_globals.txt: FireDomeRegion, the matching region for gFireDomes.
static i32 * const gFireDomeRegion = reinterpret_cast<i32*>(0x0055AA38);

// idb_globals.txt: SymBurnRegion. simby.cpp already calls the counter at
// 0x60CF94 gSymBurnCount; this is the region it turns on.
static i32 * const gSymBurnCount = reinterpret_cast<i32*>(0x0060CF94);
static i32 * const gSymBurnRegion = reinterpret_cast<i32*>(0x0054D388);

// the live fire ring object. Tentative name: it sits two slots before
// gFireDomes/gNumDomes (web.cpp) and Display only reads its field_10C to
// choose which of the two ring regions to preprocess.
static CSuper ** const gFireRingObject = reinterpret_cast<CSuper**>(0x006B559C);

// ps2m3d.cpp calls these gM3dSuperScaleEnabled and gDCUseFixedScale. Display
// turns the fixed vertex scale on around the player's extra body parts.
static i32 * const gM3dSuperScaleEnabled = reinterpret_cast<i32*>(0x005500A4);
static i32 * const gDCUseFixedScale = reinterpret_cast<i32*>(0x0060CF90);

// front.cpp calls this gFrontDrawPolyFlag. It gates the ordering table walk at
// the end of Display, the Mac build's CountPrimitives (inlined here on PC).
static i32 * const gFrontDrawPolyFlag = reinterpret_cast<i32*>(0x0060CFE0);

// where that walk leaves its result: how many ordering table entries carried a
// primitive this frame.
static i32 * const gOtPrimitiveCount = reinterpret_cast<i32*>(0x005FCD68);

// how many bytes of the poly buffer the frame used, written at the very end of
// Display.
static i32 * const gPolyBufferUsed = reinterpret_cast<i32*>(0x0060D00C);

#ifdef SPIDEY_STANDALONE
const char* skippedStages() {
    static const char* stages = std::getenv("SPIDEY_SKIP");
    return stages ? stages : "";
}
#endif

} // namespace

namespace spidey {
namespace application {

void GameplayRenderer::render() {
    prepareCamera();
    drawHud();
    beginWorld();
    prepareEffectTextures();
    drawWorldMessages();
    drawActors();
    finishFrame();
}

void GameplayRenderer::prepareCamera()
{
    if (*gDebugBanner)
    {
        Mess_SetRGB(255, 0, 0, 0);
        Mess_SetScale(256);
        Mess_SetTextJustify(0);
        Mess_DrawText(256, 60, *gDebugBanner, 0, 0x1000);
    }

    WorldSimulation simulation;
    simulation.updateCamera();

    BaddyFrameTrace::emit();

    // no null check on CameraList, same as the original
    G_VIEWPORT.Zoom = static_cast<u16>(G_CAMERA_LIST->GetZoom());

}

void GameplayRenderer::drawHud()
{
#ifdef SPIDEY_STANDALONE
    // SPIDEY_SKIP=fades,panel,bg : leave out draw stages (debugging aid)
    Plat_GfxSetGameView(1);
    Plat_GfxSetHudView(1);
    if (!strstr(skippedStages(), "fades"))
#endif
    Screen_UpdateFades();
#ifdef SPIDEY_STANDALONE
    if (!strstr(skippedStages(), "panel"))
#endif
    Panel_Display();

#ifdef SPIDEY_STANDALONE
    Plat_GfxSetHudView(0);
#endif

}

void GameplayRenderer::beginWorld()
{
    M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);

    if (gRenderListFlags[Background]
#ifdef SPIDEY_STANDALONE
            && !strstr(skippedStages(), "bg")
#endif
            )
        M3d_RenderBackground(BackgroundList);

}

void GameplayRenderer::prepareEffectTextures()
{
    if (g3DExplosions)
        M3d_PreprocessWibblyTextures(*gGrenadeExplosionRegion);

    if (*gFireRingObject)
    {
        if ((*gFireRingObject)->field_10C.pWhatever)
        {
            M3d_PreprocessWibblyTextures(*gFireRingRegion);
            M3d_PreprocessPulsingColours(*gFireRingRegion);
        }
        else
        {
            M3d_PreprocessWibblyTextures(*gPsxRingIndex);
        }
    }

    if (*gSymBurnCount)
    {
        M3d_PreprocessWibblyTextures(*gSymBurnRegion);
        M3d_PreprocessPulsingColours(*gSymBurnRegion);
    }

    if (gFireDomes)
    {
        M3d_PreprocessWibblyTextures(*gFireDomeRegion);
        M3d_PreprocessPulsingColours(*gFireDomeRegion);
    }

}

void GameplayRenderer::drawWorldMessages()
{
    Screen_DrawTarget();
    Screen_DrawArrow();
    Mess_Display();

    if (*gTrainingActive)
        PShell_EndTrainingDisplay();

}

void GameplayRenderer::drawActors()
{
    if (gRenderListFlags[Environment])
        M3d_Render(EnviroList);

    if (gRenderListFlags[InteractiveObjects])
        M3d_Render(G_ENVIRONMENTAL_OBJECT_LIST);

    Music_MusicUpdate();

    if (gRenderListFlags[Player])
        M3d_Render(G_MECHLIST_PLAYER);

    if (*gM3dSuperScaleEnabled)
        *gDCUseFixedScale = 1;

    if (gRenderListFlags[PlayerAttachments])
        M3d_Render(SpideyAdditionalBodyPartsList);

    *gDCUseFixedScale = 0;

    G_MECHLIST_PLAYER->RenderLookaroundReticle();

    if (gRenderListFlags[AdditionalObjects])
        M3d_Render(MiscellaneousRenderingList);

    if (gRenderListFlags[Enemies])
        M3d_Render(BaddyList);

    drawSpecialActorEffects();

    if (gRenderListFlags[Pickups])
        M3d_Render(PowerUpList);

    if (gRenderListFlags[Miscellaneous])
        M3d_Render(MiscList);

    if (gRenderListFlags[Projectiles])
        M3d_Render(BulletList);

}

void GameplayRenderer::drawSpecialActorEffects()
{
    // The two loops below go through CBody vtable slot 5 (offset 0x14) and
    // CBaddy vtable slot 17 (offset 0x44) in the original. Slot 5 is modelled
    // in the repo (CSearchlight::SpecialRenderer, CSniperTarget and
    // CChopperMissile::DrawTargetRecticle are all virtual there), slot 17 is
    // not: the repo's CBaddy declares 12 virtuals and the original has at
    // least 16. Both slots are dispatched on mType here instead, which reaches
    // the same three or four classes because the type ids are unique.
    for (CBody *controller = ControlBaddyList; controller != 0;
            controller = reinterpret_cast<CBody*>(controller->mNextItem))
    {
        u16 type = controller->mType;

        if (type == 322)
            static_cast<CSearchlight*>(controller)->SpecialRenderer();
        else if (type == 323)
            static_cast<CSniperTarget*>(controller)->DrawTargetRecticle();
    }

    for (CBody *enemy = reinterpret_cast<CBody*>(BaddyList); enemy != 0;
            enemy = reinterpret_cast<CBody*>(enemy->mNextItem))
    {
        u16 type = enemy->mType;

        if (type == 321)
        {
            static_cast<CChopperMissile*>(enemy)->DrawTargetRecticle();
            continue;
        }

        if (type == 310)
        {
            // scorpion fight housekeeping, folded into the render loop by the
            // original: forget every object the camera is holding on to except
            // the one the player is actually carrying.
            CCamera *camera = G_CAMERA_LIST;

            if (camera)
            {
                if (camera->mCameraMode == CAMERAMODE_USER)
                {
                    camera->field_F9 = 1;
                }
                else
                {
                    camera->field_F9 = 0;

                    CPlayer *player = G_MECHLIST_PLAYER;

                    for (u32 i = 0; i < 8; i++)
                    {
                        CItem *heldObject = camera->field_184[i];

                        if (heldObject == 0)
                            break;

                        if (player == 0
                                || reinterpret_cast<CItem*>(player->mHeldObject) != heldObject)
                        {
                            heldObject->mFlags &= ~0x800;
                            camera->field_184[i] = 0;
                            player = G_MECHLIST_PLAYER;
                        }
                    }
                }
            }
        }
        else if (type != 308 && type != 309)
        {
            continue;
        }

        if (type == 308)
            static_cast<CDocOc*>(enemy)->RenderClaws();
        else if (type == 309)
            static_cast<CSuperDocOck*>(enemy)->RenderClaws();
        else
            static_cast<CScorpion*>(enemy)->TailRenderer();
    }

}

void GameplayRenderer::finishFrame()
{
    Post_PostProcessEffects();
    M3d_RenderCleanup();
    Music_MusicUpdate();

    countPrimitives();

    Bit_Display();

    if (G_POST_WATER_EFFECT)
        Post_DoPauseDisplayListProcessing();

    Front_Display();
    Flash_Display();

    recordBufferUsage();
}

void GameplayRenderer::countPrimitives()
{
    // the Mac build's CountPrimitives: walk the ordering table from the last
    // entry and count how many links carried a primitive this frame.
    if (*gFrontDrawPolyFlag)
    {
        *gOtPrimitiveCount = 0;

        u32 *entry = reinterpret_cast<u32*>(G_PDOUBLE_BUFFER->OrderingTable[4095]);

        while (entry != reinterpret_cast<u32*>(0xFFFFFF))
        {
            if (*entry & 0xFF000000)
                (*gOtPrimitiveCount)++;

            entry = reinterpret_cast<u32*>(*entry & 0xFFFFFF);

            // always true, the pointer was just masked. Dead assert, kept.
            print_if_false((reinterpret_cast<u32>(entry) & 0xFF000000) == 0, "eh?");
        }
    }

}

void GameplayRenderer::recordBufferUsage()
{
    *gPolyBufferUsed = static_cast<i32>(
            (reinterpret_cast<u32>(G_PPOLY) & 0x7FFFFFFF)
            - (reinterpret_cast<u32>(G_PDOUBLE_BUFFER->Polys) & 0x7FFFFFFF));
}

} // namespace application
} // namespace spidey
