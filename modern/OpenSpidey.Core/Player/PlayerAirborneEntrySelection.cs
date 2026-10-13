using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerAirborneEntryHost
{
    PlayerJumpImpulse Jump { get; set; }
    int SpeedRamp { get; set; }
    byte GroundContactGrace { get; set; }
    int? SupportVerticalVelocity { get; }
    short InputHeading { get; }
    void PlaySingleAnimation(ushort animation, int firstFrame);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void DropHeldObject(FixedVector3 velocity);
}

public interface IPlayerAirborneEntryFrameHost
{
    bool CheckJump();
    bool CheckGroundGone();
}

/// <summary>Whole original CheckJump4C2090 and CheckGroundGone4C23A0, retaining shared inputs and carried-item ownership.</summary>
public static class PlayerAirborneEntrySelection
{
    public static bool CheckJump(PlayerMeleeState melee, PlayerLookaroundState lookaround, IPlayerAirborneEntryHost host)
    {
        PlayerCombatState combat = melee.Combat;
        if (melee.Input.Triggered[16] == 0) return false;
        melee.Input.Triggered[16] = 0;
        melee.Input.Mirrors[16] = 0;
        if (combat.LookaroundActive || combat.HeldObjectPresent) return false;
        if (!combat.IsCrawling)
        {
            host.Jump = host.Jump with { Velocity = PlayerSupportRules.JumpVelocity(host.SupportVerticalVelocity) };
            combat.FieldAE6 = 0;
            bool moving = (combat.ActionState & 16) != 0;
            host.PlaySingleAnimation(moving ? (ushort)223 : (ushort)210, moving ? 5 : 4);
            combat.FieldE8C = moving ? (byte)1 : (byte)0;
            combat.FieldAE5 = 0;
            combat.ActionState = 64;
            if ((combat.CollisionFlags & 1) != 0) host.SpeedRamp = 0;
        }
        else if (combat.Field8E8 != 0)
        {
            combat.CollisionFlags &= unchecked((ushort)~2);
            combat.FieldAE5 = 1;
            combat.FieldAE6 = 0;
            combat.IsCrawling = false;
            combat.ActionState = 4;
            host.PlaySingleAnimation(216, 0);
            combat.CrawlSurfaceNormal = new(0, -4096, 0);
            bool away = (melee.Input.Sideways != 0 || melee.Input.Forward != 0) && host.InputHeading is >= 1536 and <= 2560;
            host.OrientToNormal(true, away ? combat.UpDirection * -1 : combat.UpDirection);
            combat.Field8E8 = 0;
        }
        else if (combat.Field8E9 != 0)
        {
            combat.CollisionFlags &= unchecked((ushort)~2);
            combat.FieldAE5 = 0;
            combat.FieldAE6 = 0;
            combat.IsCrawling = false;
            combat.ActionState = 4;
            host.PlaySingleAnimation(212, 0);
            combat.FieldE8C = 0;
            combat.CrawlSurfaceNormal = new(0, -4096, 0);
            host.OrientToNormal(true, combat.ForwardDirection);
            combat.Field8E9 = 0;
        }
        else return false;
        host.PlayPositionSound(9, combat.Position, 0);
        lookaround.JumpReleased = 0;
        return true;
    }

    public static bool CheckGroundGone(PlayerMeleeState melee, IPlayerAirborneEntryHost host)
    {
        PlayerCombatState combat = melee.Combat;
        if ((combat.CollisionFlags & 2) != 0) return false;
        if (host.GroundContactGrace != 0) host.GroundContactGrace--;
        if (host.GroundContactGrace != 0) return false;
        if (combat.HeldObjectPresent)
        {
            host.DropHeldObject(combat.UpDirection * 4);
            combat.HeldObjectPresent = false;
        }
        combat.FallReferenceHeight = combat.Position.Y;
        host.PlaySingleAnimation(212, 0);
        combat.FieldE8C = combat.FieldAE5 = combat.FieldAE6 = 0;
        if (combat.IsCrawling)
        {
            combat.IsCrawling = false;
            combat.CrawlSurfaceNormal = new(0, -4096, 0);
            host.OrientToNormal(true, new(0, 0, 4096));
        }
        combat.ActionState = 4;
        return true;
    }
}
