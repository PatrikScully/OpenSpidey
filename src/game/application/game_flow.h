#pragma once

#include "game/application/level_exit_reason.h"

namespace spidey {
namespace application {

struct MenuRequests {
    u32 statusRequested;
    u32 finalLevelCompleted;

    MenuRequests() : statusRequested(0), finalLevelCompleted(0) {}
};

// Effects of application transitions. The production implementation adapts
// the existing level, menu, save and cinematic systems; tests supply a host
// that records transitions without needing game assets or a display.
class GameFlowActions {
public:
    virtual ~GameFlowActions() {}
    virtual bool initialize(bool forceModelPreview) = 0;
    // Consume requests when the shell is actually shown; forced level entry
    // retains them. False means the shell requested application shutdown.
    virtual bool selectLevel(MenuRequests& requests) = 0;
    virtual void loadLevel() = 0;
    virtual LevelExitReason playLevel() = 0;
    virtual void prepareReload() = 0;
    virtual bool offerRetry() = 0;
    // True means the last level completed; false means the next area loaded.
    virtual bool completeLevel() = 0;
    virtual void advanceDebugLevel() = 0;
    virtual void activateLevel() = 0;
    virtual void quitLevel() = 0;
    // Shell-free games shut down instead of offering another menu selection.
    virtual bool canReturnToMenu() const = 0;
    virtual void restartFromBeginning() = 0;
    virtual void prepareMenuStatus() = 0;
    virtual void prepareMenuReturn() = 0;
    virtual void reportUnknownExit(LevelExitReason reason) = 0;
    virtual void shutdown() = 0;
};

class GameFlow {
public:
    explicit GameFlow(GameFlowActions& actions) : actions_(actions) {}
    void run(bool forceModelPreview = false);

private:
    enum class State { Menu, LoadLevel, PlayLevel, Shutdown };
    State handleLevelExit(LevelExitReason reason);

    GameFlowActions& actions_;
    MenuRequests menuRequests_;
};

// Creates the production action adapter behind the original SpideyMain export.
void runGameFlow(bool forceModelPreview);

} // namespace application
} // namespace spidey
