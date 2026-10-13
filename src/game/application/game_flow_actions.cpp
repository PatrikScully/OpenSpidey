#include "game/application/game_flow.h"
#include "game/application/level_session_state.h"
#include "game/application/main.h"
#include "game/application/init.h"
#include "game/application/DXinit.h"
#include "game/application/SpideyDX.h"
#include "game/ui/front.h"
#include "game/ui/pshell.h"
#include "game/ui/shell.h"
#include "game/ui/mess.h"
#include "game/ui/screen.h"
#include "game/ui/PCShell.h"
#include "game/ui/rfront.h"
#include "game/player/spidey.h"
#include "game/cinematics/camera.h"
#include "game/cinematics/ps2gamefmv.h"
#include "engine/rendering/PCTex.h"
#include "engine/rendering/PCGfx.h"
#include "engine/rendering/render_buffers.h"
#include "engine/rendering/m3dinit.h"
#include "engine/assets/spool.h"
#include "engine/assets/reloc.h"
#include "engine/input/PCInput.h"
#include "engine/world/trig.h"
#include "core/utils.h"

// 0x4000 dwords, not 0x1000: SpideyMain's rep stosd at 0x455CC5 clears
// 0x4000 of them. Real address 0x5FCE04.
#ifndef SPIDEY_STANDALONE
EXPORT i32 gMainStuff[0x4000];
#else
extern i32 gMainStuff[0x4000];
#endif

extern void trigLog(const char* format, ...);

namespace {

// forced start level, -1 = none. gRenderTest bit 2 and this together decide
// whether SpideyMain shows the shell at all. Not in idb_globals.txt; the next
// named address is 0x568FB4 WindowName, so this is a standalone dword.
static i32 * const gStartLevelIndex = reinterpret_cast<i32*>(0x00568FB0);

// checked once, right after the shell returns: nonzero means quit the game.
static u8 * const gQuitAfterShell = reinterpret_cast<u8*>(0x0060CF88);

// the debug level cycle: gDebugLevelIndex is a dword but only its low byte is
// ever touched, so it is typed u8 here. gDebugLevelNames is the name pointer
// of record 0 of a table of
// 192 byte records. MSVC folded the field offset into the base address, so
// 0x60CF84 is "record 0's name", not necessarily the start of the table
// (gLevelStatus at 0x60CFA4 sits inside what would be record 0).
static u8 * const gDebugLevelIndex = reinterpret_cast<u8*>(0x0060CFA0);
static char ** const gDebugLevelNames = reinterpret_cast<char**>(0x0060CF84);

// zeroed once per pass of the level loop, right after PlayAway returns. Sits
// immediately after Pad_IdleTime (0x66129C, pcdcPad.cpp) and before gPadInited
// (0x6612AC), so it reads like a second idle counter. Tentative name.
static i32 * const gLevelIdleTime = reinterpret_cast<i32*>(0x006612A0);

// idb_globals.txt: gBitServer. Only used here, and only to delete it through
// its vtable at shutdown, so all we know about the type is that it has a
// virtual destructor. Modelled as CClass, which is what every deletable game
// object in this repo derives from.
static CClass ** const gBitServer = reinterpret_cast<CClass**>(0x0056EB50);

// The periodic CD recheck, 0x515D80, in SpideyDX.cpp's address range. It runs
// the disc test at 0x5163E0 behind an anti-tamper junk loop and, if the disc is
// gone, posts WM_CLOSE and exits the process. Deliberately not decompiled: the
// repo has a SPIDEY_NO_CD_CHECK dev toggle that patches 0x5163E0, and writing
// the check out here would fight it. Forwarded so SpideyMain's call site is
// honest.
// @Bogus
void checkGameDisc() {
#ifndef SPIDEY_STANDALONE
    typedef void (*DiscCheck)();
    reinterpret_cast<DiscCheck>(0x00515D80)();
#endif
}


using spidey::application::GameFlow;
using spidey::application::GameFlowActions;
using spidey::application::LevelExitReason;
using spidey::application::MenuRequests;

class OriginalGameFlowActions : public GameFlowActions {
public:
    bool initialize(bool forceModelPreview) override {
        trigLog("xxx main\n");

        for (i32 index = 0; index < 0x4000; ++index) {
            gMainStuff[index] = 0x4B415453;
        }

        gMainStuff[0] = 0x544C4148;

        Init_AtStart(1);
        PCTex_LoadPcIcons();
        GameFMV_PlayMovie(0, 1, 1, 2.5f);
        GameFMV_PlayMovie(1, 1, 1, 1.0f);
        GameFMV_PlayMovie(2, 1, 1, 1.0f);
        GameFMV_PlayMovie(3, 1, 1, 1.0f);

        Init_Cleanup(0);
        gRunCinemaRelated = 0;

        while (G_GAME_FADE)
            ;

        if (forceModelPreview || (gRenderTest & 8)) {
            Spool_ClearAllPSXs();
            PCGfx_DoModelPreview();
            Init_Cleanup(0);
            return false;
        }
        Mess_LoadFont("font_big.fnt", -1, -1, -1);
        Mess_LoadFont("sp_fnt02.fnt", -1, -1, -1);
        Mess_LoadFont("sp_fnt03.fnt", -1, -1, -1);

        PShell_NormalFont();
        return true;
    }

    bool selectLevel(MenuRequests& requests) override {
        M3d_FadeColour = 0xFFFFFF;
        M3dInit_SetFoggingParams(0, 6000, 2048);

        if (!(gRenderTest & 4) && *gStartLevelIndex == -1)
        {
            // normal path: hand control to the shell module and take the level
            // name it leaves in gSaveGame
            ((void(*)(i32))gsub_430880)(0);

            Reloc_Load("shell", 1);

            u32 shellParams[2] = {requests.statusRequested, requests.finalLevelCompleted};
            requests = MenuRequests();

            Reloc_CallUserFunction("shell", 0, shellParams, 0);
            Reloc_Unload("shell");

            // the "what if" version of level 1 area 2
            if (Utils_CompareStrings("l1a2_t", G_SAVE_GAME.mLevelName) && gWhatIf)
                Utils_CopyString("l1a2a_t", G_SAVE_GAME.mLevelName, 9);

            if (*gQuitAfterShell)
            {
                Init_Cleanup(3);
                return false;
            }
        }
        else if (*gStartLevelIndex >= 0)
        {
            // a level was forced from the command line: take its name straight out
            // of the level table and throw the restart point away
            Utils_CopyString(Levels[*gStartLevelIndex].mName, G_SAVE_GAME.mLevelName, 9);

            u8* saveBytes = reinterpret_cast<u8*>(&G_SAVE_GAME);

            G_SAVE_GAME.mRestartPointName[0] = 0;
            *reinterpret_cast<i32*>(saveBytes + 0x50) = 0;
            saveBytes[0x79] = 0;
            *reinterpret_cast<i32*>(saveBytes + 0x48) = 0;
            *reinterpret_cast<i32*>(saveBytes + 0x4C) = 0;
        }

        DXINIT_SetDisplayOptions(gPendingResolutionX, gPendingResolutionY,
                gPendingColorDepth, gLowGraphics, gBrightnessRelated);

        PCINPUT_SetMouseBounds(0, 0, gPendingResolutionX - 32, gPendingResolutionY - 32);
        PCINPUT_SetMousePosition((gPendingResolutionX - 32) >> 1,
                (gPendingResolutionY - 32) >> 1);
        return true;
    }

    void loadLevel() override {
        checkGameDisc();
        loadSavedLevel(false, false);
    }

    LevelExitReason playLevel() override {
        PlayAway();
        PCGfx_EndScene(1);

        const LevelExitReason reason = static_cast<LevelExitReason>(gLevelStatus);
        *gLevelIdleTime = 0;

        if (reason != LevelExitReason::PlayerDied && reason != LevelExitReason::RetryRequested)
            gRunCinemaRelated = 0;

        if (reason != LevelExitReason::Running)
            *gFrontUseAltTriggerMask = 0;
        return reason;
    }

    void prepareReload() override {
        Init_Cleanup(0);
        gSpideyMainRelated = 5;
    }

    bool offerRetry() override {
        prepareExitFade();

        G_GAME_FADE = 10;

        while (G_GAME_FADE)
            ;

        if (!Front_ContinueExit())
            return false;

        loadSavedLevel(true, false);
        return true;
    }

    bool completeLevel() override {
        Init_Cleanup(0);
        Screen_SepiaFade();

        if (Utils_CompareStrings(G_SAVE_GAME.mLevelName, "l8a7_t"))
        {
            // the last area of level 8 counts as l8a6_t. No -1 check on the
            // index here, only the assert, same as the original.
            const i32 levelIndex = Front_GetLevelIndex("l8a6_t");

            print_if_false(levelIndex != -1, "Could not find l8a6_t ???");

            if (G_SAVE_GAME.mLevelCompletionCounts[levelIndex] < 0xFF)
                G_SAVE_GAME.mLevelCompletionCounts[levelIndex]++;

            PShell_MaybeUnlockStuff();

            return true;
        }

        i32 levelIndex = Front_GetLevelIndex(G_SAVE_GAME.mLevelName);

        if (levelIndex != -1)
        {
            levelIndex--;

            print_if_false(levelIndex >= 0 && levelIndex < 34, "Bad LevelIndex");

            if (G_SAVE_GAME.mLevelCompletionCounts[levelIndex] < 0xFF)
                G_SAVE_GAME.mLevelCompletionCounts[levelIndex]++;
        }

        PShell_MaybeUnlockStuff();

        while (G_GAME_FADE)
            ;

        if (!(gRenderTest & 0x80))
        {
            // autosave on the easy difficulties, or when the level itself asks
            // for it through SLevel::field_8 bit 1
            if (G_DIFFICULTY_LEVEL == 0)
            {
                PShell_MaybeSaveGame();
            }
            else
            {
                const SLevel* level = Front_FindLevel(G_SAVE_GAME.mLevelName);

                if (level && (level->field_8 & 2))
                    PShell_MaybeSaveGame();
            }
        }

        loadSavedLevel(false, true);
        return false;
    }

    void advanceDebugLevel() override {
        Init_Cleanup(0);

        (*gDebugLevelIndex)++;

        const char* nextLevelName = *reinterpret_cast<char**>(
                reinterpret_cast<u8*>(gDebugLevelNames) + *gDebugLevelIndex * 192);

        if (*nextLevelName == 0)
            *gDebugLevelIndex = 0;
    }

    void activateLevel() override {
        Init_Cleanup(2);
        Trig_ParseTRGFile();

        CPlayer* player = new CPlayer();

        Trig_ExecuteRestart();

        // the camera attaches itself to CameraList, nothing keeps this pointer
        new CCamera(player);
    }

    void quitLevel() override {
        prepareExitFade();
    }

    bool canReturnToMenu() const override {
        return !(gRenderTest & 4) && *gStartLevelIndex < 0;
    }

    void restartFromBeginning() override {
        Front_ClearScreen();
        Init_Cleanup(0);

        G_SAVE_GAME.mRestartPointName[0] = 0;

        loadSavedLevel(false, false);
    }

    void prepareMenuStatus() override {
        prepareExitFade();
    }

    void prepareMenuReturn() override {
        Init_Cleanup(0);
    }

    void reportUnknownExit(LevelExitReason) override {
        print_if_false(0, "Unknown EndCode");
    }

    void shutdown() override {
        while (G_GAME_FADE)
            Pause(1);

        delete pYesNoMenu;
        delete *gBitServer;

        Mess_UnloadAllFonts();
        PCSHELL_Shutdown();
        PCTex_FreePcIcons();
        Db_DeleteOTsAndPolyBuffers();
        Init_AtEnd();
    }

private:
    void prepareExitFade() {
        gSpideyMainRelated = 20;
        Init_Cleanup(0);
        Screen_SepiaFade();
    }

    void loadSavedLevel(bool retainRestartNode, bool completedArea) {
        // This release hook accepts a level id in the original executable,
        // although its folded no-op has a parameterless recovered declaration.
        reinterpret_cast<void(*)(i32)>(gsub_430880)(Trig_GetLevelId());
        Front_LoadGame(&G_SAVE_GAME, retainRestartNode ? 1 : 0, completedArea);
    }

};

} // namespace

namespace spidey {
namespace application {

void runGameFlow(bool forceModelPreview) {
    OriginalGameFlowActions actions;
    GameFlow flow(actions);
    flow.run(forceModelPreview);
}

} // namespace application
} // namespace spidey
