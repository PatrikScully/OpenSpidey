#pragma once

#include "core/types.h"

namespace spidey {
namespace application {

// Values are shared with the original trigger, menu and level implementations.
enum class LevelExitReason : i32 {
    Running = 0,
    ReloadLevel = 1,
    PlayerDied = 2,
    LevelCompleted = 3,
    DebugNextLevel = 4,
    DebugNextLevelAlternate = 5,
    ActivateLevel = 6,
    QuitLevel = 7,
    RestartFromBeginning = 8,
    RetryRequested = 9,
    MenuStatusRequest = 10,
    ReturnToMenu = 11
};

} // namespace application
} // namespace spidey
