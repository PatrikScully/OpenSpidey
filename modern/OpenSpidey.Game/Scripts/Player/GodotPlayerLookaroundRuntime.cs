using OpenSpidey.Core.Player;
using OpenSpidey.Game.UI;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerLookaroundConfiguration
{
    public required Func<bool> PracticeMode { get; init; }
    public required Func<bool> InvertPitch { get; init; }
    public required Func<bool> SwingWebPresent { get; init; }
    public required Action<PlayerWorldHit> BeginZip { get; init; }
    public required Action<PlayerSwingWebAvailabilityState> BeginSwing { get; init; }
}

/// <summary>One source lookaround owner from post-analogue controls to final camera/target selection, sharing the player's real web state.</summary>
public sealed class GodotPlayerLookaroundRuntime : IPlayerLookaroundFrameHost
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerLookaroundConfiguration configuration;
    private readonly PlayerFollowCamera followCamera;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Lookaround frames require the genuine shared melee player.");
    public GodotPlayerLookaroundAim Aim { get; }
    public GodotPlayerLookaroundCamera Camera { get; }
    public GodotPlayerLookaroundTargets Targets { get; }
    public GodotScreenTarget ScreenTarget { get; }

    public GodotPlayerLookaroundRuntime(GodotPlayer player, GodotPlayerWebRuntime webs, GodotPlayerCombatRuntime combat,
        PlayerFollowCamera camera, GodotScreenTarget screenTarget, GodotPlayerLookaroundConfiguration configuration)
    {
        this.player = player; followCamera = camera; ScreenTarget = screenTarget; this.configuration = configuration;
        ArgumentNullException.ThrowIfNull(configuration.PracticeMode); ArgumentNullException.ThrowIfNull(configuration.InvertPitch);
        ArgumentNullException.ThrowIfNull(configuration.SwingWebPresent); ArgumentNullException.ThrowIfNull(configuration.BeginZip); ArgumentNullException.ThrowIfNull(configuration.BeginSwing);
        Aim = webs.Lookaround ?? throw new InvalidOperationException("Lookaround requires the player's single genuine web aim owner.");
        Camera = new(player, Aim, camera, combat.World, screenTarget.SetTargetVisible);
        Targets = new(player, webs.Actions, Camera, combat.Directory, combat.Session, combat.World, screenTarget, new()
        { Clock = () => webs.Actions.Clock, BeginZip = configuration.BeginZip, BeginSwing = configuration.BeginSwing });
    }

    public PlayerButtonInput ReadControls(PlayerMovementInputState movement, PlayerButtonInput buttons, byte groundContactGraceUpdates)
    {
        PlayerMeleeInput input = Melee.State.Input;
        input.Pressed[4] = buttons.IsHeld(PlayerButton.Target) ? (byte)1 : (byte)0;
        input.Pressed[7] = buttons.IsHeld(PlayerButton.Swing) ? (byte)1 : (byte)0;
        if (buttons.WasTriggered(PlayerButton.Swing)) input.Triggered[7] = input.Mirrors[7] = 1;
        PlayerLookaroundButtons returned = Aim.UpdateInput(new(new(input.Pressed[4], input.Pressed[7], input.Triggered[7],
            input.Pressed[16], input.Triggered[16]), movement.MovementRamp, groundContactGraceUpdates,
            followCamera.SourceControlMode, configuration.PracticeMode(), configuration.InvertPitch(), configuration.SwingWebPresent()), Camera);
        input.Pressed[4] = returned.LookHeld;
        input.Triggered[7] = returned.SwingTriggered; input.Triggered[16] = returned.JumpTriggered;
        PlayerButton held = returned.LookHeld == 0 ? buttons.Held & ~PlayerButton.Target : buttons.Held;
        PlayerButton triggered = buttons.Triggered;
        if (returned.SwingTriggered == 0) triggered &= ~PlayerButton.Swing;
        if (returned.JumpTriggered == 0) triggered &= ~PlayerButton.Jump;
        return new(held, triggered);
    }
    public void FinishFrame(int elapsedTicks)
    {
        PlayerLookaroundCameraSetupResult setup = Camera.UpdateCameraPlacement(elapsedTicks);
        Targets.Update(setup);
    }
}
