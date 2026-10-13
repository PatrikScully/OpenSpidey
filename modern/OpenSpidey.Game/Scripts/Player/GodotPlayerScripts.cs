using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;

namespace OpenSpidey.Game.Player;

/// <summary>Owns the input VM and connects its actor effects to the live player.</summary>
public sealed class GodotPlayerScripts(GodotPlayer player, TriggerFile triggers,
    Func<PlayerScriptEnvironment> environment) : IPlayerScriptHost
{
    public PlayerScriptRuntime? Runtime { get; private set; }
    public PlayerScriptProgram? CurrentProgram { get; private set; }
    public int ProgramsStarted { get; private set; }
    public bool Active => Runtime?.Active == true;
    public PlayerScriptInput? LastInput { get; private set; }
    public bool AnimationSignal { get; set; }
    public int ScriptedActionValue { get; set; }
    public bool ScriptFlag { get; private set; }
    public int IdleTimer { get; private set; }
    public ScriptAnimationPolicy LastAnimationPolicy { get; private set; }
    public Action<short, short, short>? VoicePlayback { get; set; }
    public Action? MenuExit { get; set; }
    public PlayerCutsceneSkipState SkipState { get; } = new();
    public GodotPlayerCutsceneSkip? CutsceneSkip { get; internal set; }

    public void Start(PlayerScriptProgram program)
    {
        SkipState.BeginScript();
        if (Runtime is null) Runtime = new(program, this);
        else Runtime.ReplaceProgram(program);
        CurrentProgram = program;
        ProgramsStarted++;
        player.Combat.State.SynthesizedInputActive = true;
        player.Controller.SetScriptVelocity(default);
        LastInput = null;
    }

    public PlayerControlFrame ReadControls()
    {
        if (Runtime is null || !Runtime.Active) throw new InvalidOperationException("Player input script is not active.");
        PlayerScriptEnvironment source = environment();
        if (CutsceneSkip is { } skip) source = skip.ScriptEnvironment(source);
        LastInput = Runtime.Update(2, source, player.PhysicalPad);
        player.Combat.State.SynthesizedInputActive = Runtime.Active;
        return PlayerScriptControlMapper.Map(LastInput);
    }

    public void PreparePlayerAi() => CutsceneSkip?.PreparePlayerAi();

    public FixedVector3 Position
    {
        get => player.Controller.Position;
        set => player.Controller.SetScriptPosition(value);
    }
    public FixedVector3 Velocity
    {
        get => player.Controller.Velocity;
        set => player.Controller.SetScriptVelocity(value);
    }
    public short CameraHeading => player.CameraHeading?.Invoke() ?? player.Controller.Orientation.Heading;
    public int AnimationId => player.Model.ClipId;
    public bool AnimationFinished => player.Model.Playback.IsFinished;
    public bool IsCrawling => player.Controller.IsCrawling;

    public FixedVector3 TriggerPosition(int nodeId)
    {
        if (nodeId < 0 || nodeId >= triggers.Nodes.Count || triggers.Nodes[nodeId].Id != nodeId)
            throw new InvalidDataException($"Player script references missing node {nodeId}.");
        TriggerPosition position = TriggerPlacement.Read(triggers.Nodes[nodeId]).Position;
        return new FixedVector3(position.X, position.Y, position.Z) << 12;
    }
    public void ResetIdleTimer() => IdleTimer = 0;
    public void TeleportToNode(int nodeId) => player.Controller.TeleportFromScript(TriggerPosition(nodeId));
    public void PlayVoice(short bank, short track, short priority)
    {
        if (VoicePlayback is null) throw new NotSupportedException($"Script voice {bank}/{track} requires the audio adapter.");
        VoicePlayback(bank, track, priority);
    }
    public void PlayAnimation(int animationId, ScriptAnimationPolicy policy)
    {
        LastAnimationPolicy = policy;
        player.Animation.RunWithSoundPolicy(animationId, policy);
    }
    public void Stand() => player.Controller.Stand();
    public void EnterDelayedStandState() => player.Controller.EnterScriptedDelay();
    public void SetScriptFlag(bool enabled) => ScriptFlag = enabled;
    public void RequestMenuExit()
    {
        if (MenuExit is null) throw new NotSupportedException("Scripted menu exit requires the session navigation adapter.");
        MenuExit();
    }
}
