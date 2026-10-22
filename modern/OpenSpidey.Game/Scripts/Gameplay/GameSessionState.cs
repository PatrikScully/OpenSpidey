using Godot;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Core.UI;

namespace OpenSpidey.Game.Gameplay;

/// <summary>Application-owned source state that survives a level scene reload.</summary>
public partial class GameSessionState : Node, IMissionGameLevelHost, IMissionEndLevelNodeHost
{
    public GameSessionState() => WaterPost = new(WaterEffect);
    private string? openedSceneAsset;
    private string initialRestart = "";
    private MissionCheckpointSave? checkpoint;
    private bool clearRestartOnNextOpen;
    private bool awaitingSelectedMissionOpen;
    private string? restoredRestartName;
    public string? SceneAsset { get; private set; }
    public string MissionName { get; private set; } = "";
    public ushort EndLevelNode { get; private set; } = ushort.MaxValue;
    public LevelExitReason ExitReason { get; private set; }
    public PlayerCarriedSaveHistory CarriedHistory { get; set; }
    public CampaignProgress Campaign { get; private set; } = new();
    public int Difficulty { get; set; } = 2;
    public uint RenderTest { get; set; }
    public uint ComicCoverUnlockFlags { get; set; }
    public uint MovieViewerUnlockFlags { get; set; }
    public string SavedRestartPointName => checkpoint?.RestartPointName ?? "";
    // Source sweep direction belongs to the running application, never to the disk save.
    public PlayerTriggerZoneSweep TriggerSweep { get; } = new();
    public OpenSpidey.Core.Mathematics.FixedVector3 AudioListenerPosition { get; set; }
    public int AttackClock { get; set; }
    public CompassState Compass { get; } = new();
    public CameraNormalProjectionCache CameraProjectionCache { get; } = new();
    public int ForceEarlyLevelExit { get; set; }
    public PlayerWaterEffectState WaterEffect { get; } = new();
    public CampaignSceneWaterState SceneWater { get; } = new();
    public SparkSettings Sparks { get; } = new();
    public SourceMusicState Music { get; } = new();
    public ScorpionApproachState ScorpionApproach { get; } = new();
    public PlayerModuleHook PlayerModuleHook { get; } = new();
    public CinematicScreenState CinematicScreen { get; } = new();
    public bool WhatIf { get; set; }
    /// <summary>Separate source JoelJewCheatCode flag selecting the first alternate autoexec node.</summary>
    public bool AlternativeAutoexec { get; set; }
    public int EndTrainingFlag { get; set; }
    /// <summary>Shell-owned gTrainingSeconds/score, retained across training levels.</summary>
    public TrainingResults TrainingResults { get; } = new();
    public SourceWaterPostState WaterPost { get; }
    public int FrontPauseState { get; set; }
    public MissionCountdown Countdown { get; } = new();
    public MissionTimerDisplayState TimerDisplay { get; } = new();
    public int GameLevel { get; set; }

    public MissionCheckpointSave OpenLevel(string geometryAsset, string launchRestart)
    {
        ArgumentNullException.ThrowIfNull(geometryAsset);
        if (awaitingSelectedMissionOpen && !geometryAsset.Equals(SceneAsset, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Mission {MissionName} selected {SceneAsset}, but bootstrap requested {geometryAsset}.");
        WaterEffect.Value = 0;
        bool changedScene = !geometryAsset.Equals(openedSceneAsset, StringComparison.OrdinalIgnoreCase);
        if (changedScene)
        {
            string restart = restoredRestartName ?? (clearRestartOnNextOpen ? "" : launchRestart);
            checkpoint = new(restart);
            initialRestart = restart;
            openedSceneAsset = geometryAsset;
        }
        else if (checkpoint is null) checkpoint = new(clearRestartOnNextOpen ? "" : launchRestart);
        clearRestartOnNextOpen = false;
        restoredRestartName = null;
        SceneAsset = geometryAsset;
        if (MissionName.Length == 0 || changedScene && !awaitingSelectedMissionOpen)
            MissionName = InitialMissionName(geometryAsset);
        awaitingSelectedMissionOpen = false;
        ExitReason = LevelExitReason.Running;
        ResetEndLevelNode();
        return checkpoint ?? throw new InvalidOperationException("The application has no current level checkpoint.");
    }

    public void SelectMission(string name, string geometryAsset)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(geometryAsset);
        if (name.Length is 0 or > 9 || name.Any(character => character == 0 || character > 255))
            throw new InvalidDataException("An authored mission name must fit the original nine-byte Latin1 copy.");
        MissionName = name;
        SceneAsset = geometryAsset;
        awaitingSelectedMissionOpen = true;
    }

    public void SetExitReason(LevelExitReason reason) => ExitReason = reason;
    public void SetEndLevelNode(ushort node) => EndLevelNode = node;
    public void ResetEndLevelNode() => EndLevelNode = ushort.MaxValue;
    public void CapturePlayerState(PlayerCarriedSaveState live) =>
        CarriedHistory = PlayerCarriedSavePolicy.OnPlayerDestroyed(CarriedHistory, ExitReason, live);
    public void RestorePreviousCarriedState() => CarriedHistory = PlayerCarriedSavePolicy.RestorePrevious(CarriedHistory);

    /// <summary>Disk save restart selection, including the original empty-name state before authored startup selects a node.</summary>
    public void RestoreSavedRestartPointName(string name)
    {
        checkpoint = new(name);
        initialRestart = name;
        restoredRestartName = name;
    }

    public void RestoreCampaign(CampaignProgress progress, int difficulty, uint comicCoverFlags, uint movieViewerFlags)
    {
        Campaign = progress ?? throw new ArgumentNullException(nameof(progress));
        Difficulty = difficulty;
        ComicCoverUnlockFlags = comicCoverFlags;
        MovieViewerUnlockFlags = movieViewerFlags;
    }

    /// <summary>Completion clears the restart-name field; retries retain the actual checkpoint selection.</summary>
    public void PrepareLevelLoad(LevelExitReason reason)
    {
        if (reason != LevelExitReason.LevelCompleted) return;
        checkpoint = null;
        initialRestart = "";
        clearRestartOnNextOpen = true;
        restoredRestartName = null;
    }

    public void RestartFromBeginning()
    {
        if (checkpoint is null) throw new InvalidOperationException("No level is selected for restart.");
        checkpoint = new(initialRestart);
    }

    private static string InitialMissionName(string geometryAsset)
    {
        string name = Path.GetFileName(geometryAsset.Replace('\\', '/'));
        if (!name.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("An initial mission requires its original _G.psx geometry name.");
        return name[..^6] + "_T";
    }
}
