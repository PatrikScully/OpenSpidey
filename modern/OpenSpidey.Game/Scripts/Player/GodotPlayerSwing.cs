using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

public sealed class GodotPlayerSwingConfiguration
{
    public required Func<int> PracticeDifficulty { get; init; }
    /// <summary>True only while the shared source G_LOW_MEMORY DWORD is zero (the original SwingBack guard).</summary>
    public required Func<bool> ReleaseVisualEnabled { get; init; }
    public required Func<bool> CheckJumpingSmashKick { get; init; }
    public required Func<bool> CheckJumpingZipWeb { get; init; }
    public required Action<PlayerJumpImpulse> SetJumpImpulse { get; init; }
    public required Action ClearJumpDurations { get; init; }
    public required Action<sbyte, sbyte, int> SetForcedAirControl { get; init; }
    public required Action<byte> SetPendingWebReleaseAnimation { get; init; }
    public required Action<FixedVector3> SetOrientationReference { get; init; }
    public required Func<ushort, int> AnimationFrameCount { get; init; }
    public required Action<FixedVector3, uint> SetTripodMotion { get; init; }
    public required Action<int> SetFloorCamera { get; init; }
}

/// <summary>Real source100/200/400 player-owned swing, using the posed hook cache and shared protected web bits.</summary>
public sealed partial class GodotPlayerSwing : IPlayerSwingActionHost, IPlayerCeilingAttachmentHost, IPlayerSurfaceTransitionHost, IPlayerSwingFrameHost, IDisposable
{
    private readonly GodotPlayer player;
    private readonly GodotPlayerFireWeb fire;
    private readonly GodotWebStrands strands;
    private readonly PlayerFollowCamera camera;
    private readonly GodotActorWorldQuery world;
    private readonly GodotPlayerSwingConfiguration configuration;
    private readonly SwingWorldQuery physicsWorld;
    private bool disposed;
    private GodotPlayerMelee Melee => player.Melee ?? throw new InvalidOperationException("Swinging requires the player's original posed melee owner.");
    public PlayerSwingActionState State { get; }
    public bool SwingPresent => State.Current is not null;
    public uint Clock => fire.Clock;
    public byte FireVariant => fire.FireWebbing;
    public int PracticeDifficulty => configuration.PracticeDifficulty();
    public int AnimationFrameCount => player.Model.FrameCount;
    public int? SupportVerticalVelocity => player.Controller.SupportBody?.Velocity.Y;
    public FixedVector3 RightDirection => player.Controller.Orientation.Right;

    public GodotPlayerSwing(GodotPlayer player, GodotPlayerFireWeb fire, GodotWebStrands strands,
        PlayerFollowCamera camera, GodotActorWorldQuery world, PlayerSwingWebAvailabilityState availability,
        PlayerLookaroundTargetState targets, GodotPlayerSwingConfiguration configuration)
    {
        this.player = player; this.fire = fire; this.strands = strands; this.camera = camera; this.world = world;
        this.configuration = configuration;
        if (!ReferenceEquals(targets.Melee, Melee.State))
            throw new ArgumentException("Swing and lookaround must use the same source player fields.", nameof(targets));
        State = new(Melee.State, availability, targets); physicsWorld = new(world);
    }
    public void UpdateWindup() => PlayerSwingActions.UpdateWindup(State, this);
    public void UpdateRelease() => PlayerSwingActions.UpdateRelease(State, this);
    public void UpdateSwinging() => PlayerSwingActions.UpdateSwinging(State, this);
    public void UpdateSurfaceTransition(ushort completedAnimation) => PlayerSurfaceTransitions.Update(State.Surface,
        completedAnimation, PlayerTerrainController.SurfaceClearance, this);

    /// <summary>Source updateSwingLine runs after regeneration and before player physics, without EveryFrame or a world actor list.</summary>
    public void PreparePlayerFrame()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (State.Current is not { } swing) return;
        if ((Melee.State.Combat.ActionState & 0x400) != 0) swing.Advance(Clock, fire.CameraPositionWholeUnits);
        else ReleaseCurrent(createVisual: false);
    }

    /// <summary>Returns null only when no source E64 exists. Its posed MATRIX cache remains unchanged across the two hook2 queries.</summary>
    public PlayerSwingPhysicsResult? StepPhysics()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (State.Current is not { } swing) return null;
        PlayerCombatState combat = Melee.State.Combat;
        FixedVector3 previous = combat.Position, before = Melee.GetPlayerHookPosition(2);
        combat.Position = swing.CurrentPosition;
        FixedVector3 after = Melee.GetPlayerHookPosition(2);
        PlayerSwingPhysicsResult result = PlayerSwingPhysics.Step(new(previous, combat.Position, before, after,
            PlayerTerrainController.SurfaceClearance), physicsWorld);
        combat.Position = result.Position; combat.Velocity = result.Velocity; combat.CollisionFlags = result.CollisionFlags;
        State.Contact = result.Contact;
        if (result.Contact is { } contact) State.ContactNormal = contact.Contact.Normal;
        return result;
    }

    /// <summary>Source attachments mutate the selected posed hand before held-object placement and current-CWeb SetFirePos.</summary>
    public void FinishPlayerHands(ref PlayerHandHooks hooks)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (State.Current is not { } swing) return;
        FixedVector3 end;
        if (Melee.State.Combat.AnimationId == 0x118) { hooks.Left -= swing.Forward * 8; end = hooks.Left; }
        else { hooks.Right -= swing.Forward * 8; end = hooks.Right; }
        swing.SetRenderEnd(end, fire.CameraPositionWholeUnits);
    }

    /// <summary>Guard entry chooses true for SwingBack + deletion, false for source direct deletion.</summary>
    public void ReleaseCurrent(bool createVisual)
    {
        if (State.Current is not { } swing) return;
        if (createVisual) ReleaseSwing(swing);
        DeleteSwing(swing); State.Current = null;
    }
    public bool CheckStickToCeiling() => PlayerSurfaceTransitions.CheckCeiling(Melee.State,
        player.Controller.LastTerrainStep.CeilingHit, PlayerTerrainController.SurfaceClearance, this);
    public bool CheckJumpingSmashKick() => configuration.CheckJumpingSmashKick();
    public bool CheckJumpingZipWeb() => configuration.CheckJumpingZipWeb();
    public bool ShouldDropFlail() => world.GetGroundHeight(Melee.State.Combat.Position, 0, 4096, out _) != -1;
    public void SetJumpImpulse(PlayerJumpImpulse impulse) => configuration.SetJumpImpulse(impulse);
    public void ClearJumpDurations() => configuration.ClearJumpDurations();
    public void SetForcedAirControl(sbyte upDown, sbyte leftRight, int ramp) => configuration.SetForcedAirControl(upDown, leftRight, ramp);
    public void SetPendingWebReleaseAnimation(byte value) => configuration.SetPendingWebReleaseAnimation(value);
    public void SetLandingAnimationLatch(byte value) => State.Targets.Transition.Angles.SwingHeldLatch = value;
    public void ClearAirborneReleaseLatch() => State.Targets.Transition.Angles.JumpReleased = 0;
    public void SetOrientationReference(FixedVector3 direction) => configuration.SetOrientationReference(direction);
    public void SetFenceSurfaceDetected() => State.Surface.FenceDetected = 1;
    public bool CheckFenceSurfaceTransition() => PlayerSurfaceTransitions.CheckFence(State.Surface, this);
    public void TidyLandingPosition(int distance) => Melee.State.Combat.Position = PlayerZipWebRules.TidyLanding(
        Melee.State.Combat.Position, player.Controller.Orientation.Right, player.Controller.Orientation.Forward, distance, physicsWorld);
    public void SetCameraFocusHook(int hook) => camera.SetFocusHook(hook);
    public void PutCameraBehind(int duration) => camera.PutBehind(duration);
    public void OrientToNormal(bool useTarget, FixedVector3 target) => Melee.OrientToNormal(useTarget, target);
    public void LockTargetTorsoAngle() => Melee.LockTargetTorsoAngle();
    public void PlayAnimation(ushort animation) => Melee.PlaySingleAnimation(animation, 0, -1);
    public void RunAnimation(ushort animation) => Melee.RunAnimation(animation, 0, -1);
    public void SelectAndResetAnimationSounds(ushort animation) => Melee.SelectAndResetAnimationSounds(animation);
    int IPlayerSurfaceTransitionHost.AnimationFrameCount(ushort animation) => configuration.AnimationFrameCount(animation);
    public void ApplyPlayerPose() => Melee.ApplyPlayerPose();
    public void SetTripodMotion(FixedVector3 position, uint duration) => configuration.SetTripodMotion(position, duration);
    public void SetFloorCamera(int duration) => configuration.SetFloorCamera(duration);
    public void SwitchToStandMode() => Melee.SwitchToStandMode();
    public int GroundHeight(FixedVector3 position, int above, int below) => world.GetGroundHeight(position, above, below, out _);
    public void SetAnimationFrame(short frame)
    { Melee.State.Combat.AnimationFrame = frame; player.Model.SetSourceAnimationFrame(frame); }
    public void SetAnimationSpeed(int speed) => player.Model.PlaybackSpeed = speed;
    public bool DecreaseWebbing(int amount) => fire.DecreaseWebbing(amount);
    public WebSwingAnchor CreateSwing(FixedVector3 anchor, int length, SourceMatrix basis, FixedVector3 normal)
    {
        var swing = new WebSwingAnchor(anchor, length, basis, normal, Clock, fire.CameraPositionWholeUnits, fire.Random);
        try { strands.AttachStrand(swing.Strand); return swing; }
        catch { swing.Dispose(); throw; }
    }
    public void ReleaseSwing(WebSwingAnchor swing)
    {
        if (configuration.ReleaseVisualEnabled()) strands.EmitReleasedSwing(swing, () => Melee.State.Combat.Position,
            () => fire.CameraPositionWholeUnits);
    }
    public void DeleteSwing(WebSwingAnchor swing) => swing.Dispose();
    public PlayerWorldHit? TraceWorld(FixedVector3 start, FixedVector3 end) => physicsWorld.Trace(start, end);
    public FixedVector3 GetPlayerHookPosition(int hook) => Melee.GetPlayerHookPosition(hook);
    public int Random(int bound) => fire.Random(bound);
    public void PlaySound(ushort sound, int volume, int pitch) => Melee.PlaySound(sound, volume, pitch);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Melee.PlayPositionSound(sound, position, pitch);
    public void Dispose()
    {
        if (disposed) return;
        ReleaseCurrent(createVisual: false); disposed = true;
    }

    private sealed class SwingWorldQuery(GodotActorWorldQuery world) : IPlayerWorldQuery
    {
        public PlayerWorldHit? Trace(FixedVector3 start, FixedVector3 end)
        {
            if (world.Trace(start, end, includeEnvironmentalActors: true) is not { } hit) return null;
            var normal = GodotImpactWebNativeHost.Normal(hit);
            bool supports = hit.Collider is ImportedCollisionBody body && (body.SourceFlags & 0x100) != 0;
            return new(new(hit.Position, new(normal.X, normal.Y, normal.Z), FixedVectorMath.OriginalDistance(start, hit.Position),
                hit.Triangle.PackedMetadata, supports), hit.InstanceIndex, hit.ModelIndex, hit.Triangle.FaceIndex,
                hit.SourceAsset, hit.ModelChecksum, hit.SupportId);
        }
    }
}
