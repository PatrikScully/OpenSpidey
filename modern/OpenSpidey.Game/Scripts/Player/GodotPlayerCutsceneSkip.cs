using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player.Scripting;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;

namespace OpenSpidey.Game.Player;

/// <summary>Actual manual pad gate before private webs, and cleanup only after pending mission commands.</summary>
public sealed class GodotPlayerCutsceneSkip : IPlayerCutsceneCleanupHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerScripts scripts;
    private readonly TriggerFile triggers;
    private readonly GodotActorRegistry registry;
    private readonly PlayerFollowCamera camera;
    private readonly GodotSpeechPlayer speech;
    private readonly Func<bool> forceMenuExit;
    private readonly Action<FixedVector3>? prepareDistanceAttack;
    private bool failed;
    public PlayerCutsceneSkipState State => scripts.SkipState;
    public int ManualSkips { get; private set; }
    public int CleanupCount { get; private set; }
    public event Action? IdleAnimationPlayed;
    public event Action? CleanupCompleted;

    public GodotPlayerCutsceneSkip(GodotPlayer player, TriggerFile triggers, GodotActorRegistry registry,
        PlayerFollowCamera camera, GodotSpeechPlayer speech, Func<bool> forceMenuExit,
        Action<FixedVector3>? prepareDistanceAttack = null)
    {
        this.player = player; this.triggers = triggers; this.registry = registry; this.camera = camera;
        this.speech = speech; this.forceMenuExit = forceMenuExit ?? throw new ArgumentNullException(nameof(forceMenuExit));
        this.prepareDistanceAttack = prepareDistanceAttack;
        scripts = player.Scripts ?? throw new InvalidOperationException("Cutscene skip requires the actual player input VM.");
        if (scripts.CutsceneSkip is not null) throw new InvalidOperationException("The player VM already has its source skip owner.");
        if (!ReferenceEquals(camera.Target, player)) throw new InvalidOperationException("Cutscene skip requires the actual player camera.");
        scripts.CutsceneSkip = this;
    }

    public void PreparePlayerAi()
    {
        if (failed) throw new InvalidOperationException("Partially executed cutscene cleanup cannot be replayed.");
        if (!PlayerCutsceneSkip.ReadManualInput(State, scripts.Active, forceMenuExit(), registry.Difficulty, player.PhysicalPad)) return;
        registry.Environment = registry.Environment with { FastForwardDeath = true };
        ManualSkips++;
    }

    public PlayerScriptEnvironment ScriptEnvironment(PlayerScriptEnvironment environment) => environment with
    {
        Abort = registry.Environment.FastForwardDeath,
        ForceMenuExit = forceMenuExit()
    };

    public void FinishSourceFrame()
    {
        if (failed) throw new InvalidOperationException("Partially executed cutscene cleanup cannot be replayed.");
        if (!registry.Environment.FastForwardDeath) return;
        try
        {
            PlayerCutsceneSkip.Cleanup(State, this, player.PhysicalPad);
            player.RefreshCombatTransform();
            CleanupCount++;
            CleanupCompleted?.Invoke();
        }
        catch { failed = true; throw; }
    }

    public int CameraMode => camera.SourceControlMode;
    public int LevelId => registry.Environment.LevelId;
    public int CheckpointNode => scripts.Runtime?.CheckpointNode ?? 0;
    public FixedVector3 Position => player.Controller.Position;
    public FixedVector3 ForwardDirection => player.Controller.Orientation.Forward;
    public IReadOnlyList<int> CheckpointLinks(int node) => MissionRuntime.ReadLinks(Node(node)).Select(link => (int)link).ToArray();
    public FixedVector3 TriggerPosition(int node) => scripts.TriggerPosition(node);
    public void StopSpeech() => speech.Controller.Stop();
    public void SetCameraMode(ushort mode) => camera.SetSourceCameraMode(mode);
    public void SetFloorNormalAndOrient(FixedVector3 direction)
    {
        player.Combat.State.CrawlSurfaceNormal = new(0, -4096, 0);
        player.Combat.OrientToNormal(true, direction);
    }
    public void PrepareDistanceAttack(FixedVector3 direction) =>
        (prepareDistanceAttack ?? throw new NotSupportedException("Skipping a checkpoint-bearing cutscene requires the genuine PriorToVenomDistanceAttack resource owner."))(direction);
    public void PlayIdleAnimation()
    {
        player.Combat.PlaySingleAnimation(0, 0, -1);
        IdleAnimationPlayed?.Invoke();
    }
    public void Stand() => player.Combat.SwitchToStandMode();
    public void SetCameraStartPosition() => camera.SetStartPosition();
    public void ClearAbort() => registry.Environment = registry.Environment with { FastForwardDeath = false };

    private TriggerNode Node(int node)
    {
        if ((uint)node >= triggers.Nodes.Count || triggers.Nodes[node].Id != node)
            throw new InvalidDataException($"Cutscene checkpoint references missing source node{node}.");
        return triggers.Nodes[node];
    }
}
