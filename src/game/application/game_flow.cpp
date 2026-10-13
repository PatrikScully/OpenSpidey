#include "game/application/game_flow.h"

namespace spidey {
namespace application {

void GameFlow::run(bool forceModelPreview) {
    menuRequests_ = MenuRequests();
    State state = actions_.initialize(forceModelPreview) ? State::Menu : State::Shutdown;
    while (state != State::Shutdown) {
        switch (state) {
        case State::Menu:
            state = actions_.selectLevel(menuRequests_) ? State::LoadLevel : State::Shutdown;
            break;
        case State::LoadLevel:
            actions_.loadLevel();
            state = State::PlayLevel;
            break;
        case State::PlayLevel:
            state = handleLevelExit(actions_.playLevel());
            break;
        case State::Shutdown:
            break;
        }
    }
    actions_.shutdown();
}

GameFlow::State GameFlow::handleLevelExit(LevelExitReason reason) {
    switch (reason) {
    case LevelExitReason::ReloadLevel:
        actions_.prepareReload();
        return State::LoadLevel;
    case LevelExitReason::PlayerDied:
    case LevelExitReason::RetryRequested:
        if (actions_.offerRetry()) return State::PlayLevel;
        // offerRetry already performed the cleanup and fade. Declining a
        // retry uses the same destination as QuitLevel, without doing them twice.
        return actions_.canReturnToMenu() ? State::Menu : State::Shutdown;
    case LevelExitReason::LevelCompleted:
        if (actions_.completeLevel()) {
            menuRequests_.finalLevelCompleted = 1;
            return State::Menu;
        }
        return State::PlayLevel;
    case LevelExitReason::DebugNextLevel:
    case LevelExitReason::DebugNextLevelAlternate:
        actions_.advanceDebugLevel();
        return State::LoadLevel;
    case LevelExitReason::ActivateLevel:
        actions_.activateLevel();
        return State::PlayLevel;
    case LevelExitReason::QuitLevel:
        actions_.quitLevel();
        return actions_.canReturnToMenu() ? State::Menu : State::Shutdown;
    case LevelExitReason::RestartFromBeginning:
        actions_.restartFromBeginning();
        return State::PlayLevel;
    case LevelExitReason::MenuStatusRequest:
        menuRequests_.statusRequested = 1;
        actions_.prepareMenuStatus();
        return State::Menu;
    case LevelExitReason::ReturnToMenu:
        actions_.prepareMenuReturn();
        return State::Menu;
    default:
        actions_.reportUnknownExit(reason);
        return State::Shutdown;
    }
}

} // namespace application
} // namespace spidey
