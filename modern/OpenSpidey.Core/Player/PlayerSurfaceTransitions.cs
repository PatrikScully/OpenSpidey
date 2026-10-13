using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Source surface transition scratch fields and the same mLineInfo normal used by swinging.</summary>
public sealed class PlayerSurfaceTransitionState(PlayerMeleeState melee)
{
    public PlayerMeleeState Melee { get; } = melee;
    public FixedVector3 LineNormal { get; set; }
    public byte FenceDetected { get; set; } // B09.
    public byte ApplyHookPosition { get; set; } = 1; // AD6; player constructor enables automatic camera steering.
    public byte WallToCeilingMotion { get; set; } // AD8.
    public byte CeilingToWallMotion { get; set; } // AD9.
    public byte WallMotionRequested { get; set; } // ADA.
    public byte CeilingMotionRequested { get; set; } // ADC.
    public byte CeilingToWallInputRequested { get; set; } // ADB; distinct from exterior transition's ADC.
    public byte ExteriorCornerDetected { get; set; } // B08.
    public int InteriorSlideRemaining { get; set; } // C58.
    public int ForwardSlideRemaining { get; set; } // C54.
}

public interface IPlayerCeilingAttachmentHost
{
    void SetOrientationReference(FixedVector3 direction);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void ClearJumpDurations();
    void PlayAnimation(ushort clip);
    void SetCameraFocusHook(int hook);
    void PlaySound(ushort sound, int volume, int pitch);
}

public interface IPlayerSurfaceTransitionHost
{
    FixedVector3 RightDirection { get; }
    void SetOrientationReference(FixedVector3 direction);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void SelectAndResetAnimationSounds(ushort animation);
    int AnimationFrameCount(ushort animation);
    void RunAnimation(ushort animation);
    void PlayAnimation(ushort animation);
    void SetAnimationFrame(short frame);
    void ApplyPlayerPose();
    FixedVector3 GetPlayerHookPosition(int hook);
    void SetTripodMotion(FixedVector3 position, uint duration);
    void SetFloorCamera(int duration);
    void LockTargetTorsoAngle();
    void SwitchToStandMode();
    void TidyLandingPosition(int radius);
    int GroundHeight(FixedVector3 position, int above, int below);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
}

public static class PlayerSurfaceTransitions
{
    /// <summary>Actual CheckStickToCeiling, including held Jump, the second world line and partial jump-duration reset.</summary>
    public static bool CheckCeiling(PlayerMeleeState melee, PlayerWorldHit? ceiling, ushort clearance,
        IPlayerCeilingAttachmentHost host)
    {
        PlayerCombatState combat = melee.Combat;
        if (combat.Velocity.Y > 0 || (combat.CollisionFlags & 0x100) == 0 || ceiling is not { } hit ||
            melee.Input.Pressed[16] == 0 || hit.Contact.Normal.Y <= 3400 || (hit.Contact.SurfaceFlags & 0x40000) != 0) return false;
        combat.IsCrawling = true; combat.CrawlSurfaceNormal = hit.Contact.Normal;
        host.SetOrientationReference(combat.ForwardDirection); host.OrientToNormal(true, combat.ForwardDirection);
        host.ClearJumpDurations(); combat.Position = hit.Contact.Position + hit.Contact.Normal * clearance;
        host.PlayAnimation(combat.AnimationId == 232 ? (ushort)234 : (ushort)227);
        if ((combat.ActionState & 0x300) != 0) host.SetCameraFocusHook(-1);
        combat.ActionState = 1; host.PlaySound(9, 0x2000, 0); combat.FieldAE5 = 0; combat.Field54C = 0;
        return true;
    }

    /// <summary>Actual CheckFenceSurfaceTransition: last-frame posed camera tripod followed by a fresh frame0 clip93.</summary>
    public static bool CheckFence(PlayerSurfaceTransitionState state, IPlayerSurfaceTransitionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        if (state.FenceDetected == 0 || combat.Field8E8 == 0 || !combat.IsCrawling) return false;
        FixedVector3 reference = new(0, 4096, 0);
        host.SetOrientationReference(reference); host.OrientToNormal(true, reference);
        host.SelectAndResetAnimationSounds(0x5D);
        int frames = host.AnimationFrameCount(0x5D) & 0xFFFF;
        host.RunAnimation(0x5D); host.SetAnimationFrame(unchecked((short)(frames - 1))); host.ApplyPlayerPose();
        FixedVector3 hook = host.GetPlayerHookPosition(2);
        host.SetTripodMotion(hook, unchecked((uint)(frames * 2)));
        state.CeilingToWallMotion = 0; host.SetFloorCamera(16);
        state.LineNormal = new(0, -4096, 0); combat.ActionState = 0x2000;
        host.SelectAndResetAnimationSounds(0x5D); host.RunAnimation(0x5D);
        combat.HeadingTurnTicksRemaining = 0; combat.FieldAE5 = 0; return true;
    }

    /// <summary>Complete source action2000 surface transition, including queued slides and all eighteen ground recovery probes.</summary>
    public static void Update(PlayerSurfaceTransitionState state, ushort completedAnimation, ushort clearance,
        IPlayerSurfaceTransitionHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (completedAnimation == ushort.MaxValue)
        {
            int remaining = state.ForwardSlideRemaining;
            if (remaining == 0) return;
            int step = remaining >= 8 ? 8 : remaining;
            combat.Position += combat.ForwardDirection * step;
            state.ForwardSlideRemaining = remaining >= 8 ? unchecked(remaining - 8) : 0;
            return;
        }
        int pushOut = completedAnimation == 0x3F ? 66 : 55;
        state.ApplyHookPosition = 1;
        combat.Position = host.GetPlayerHookPosition(2);
        FixedVector3 reference = combat.UpDirection;
        host.SetOrientationReference(reference); host.LockTargetTorsoAngle();
        if (state.LineNormal.Y < -2600)
        {
            combat.Field8E9 = combat.Field8E8 = 0; combat.CrawlSurfaceNormal = new(0, -4096, 0);
            host.OrientToNormal(true, reference); combat.IsCrawling = false;
            if (completedAnimation is 0x3B or 0x56 or 0x5D)
            {
                host.PlayPositionSound(9, combat.Position, 0); pushOut = 0; host.SwitchToStandMode();
            }
            else { host.PlayAnimation(0x14); combat.ActionState = 1; }
            if (pushOut != 0) { combat.Position += combat.UpDirection * pushOut; pushOut = 0; }
            host.TidyLandingPosition(8);
            int height = host.GroundHeight(combat.Position, 0, 200);
            if (height != -1) combat.Position = combat.Position with { Y = unchecked(height - (clearance << 12)) };
            else
            {
                int step = 8;
                for (int i = 0; i < 18; i++)
                {
                    FixedVector3 probe = combat.Position + host.RightDirection * ((i & 1) != 0 ? step : -step);
                    height = host.GroundHeight(probe, 0, 200);
                    if (height != -1) { combat.Position = probe with { Y = unchecked(height - (clearance << 12)) }; break; }
                    if ((i & 1) != 0) step = unchecked(step * 2);
                }
            }
        }
        else
        {
            combat.CrawlSurfaceNormal = state.LineNormal; host.OrientToNormal(true, reference); combat.ActionState = 16;
            if (combat.CrawlSurfaceNormal.Y > 3400)
            {
                if (combat.Field8E8 != 0 && state.WallMotionRequested != 0) state.WallToCeilingMotion = 1;
                combat.Field8E8 = 0; combat.Field8E9 = 1;
            }
            else
            {
                if (combat.Field8E9 != 0 && state.CeilingMotionRequested != 0) state.CeilingToWallMotion = 1;
                combat.Field8E8 = 1; combat.Field8E9 = 0;
            }
            if (pushOut != 0) { combat.Position += combat.UpDirection * pushOut; pushOut = 0; }
            host.TidyLandingPosition(16);
        }
        if (pushOut != 0) combat.Position += combat.UpDirection * pushOut;
    }
}
