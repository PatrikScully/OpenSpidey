#include "game/application/game_application.h"

#ifdef SPIDEY_STANDALONE
#include "platform/exemem.h"
#include "game/application/SpideyDX.h"
#include "game/application/DXinit.h"
#include "game/application/PCTimer.h"
#include "game/application/main.h"
#include <cstdio>
#include <cstdlib>
#ifdef _WIN32
#include <direct.h>
#else
#include <unistd.h>
#endif

void compile_time_assertions();

namespace spidey {
namespace application {

bool GameApplication::loadGameData(const char* gameDirectory) {
#ifdef _WIN32
    if (gameDirectory && _chdir(gameDirectory) != 0) {
#else
    if (gameDirectory && chdir(gameDirectory) != 0) {
#endif
        std::perror(gameDirectory);
        return false;
    }
    const char* executablePath = std::getenv("SPIDEY_EXE");
    if (!executablePath) executablePath = "SpideyPC.exe";
    if (!ExeMem_Init(executablePath)) return false;
    if (!ExeMem_IsSeeded()) {
        std::puts("WARNING: exe data block not seeded, menus and tables will be empty");
    }
    return true;
}

void GameApplication::configureRenderDefaults() {
    gRenderTest = 0;
    G_GAME_RESOLUTION_X = 640;
    gDxResolutionX = 640;
    G_GAME_RESOLUTION_Y = 480;
    gDxResolutionY = 480;
    gMMXSupport = 1;
}

int GameApplication::run(const char* gameDirectory) {
    if (!loadGameData(gameDirectory)) return 1;
    compile_time_assertions();
    SPIDEYDX_LoadSettings();
    configureRenderDefaults();

    // Preserve the original initialization order: settings, timer, device,
    // then the game loop. The logical canvas remains 640x480; output sizing
    // and widescreen presentation belong to the selected platform backend.
    PCTIMER_Init();
    enum { WindowedFlag = 1, DepthBufferFlag = 2 };
    const bool fullscreen = std::getenv("SPIDEY_FULLSCREEN") != nullptr;
    const i32 deviceFlags = DepthBufferFlag | (fullscreen ? 0 : WindowedFlag);
    DXINIT_DirectX8(0, 0, deviceFlags);
    SpideyMain();
    SPIDEYDX_Shutdown();
    return 0;
}

} // namespace application
} // namespace spidey
#endif
