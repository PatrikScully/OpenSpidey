using System.Text;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.Gameplay;

public sealed class GodotMissionTransitionServices
{
    public required PkrArchive Archive { get; init; }
    public required Func<GodotPlayer?> Player { get; init; }
    /// <summary>Actual Init_KillAll equivalent, including live gameplay/effect/message teardown.</summary>
    public required Action<LevelExitReason> KillAllSceneObjects { get; init; }
    /// <summary>Original completion counts, unlock and save owners. Missing support blocks exit 3.</summary>
    public Func<CancellationToken, Task<bool>>? CompleteLevel { get; init; }
    public string LevelScenePath { get; init; } = "res://Scenes/LevelSession.tscn";
    public string? MenuScenePath { get; init; }
}

/// <summary>Authored mission selection and carried state around real scene teardown and navigation.</summary>
public sealed class GodotMissionLevelTransitions : ILevelTransitionHost, IDisposable
{
    private readonly GameSessionState application;
    private readonly GodotLevelLifecycle lifecycle;
    private readonly GodotMissionTransitionServices services;
    private bool disposed;
    private bool carriedStateCaptured;
    public bool SceneTeardownCompleted { get; private set; }
    public Exception? TeardownFailure { get; private set; }
    public string MissionName => application.MissionName;
    public LevelExitReason ExitReason => lifecycle.ExitReason;
    public bool HasPlayer => CurrentPlayer is not null;
    private GodotPlayer? CurrentPlayer
    {
        get
        {
            GodotPlayer? player = services.Player();
            return player is not null && GodotObject.IsInstanceValid(player) ? player : null;
        }
    }

    public GodotMissionLevelTransitions(GameSessionState application, GodotLevelLifecycle lifecycle,
        GodotMissionTransitionServices services)
    {
        this.application = application ?? throw new ArgumentNullException(nameof(application));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(services.Archive);
        ArgumentNullException.ThrowIfNull(services.Player);
        ArgumentNullException.ThrowIfNull(services.KillAllSceneObjects);
        lifecycle.ConfigureMissionNavigation(this, application, PrepareNavigation, services.CompleteLevel,
            services.LevelScenePath, services.MenuScenePath);
    }

    public void PreserveCarriedPlayerState()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        application.RestorePreviousCarriedState();
    }

    public void SetExitReason(LevelExitReason reason)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        lifecycle.RequestExit((int)reason);
    }

    public void SetMissionName(string name)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        application.SelectMission(name, ResolveAuthoredGeometry(services.Archive, name, application.AlternativeAutoexec));
    }

    public void SetEndLevelNode(ushort node)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        application.SetEndLevelNode(node);
    }

    public void KillAllSceneObjects()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (SceneTeardownCompleted) return;
        if (TeardownFailure is not null)
            throw new InvalidOperationException("The mission's earlier scene teardown failed; it cannot be replayed.", TeardownFailure);
        try
        {
            // CPlayer's destructor observes the status/name already selected by original opcode 181.
            GodotPlayer? player = CurrentPlayer;
            if (!carriedStateCaptured && player is not null)
            {
                application.CapturePlayerState(player.ReadCarriedSaveState());
            }
            carriedStateCaptured = true;
            services.KillAllSceneObjects(ExitReason);
            SceneTeardownCompleted = true;
        }
        catch (Exception exception) { TeardownFailure = exception; throw; }
    }

    private void PrepareNavigation(LevelExitReason reason)
    {
        KillAllSceneObjects();
        application.PrepareLevelLoad(reason);
    }

    /// <summary>Reads the target mission's actual startup environment request, retaining original asset identity.</summary>
    public static string ResolveAuthoredGeometry(PkrArchive archive, string missionName, bool alternativeAutoexec = false)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(missionName);
        if (missionName.Length is 0 or > 9 || missionName.Any(character => character == 0 || character > 255 || character is '/' or '\\' or ':'))
            throw new InvalidDataException("An authored mission name is a single original Latin1 archive name.");
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/" + missionName + ".trg"));
        string path = MissionEnvironmentPlan.Read(triggers, alternativeAutoexec: alternativeAutoexec).AssetPath;
        if (!archive.Contains(path)) throw new FileNotFoundException($"Mission {missionName} requests missing environment {path}.", path);
        return path;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lifecycle.RemoveMissionNavigation(this);
    }
}
