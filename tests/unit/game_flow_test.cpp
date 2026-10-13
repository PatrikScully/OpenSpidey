#include "game/application/game_flow.h"
#include <cassert>
#include <initializer_list>
#include <string>
#include <vector>

using spidey::application::GameFlow;
using spidey::application::GameFlowActions;
using spidey::application::LevelExitReason;
using spidey::application::MenuRequests;

class TestGameActions : public GameFlowActions {
public:
    TestGameActions(std::initializer_list<LevelExitReason> exits)
        : exits(exits), exitIndex(0), menuVisits(0), allowedMenuVisits(1),
          retriesAccepted(true), returnToMenu(true), initializationReady(true),
          completedFinalLevel(false), completeIndex(0), retainedRequestVisit(0) {}

    bool initialize(bool forceModelPreview) override {
        events.push_back(forceModelPreview ? "preview" : "initialize");
        return initializationReady;
    }
    bool selectLevel(MenuRequests& requests) override {
        events.push_back("menu");
        menuRequests.push_back(requests);
        if (menuVisits + 1 != retainedRequestVisit) requests = MenuRequests();
        return ++menuVisits <= allowedMenuVisits;
    }
    void loadLevel() override { events.push_back("load"); }
    LevelExitReason playLevel() override {
        events.push_back("play");
        assert(exitIndex < exits.size());
        return exits[exitIndex++];
    }
    void prepareReload() override { events.push_back("reload"); }
    bool offerRetry() override {
        events.push_back("retry");
        return retriesAccepted;
    }
    bool completeLevel() override {
        events.push_back("complete");
        if (completeIndex < completionResults.size())
            return completionResults[completeIndex++];
        return completedFinalLevel;
    }
    void advanceDebugLevel() override { events.push_back("debug-next"); }
    void activateLevel() override { events.push_back("activate"); }
    void quitLevel() override { events.push_back("quit"); }
    bool canReturnToMenu() const override { return returnToMenu; }
    void restartFromBeginning() override { events.push_back("restart"); }
    void prepareMenuStatus() override { events.push_back("menu-status"); }
    void prepareMenuReturn() override { events.push_back("menu-return"); }
    void reportUnknownExit(LevelExitReason) override { events.push_back("unknown-exit"); }
    void shutdown() override { events.push_back("shutdown"); }

    std::vector<LevelExitReason> exits;
    std::size_t exitIndex;
    unsigned menuVisits;
    unsigned allowedMenuVisits;
    bool retriesAccepted;
    bool returnToMenu;
    bool initializationReady;
    bool completedFinalLevel;
    std::size_t completeIndex;
    unsigned retainedRequestVisit;
    std::vector<bool> completionResults;
    std::vector<std::string> events;
    std::vector<MenuRequests> menuRequests;
};

static void checkRetryDestinations() {
    // Declining retry has already cleaned up the level. It must never invoke
    // quitLevel's second cleanup, whether returning to a menu or shutting down.
    for (bool returnToMenu : {false, true}) {
        for (LevelExitReason reason : {LevelExitReason::PlayerDied, LevelExitReason::RetryRequested}) {
            TestGameActions actions({reason});
            actions.retriesAccepted = false;
            actions.returnToMenu = returnToMenu;
            GameFlow flow(actions);
            flow.run();
            std::vector<std::string> expected = {"initialize", "menu", "load", "play", "retry"};
            if (returnToMenu) expected.push_back("menu");
            expected.push_back("shutdown");
            assert(actions.events == expected);
        }
    }
}

static void checkLevelAndMenuSequence() {
    TestGameActions actions({
        LevelExitReason::ReloadLevel, LevelExitReason::PlayerDied,
        LevelExitReason::RetryRequested, LevelExitReason::RestartFromBeginning,
        LevelExitReason::ActivateLevel, LevelExitReason::DebugNextLevel,
        LevelExitReason::DebugNextLevelAlternate, LevelExitReason::LevelCompleted,
        LevelExitReason::MenuStatusRequest, LevelExitReason::ReturnToMenu,
        LevelExitReason::LevelCompleted, LevelExitReason::QuitLevel
    });
    actions.allowedMenuVisits = 4;
    actions.completionResults = {false, true};
    GameFlow flow(actions);
    flow.run();
    const std::vector<std::string> expected = {
        "initialize", "menu", "load", "play", "reload", "load",
        "play", "retry", "play", "retry", "play", "restart",
        "play", "activate", "play", "debug-next", "load",
        "play", "debug-next", "load", "play", "complete",
        "play", "menu-status", "menu", "load", "play", "menu-return",
        "menu", "load", "play", "complete", "menu", "load",
        "play", "quit", "menu", "shutdown"
    };
    assert(actions.events == expected);
    assert(actions.exitIndex == actions.exits.size());
    assert(actions.menuRequests.size() == 5);
    assert(actions.menuRequests[1].statusRequested == 1);
    assert(actions.menuRequests[1].finalLevelCompleted == 0);
    assert(actions.menuRequests[2].statusRequested == 0);
    assert(actions.menuRequests[3].finalLevelCompleted == 1);
    assert(actions.menuRequests[3].statusRequested == 0);
    assert(actions.menuRequests[4].finalLevelCompleted == 0);
}

int main() {
    checkRetryDestinations();
    checkLevelAndMenuSequence();

    // A forced selection does not show the menu, so it must retain requests.
    // The next actual menu consumes both status and final-completion flags.
    TestGameActions forcedSelection({LevelExitReason::MenuStatusRequest,
        LevelExitReason::LevelCompleted, LevelExitReason::QuitLevel});
    forcedSelection.allowedMenuVisits = 3;
    forcedSelection.completedFinalLevel = true;
    forcedSelection.retainedRequestVisit = 2;
    GameFlow(forcedSelection).run();
    assert(forcedSelection.menuRequests.size() == 4);
    assert(forcedSelection.menuRequests[2].statusRequested == 1);
    assert(forcedSelection.menuRequests[2].finalLevelCompleted == 1);
    assert(forcedSelection.menuRequests[3].statusRequested == 0);
    assert(forcedSelection.menuRequests[3].finalLevelCompleted == 0);

    TestGameActions shellQuit({});
    shellQuit.allowedMenuVisits = 0;
    GameFlow(shellQuit).run();
    assert((shellQuit.events == std::vector<std::string>{"initialize", "menu", "shutdown"}));

    TestGameActions preview({});
    preview.initializationReady = false;
    GameFlow(preview).run(true);
    assert((preview.events == std::vector<std::string>{"preview", "shutdown"}));

    TestGameActions shellFreeQuit({LevelExitReason::QuitLevel});
    shellFreeQuit.returnToMenu = false;
    GameFlow(shellFreeQuit).run();
    assert((shellFreeQuit.events == std::vector<std::string>{
        "initialize", "menu", "load", "play", "quit", "shutdown"}));

    // Running is not a valid level exit, and unrecognized trigger values must
    // report failure and shut down rather than repeatedly reload the level.
    for (i32 value : {0, 99}) {
        TestGameActions invalid({static_cast<LevelExitReason>(value)});
        GameFlow(invalid).run();
        assert((invalid.events == std::vector<std::string>{
            "initialize", "menu", "load", "play", "unknown-exit", "shutdown"}));
    }
}
