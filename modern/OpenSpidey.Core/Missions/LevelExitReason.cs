namespace OpenSpidey.Core.Missions;

/// <summary>Original trigger/application exit values, named at the modern session boundary.</summary>
public enum LevelExitReason
{
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
}
