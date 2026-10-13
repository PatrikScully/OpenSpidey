using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerWalkingHost
{
    bool HardSurface { get; }
    short InputHeading { get; }
    short CameraHeading { get; }
    short GetEffectiveHeading();
    void ClearAndSmashHeldObject();
    bool CheckGroundGone();
    bool CheckWebShot();
    bool CheckJump();
    bool CheckCeilingJumpingSmashPunch();
    bool CheckKick();
    bool CheckRunIntoWall();
    bool CheckJumpingSwingWeb();
    bool CheckJumpingR1ZipWeb();
    bool CheckJumpingR2ZipWeb();
    bool CheckInteriorSurfaceTransition();
    bool CheckExteriorSurfaceTransition();
    bool CheckFenceSurfaceTransition();
    void PlayAnimation(ushort clip);
    void SwitchToStandMode();
    void LockTargetTorsoAngle();
}

/// <summary>Entire source action16, retaining both CheckWebShot calls and exact guard precedence before movement/roll recovery.</summary>
public static class PlayerWalkingBehavior
{
    public static void Step(PlayerMeleeState melee, ushort completedAnimation, IPlayerWalkingHost host)
    {
        PlayerCombatState combat = melee.Combat;
        combat.FieldEA6 = 0;
        if (combat.HeldObjectPresent && (combat.CollisionFlags & 0x40) != 0)
        {
            host.PlayAnimation(21);
            combat.HeldObjectPresent = false;
            host.ClearAndSmashHeldObject();
        }
        if (host.CheckGroundGone() || host.CheckWebShot() || host.CheckJump() || host.CheckCeilingJumpingSmashPunch() ||
            host.CheckKick() || host.CheckWebShot() || host.CheckRunIntoWall()) return;
        if (!combat.IsCrawling && (host.CheckJumpingSwingWeb() || host.CheckJumpingR1ZipWeb() || host.CheckJumpingR2ZipWeb())) return;
        if (host.CheckInteriorSurfaceTransition() || host.CheckExteriorSurfaceTransition() || host.CheckFenceSurfaceTransition())
        {
            combat.Velocity = default;
            return;
        }
        if (combat.LookaroundActive || (melee.Input.Sideways | melee.Input.Forward) == 0)
        {
            host.SwitchToStandMode();
            return;
        }
        if (completedAnimation == 52)
        {
            if (!host.HardSurface) host.PlayAnimation(50);
        }
        else if (completedAnimation is 51 or 85 && host.HardSurface) host.PlayAnimation(52);
        int wanted = combat.Field8E8 != 0 ? host.InputHeading : (host.InputHeading + host.CameraHeading) & 4095;
        int delta = (wanted - host.GetEffectiveHeading()) & 4095;
        if (delta is <= 1536 or >= 2560 || PlayerLocomotionAnimations.Reverse(combat.AnimationId) is not { } reverse) return;
        host.PlayAnimation(unchecked((ushort)reverse));
        combat.Velocity = default;
        host.LockTargetTorsoAngle();
        combat.ActionState = 0x400000;
    }
}

public readonly record struct PlayerRunIntoWallContact(FixedVector3 Normal, uint FaceFlags);

public interface IPlayerRunIntoWallHost
{
    void SelectAndRunAnimation(ushort animation);
}

/// <summary>Original4BFBC0 wall counter, including byte overflow and the prior counter's trigger before its final reset.</summary>
public static class PlayerRunIntoWall
{
    public static bool Check(PlayerCombatState combat, PlayerRunIntoWallContact? contact, IPlayerRunIntoWallHost host)
    {
        if (combat.HeldObjectPresent) return false;
        bool reset = true;
        if ((combat.CollisionFlags & 1) != 0 && contact is { } hit &&
            hit.Normal.Y is >= -2600 and <= 3400 && (hit.FaceFlags & 0x40000) == 0)
        {
            FixedVector3 forward = combat.ForwardDirection;
            int alignment = unchecked(((forward.X * hit.Normal.X) >> 12) + ((forward.Y * hit.Normal.Y) >> 12) +
                ((forward.Z * hit.Normal.Z) >> 12));
            if (alignment > 3800)
            {
                reset = false;
                combat.FieldAD7 = unchecked((byte)(combat.FieldAD7 + combat.ElapsedTicks));
            }
            if (combat.FieldAD7 > 20)
            {
                combat.FieldAD7 = 0;
                host.SelectAndRunAnimation(14);
                combat.ActionState = 0x80000;
                return true;
            }
        }
        if (reset) combat.FieldAD7 = 0;
        return false;
    }
}
