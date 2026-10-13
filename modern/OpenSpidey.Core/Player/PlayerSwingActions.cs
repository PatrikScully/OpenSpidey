using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Player-owned swing fields; target availability and melee/combat remain their existing shared owners.</summary>
public sealed class PlayerSwingActionState(PlayerMeleeState melee, PlayerSwingWebAvailabilityState availability,
    PlayerLookaroundTargetState targets)
{
    public PlayerMeleeState Melee { get; } = melee;
    public PlayerSwingWebAvailabilityState Availability { get; } = availability;
    public PlayerLookaroundTargetState Targets { get; } = targets;
    public PlayerSurfaceTransitionState Surface { get; } = new(melee);
    public WebSwingAnchor? Current { get; set; }
    public SourceMatrix Basis { get; set; }
    public int VerticalAlignment { get; set; }
    public int Length { get; set; }
    /// <summary>mLineInfo's item and normal are separate: the completion probe updates only its normal.</summary>
    public PlayerWorldHit? Contact { get; set; }
    public FixedVector3 ContactNormal { get => Surface.LineNormal; set => Surface.LineNormal = value; }
}

public interface IPlayerSwingActionHost
{
    uint Clock { get; }
    byte FireVariant { get; }
    int PracticeDifficulty { get; }
    int AnimationFrameCount { get; }
    int? SupportVerticalVelocity { get; }
    bool CheckStickToCeiling();
    bool CheckJumpingSmashKick();
    bool CheckJumpingZipWeb();
    bool ShouldDropFlail();
    void SetJumpImpulse(PlayerJumpImpulse impulse);
    void SetForcedAirControl(sbyte sourceUpDown, sbyte sourceLeftRight, int ramp);
    void SetPendingWebReleaseAnimation(byte value);
    void SetLandingAnimationLatch(byte value);
    void ClearAirborneReleaseLatch();
    void SetOrientationReference(FixedVector3 direction);
    void SetFenceSurfaceDetected();
    bool CheckFenceSurfaceTransition();
    void TidyLandingPosition(int distance);
    void SetCameraFocusHook(int hook);
    void PutCameraBehind(int duration);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void LockTargetTorsoAngle();
    void PlayAnimation(ushort animation);
    void SetAnimationFrame(short frame);
    void SetAnimationSpeed(int speed);
    bool DecreaseWebbing(int amount);
    WebSwingAnchor CreateSwing(FixedVector3 anchor, int length, SourceMatrix basis, FixedVector3 surfaceNormal);
    void ReleaseSwing(WebSwingAnchor swing);
    void DeleteSwing(WebSwingAnchor swing);
    PlayerWorldHit? TraceWorld(FixedVector3 start, FixedVector3 end);
    FixedVector3 GetPlayerHookPosition(int hook);
    int Random(int bound);
    void PlaySound(ushort sound, int volume, int pitch);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
}

/// <summary>Original 100/200/400 windup, release and swinging branches, including partial collision writes.</summary>
public static class PlayerSwingActions
{
    public static void UpdateWindup(PlayerSwingActionState state, IPlayerSwingActionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0; combat.Field54C = 1;
        if (combat.AnimationFrame < 13) return;
        host.SetJumpImpulse(new(unchecked((host.SupportVerticalVelocity ?? 0) - 0x3C000), 0x70000, 0xA8000));
        combat.ActionState = 0x200; combat.ActionProgress = 0;
        host.PlayPositionSound(9, combat.Position, 0);
        combat.CrawlSurfaceNormal = new(0, -4096, 0);
        FixedVector3 facing = state.Targets.SwingFacingTarget;
        if ((facing.X | facing.Y | facing.Z) != 0)
        {
            FixedVector3 delta = (combat.Position - facing) >> 12;
            host.OrientToNormal(true, FixedVectorMath.Normalize(delta with { Y = 0 }));
            combat.Field8E8 = 0;
        }
        else host.OrientToNormal(false, default);
    }

    public static void UpdateRelease(PlayerSwingActionState state, IPlayerSwingActionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (combat.ActionProgress == 0)
        {
            host.SetCameraFocusHook(2); host.PutCameraBehind(16); combat.ActionProgress++;
        }
        if ((combat.LastHitAction & 0x200) != 0 && unchecked(host.Clock - combat.LastHitClock) < 6)
        {
            if (host.ShouldDropFlail())
            {
                host.PlayAnimation(0xAF); combat.Velocity = combat.Velocity with { X = 0, Z = 0 };
                combat.ActionState = 0x800000; combat.FlailDropChain = 0;
            }
            else { host.PlayAnimation(0xD8); combat.ActionState = 4; }
            host.SetPendingWebReleaseAnimation(0);
        }
        if (host.CheckStickToCeiling()) return;
        if (combat.Velocity.Y < 0) { host.SetForcedAirControl(-127, 0, 16); return; }
        combat.ActionState = 0x400; host.PlayAnimation(0x113);
        host.PlayPositionSound(0x19, combat.Position, 0); host.DecreaseWebbing(0x80);
        // The source diagnostic is nonfatal: this branch overwrites E64 even when an old owner remains.
        Create(state, state.Availability.NearCentre, host);
        host.PlaySound(unchecked((ushort)(host.Random(3) + 0x15)), 0x2000, 0);
    }

    public static void UpdateSwinging(PlayerSwingActionState state, IPlayerSwingActionHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        combat.FieldEA6 = 0;
        if (host.CheckJumpingSmashKick() || host.CheckJumpingZipWeb()) return;
        bool endSwing = false;
        if (state.Current is { } swing)
        {
            if (swing.IsComplete)
            {
                if (state.Availability.FarFromWall != 0)
                {
                    host.PlayAnimation(0x118); combat.Position -= combat.ForwardDirection * 192;
                    host.DecreaseWebbing(0x80); Release(state, host);
                    Create(state, state.Availability.FarCentre, host);
                    host.PlaySound(unchecked((ushort)(host.Random(3) + 0x15)), 0x2000, 0);
                    host.SetAnimationFrame(state.Current!.AnimationFrame(unchecked(host.AnimationFrameCount - 1)));
                    state.Availability.FarFromWall = 0; host.PlayPositionSound(0x19, combat.Position, 0);
                }
                else endSwing = true;
            }
            else
            {
                host.SetAnimationSpeed(0);
                host.SetAnimationFrame(swing.AnimationFrame(unchecked(host.AnimationFrameCount - 1)));
            }
        }
        if (endSwing && (combat.CollisionFlags & 3) == 0)
        {
            FixedVector3 start = host.GetPlayerHookPosition(2);
            if (host.TraceWorld(start, start - (combat.ForwardDirection << 8)) is { } hit)
            {
                // The original leaves mLineInfo.pItem unchanged in this branch.
                state.ContactNormal = hit.Contact.Normal;
                combat.CollisionFlags |= 1;
                combat.Position = hit.Contact.Position + hit.Contact.Normal * PlayerTerrainController.SurfaceClearance;
            }
        }
        if ((combat.CollisionFlags & 3) != 0)
        {
            combat.Velocity = default; host.PlayPositionSound(9, combat.Position, 0); combat.FieldAE5 = 0;
            if (state.Contact is { } contact)
            {
                FixedVector3 normal = state.ContactNormal;
                int normalY = unchecked((short)normal.Y);
                uint faceFlags = contact.Contact.SurfaceFlags;
                bool land = false;
                if (normalY < -2600)
                {
                    combat.IsCrawling = false; Release(state, host); combat.CrawlSurfaceNormal = normal;
                    combat.Field54C = 0; host.SetLandingAnimationLatch(1); host.PlayAnimation(0xD5);
                    combat.ActionState = 1; host.SetCameraFocusHook(-1); return;
                }
                if (normalY > 3400)
                {
                    if ((faceFlags & 0x40000) != 0) endSwing = true;
                    else
                    {
                        combat.IsCrawling = true; Release(state, host); combat.CrawlSurfaceNormal = normal;
                        host.SetOrientationReference(combat.ForwardDirection);
                        host.OrientToNormal(true, combat.ForwardDirection); host.LockTargetTorsoAngle(); land = true;
                    }
                }
                else if ((faceFlags & 0x40000) == 0)
                {
                    combat.IsCrawling = true; Release(state, host); combat.CrawlSurfaceNormal = normal;
                    host.OrientToNormal(true, new(0, 4096, 0)); land = true;
                }
                else if ((faceFlags & 0x8000000) != 0)
                {
                    combat.CrawlSurfaceNormal = normal;
                    FixedVector3 position = combat.Position;
                    for (;;)
                    {
                        FixedVector3 up = new(0, 0x4000, 0);
                        PlayerWorldHit? fence = host.TraceWorld(position + normal * 40 + up, position - normal * 140 + up);
                        if (fence is not { } next || (next.Contact.SurfaceFlags & 0x8000000) == 0) break;
                        position = next.Contact.Position + next.Contact.Normal * PlayerTerrainController.SurfaceClearance;
                    }
                    combat.IsCrawling = true; host.SetFenceSurfaceDetected(); combat.Field54C = 0;
                    combat.Position = position; host.SetLandingAnimationLatch(1); combat.Field8E8 = 1;
                    Release(state, host); host.SetCameraFocusHook(-1);
                    host.CheckFenceSurfaceTransition();
                    return;
                }
                else endSwing = true;
                if (land)
                {
                    host.TidyLandingPosition(32); host.SetCameraFocusHook(-1); combat.Field54C = 0;
                    host.SetLandingAnimationLatch(1); host.PlayAnimation(0x119); combat.ActionState = 1; return;
                }
            }
        }
        if ((combat.LastHitAction & 0x400) != 0 && unchecked(host.Clock - combat.LastHitClock) < 6)
        {
            host.PlayAnimation(0xD8); combat.ActionState = 4; host.SetPendingWebReleaseAnimation(0); endSwing = true;
        }
        bool jump = melee.Input.Triggered[16] != 0;
        if (jump) host.ClearAirborneReleaseLatch();
        if (!endSwing && !((host.PracticeDifficulty == 0 || combat.SynthesizedInputActive) && jump))
        {
            if (host.PracticeDifficulty == 0) return;
            if (melee.Input.Triggered[17] == 0 && melee.Input.Triggered[18] == 0 && melee.Input.Triggered[19] == 0) return;
        }
        combat.Velocity = combat.Velocity with { X = combat.Velocity.X >> 1, Z = combat.Velocity.Z >> 1 };
        host.SetAnimationSpeed(0x10000);
        if (combat.ActionState != 4) { combat.ActionState = 4; host.SetPendingWebReleaseAnimation(1); }
        host.SetCameraFocusHook(-1); combat.Field54C = 0; host.SetLandingAnimationLatch(1); Release(state, host);
    }

    private static void Create(PlayerSwingActionState state, FixedVector3 anchor, IPlayerSwingActionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        state.Length = FixedVectorMath.OriginalDistance(combat.Position, anchor);
        PlayerSwingBasisResult basis = PlayerSwingBasis.Calculate(combat.Position, anchor, state.Availability.PlaneNormal, state.Basis);
        state.Basis = basis.Matrix; state.VerticalAlignment = basis.VerticalAlignment;
        state.Current = host.CreateSwing(anchor, state.Length, state.Basis, state.Availability.PlaneNormal);
        state.Current.FireVariant = host.FireVariant;
    }
    private static void Release(PlayerSwingActionState state, IPlayerSwingActionHost host)
    {
        if (state.Current is not { } swing) return;
        host.ReleaseSwing(swing); host.DeleteSwing(swing); state.Current = null;
    }
}
