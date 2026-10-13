using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;

namespace OpenSpidey.Game.Player;

public partial class GodotPlayerWebRuntime
{
    public GodotPlayerSwing? Swing { get; private set; }
    public GodotPlayerAirborneAttackSelection? Airborne { get; private set; }
    public GodotPlayerSmashAttack? Smash { get; private set; }
    public GodotPlayerLandingRecovery? LandingRecovery { get; private set; }
    public GodotPlayerWalking? Walking { get; private set; }
    public GodotPlayerAirborneMovement? AirborneMovement { get; private set; }
    public GodotSmokeTrails? SmokeTrails { get; private set; }
    public int PracticeDifficulty { get; set; } // Original practice global is zero in normal level startup.

    public void ConfigureSwing(PlayerFollowCamera camera, GodotPlayerCombatRuntime combat, GodotPlayerLookaroundTargets targets)
    {
        if (Swing is not null) throw new InvalidOperationException("The player already owns its direct swing controller.");
        if (Lookaround is null) throw new InvalidOperationException("Swinging requires the existing shared lookaround owner.");
        SmokeTrails = GodotSmokeTrails.Create(originalBits);
        AddChild(SmokeTrails);
        SmokeTrails.OwnerCreated += bit => combat.Particles.Attach(SourceParticleList.NonRendered, bit);
        SmokeTrails.SegmentCreated += bit => combat.Particles.Attach(SourceParticleList.Linked2EndedLeftover, bit);
        SmokeTrails.BitRemoved += identity => combat.Particles.Detach(identity);
        Airborne = new(player, Actions, Lookaround, camera, combat.World, combat.Session, new()
        {
            SwingPresent = () => Swing?.SwingPresent == true,
            ReleaseSwing = visual => Swing!.ReleaseCurrent(visual),
            CreateSmashAttackTrails = () => Smash!.CreateSmashAttackTrails(),
            BeginZip = player.Controller.BeginLookaroundZip
        });
        Smash = new(player, Airborne.Smash, combat.Directory, combat.Session, SmokeTrails, () => Actions.Clock);
        Swing = new(player, Actions, Strands, camera, combat.World, targets.Swing, targets.State, new()
        {
            PracticeDifficulty = () => PracticeDifficulty,
            ReleaseVisualEnabled = () => !combat.ItemShatter.LowMemory,
            CheckJumpingSmashKick = Airborne.CheckJumpingSmashKick,
            CheckJumpingZipWeb = Airborne.CheckJumpingR1ZipWeb,
            SetJumpImpulse = player.Controller.SetSwingJumpImpulse,
            ClearJumpDurations = player.Controller.ClearJumpDurations,
            SetForcedAirControl = (upDown, leftRight, ramp) =>
            {
                player.Controller.SetForcedAirControl(upDown, leftRight, ramp);
                player.Melee!.State.Input.Sideways = upDown;
                player.Melee.State.Input.Forward = leftRight;
            },
            SetPendingWebReleaseAnimation = value => player.Controller.PendingWebReleaseAnimation = value,
            SetOrientationReference = value => player.Controller.OrientationReference = value,
            AnimationFrameCount = animation => player.Model.ReadFrameCount(animation),
            SetTripodMotion = camera.SetTripodMotion,
            SetFloorCamera = camera.SetFloorCamera
        });
        Swing.ConfigureSelection(Airborne, combat.Directory);
        LandingRecovery = new(player, camera, Lookaround, new()
        {
            CheckJumpingSwingWeb = Swing.CheckJumpingSwingWeb,
            CheckJumpingR1ZipWeb = Airborne.CheckJumpingR1ZipWeb,
            CheckJumpingR2ZipWeb = Swing.CheckJumpingR2ZipWeb,
            Random = Swing.Random
        });
        player.Controller.Swing = Swing;
        player.Controller.Smash = Smash;
        player.Controller.LandingRecovery = LandingRecovery;
        Walking = new(player, camera, this, combat.Session);
        player.Controller.Walking = Walking;
        AirborneMovement = new(player, Swing, Airborne, combat.World, new()
        {
            Difficulty = () => combat.Actors.Registry.Difficulty,
            LevelId = () => combat.Actors.Registry.Environment.LevelId
        });
        player.Controller.AirborneMovement = AirborneMovement;
        player.Controller.CameraFacing = Swing.CameraFacing;
        player.Combat.SmokeFade = Smash.FadeSmokeTrail;
        player.Melee!.BeforeHeldObjectPlacement += FinishSwingAttachments;
    }

    private void FinishSwingAttachments(ref PlayerHandHooks hands)
    {
        if (PlayerEffectPhasesEnabled)
        {
            AdditionalEffects!.FinishFrame();
            FlamingFists!.FinishFrame(hands);
        }
        Swing!.FinishPlayerHands(ref hands);
        Airborne!.UpdateCurrentWeb(hands.Left, hands.Right);
        if (!PlayerEffectPhasesEnabled) Smash!.UpdateTrails();
    }
}
