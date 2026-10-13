using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerLandingRecoveryConfiguration
{
    public required Func<bool> CheckJumpingSwingWeb { get; init; }
    public required Func<bool> CheckJumpingR1ZipWeb { get; init; }
    public required Func<bool> CheckJumpingR2ZipWeb { get; init; }
    public required Func<int, int> Random { get; init; }
}

/// <summary>Actual action8/64 over one player impulse, source lookaround release byte and genuine airborne guard callbacks.</summary>
public sealed class GodotPlayerLandingRecovery : IPlayerLandingRecoveryFrameHost, IPlayerLandingRecoveryHost, IPlayerForwardSelectionHost
{
    private readonly GodotPlayer player;
    private readonly PlayerFollowCamera camera;
    private readonly GodotPlayerLandingRecoveryConfiguration configuration;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Landing recovery requires the genuine posed melee player.");
    public PlayerLandingRecoveryState State { get; }
    public int? SupportVerticalVelocity => player.Controller.SupportBody?.Velocity.Y;

    public GodotPlayerLandingRecovery(GodotPlayer player, PlayerFollowCamera camera, GodotPlayerLookaroundAim lookaround,
        GodotPlayerLandingRecoveryConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(player); ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(lookaround);
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(configuration.CheckJumpingSwingWeb);
        ArgumentNullException.ThrowIfNull(configuration.CheckJumpingR1ZipWeb); ArgumentNullException.ThrowIfNull(configuration.CheckJumpingR2ZipWeb);
        ArgumentNullException.ThrowIfNull(configuration.Random);
        this.player = player; this.camera = camera; this.configuration = configuration;
        State = new(Melee.State, lookaround.State);
    }

    public void UpdateRecovery(ushort completedAnimation)
    {
        CaptureImpulse(); PlayerJumpImpulse previous = State.Jump; int previousRamp = State.SpeedRamp;
        PlayerLandingRecoveryBehavior.Step(State, completedAnimation, this);
        ApplyImpulseChanges(previous, previousRamp);
    }
    public void UpdateWindup()
    {
        CaptureImpulse(); PlayerJumpImpulse previous = State.Jump; int previousRamp = State.SpeedRamp;
        PlayerLandingRecoveryBehavior.StepWindup(State, player.Model.Playback.IsFinished, this);
        ApplyImpulseChanges(previous, previousRamp);
    }
    private void CaptureImpulse()
    { State.Jump = player.Controller.SourceJumpImpulse; State.SpeedRamp = player.Controller.SpeedRamp; }
    private void ApplyImpulseChanges(PlayerJumpImpulse previous, int previousRamp)
    {
        // A successful earlier guard can mutate the actual controller impulse; only continuation-owned changes are written back.
        if (State.Jump != previous) player.Controller.SetSwingJumpImpulse(State.Jump);
        if (State.SpeedRamp != previousRamp) player.Controller.SetSourceSpeedRamp(State.SpeedRamp);
    }
    public bool CheckGroundGone() => Melee.CheckGroundGone();
    public bool CheckForwards(bool allowStart)
    {
        PlayerTerrainController controller = player.Controller;
        return PlayerForwardSelection.Check(Melee.State.Combat, new(controller.CurrentButtons.IsHeld(PlayerButton.Target),
            controller.MovementInput.Axes != default, allowStart, unchecked((ushort)controller.Orientation.GetEffectiveHeading(controller.OnWall)),
            controller.MovementInput.Heading, camera.SourceHeading, controller.HeadingTurn.Target, controller.HardSurface), this);
    }
    public bool CheckJump() => Melee.CheckJump();
    public bool CheckKick() => Melee.CheckKick();
    public bool CheckJumpingSwingWeb() => configuration.CheckJumpingSwingWeb();
    public bool CheckJumpingR1ZipWeb() => configuration.CheckJumpingR1ZipWeb();
    public bool CheckJumpingR2ZipWeb() => configuration.CheckJumpingR2ZipWeb();
    public int Random(int range) => configuration.Random(range);
    public void PlaySingleAnimation(ushort animation) => Melee.PlaySingleAnimation(animation, 0, -1);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public void SetTargetTorsoAngle(short wanted, bool fast) => Melee.SetTargetTorsoAngle(wanted, fast);
    public void SelectAndRunAnimation(ushort animation)
    { Melee.SelectAndResetAnimationSounds(animation); Melee.RunAnimation(animation, 0, -1); }
}
