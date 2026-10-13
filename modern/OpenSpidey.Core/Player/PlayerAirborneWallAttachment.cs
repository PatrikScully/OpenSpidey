using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public interface IPlayerAirborneWallAttachmentHost
{
    int GroundHeight(FixedVector3 position, int above, int below);
    void OrientToNormal(bool useTarget, FixedVector3 target);
    void PlayAnimation(ushort animation);
    void PlaySound(ushort sound, int volume, int pitch);
}

/// <summary>Whole CheckStickToWall4BFEC0, over the actual body-contact item/normal rather than shoulder-only collision.</summary>
public static class PlayerAirborneWallAttachment
{
    public static bool Check(PlayerMeleeState melee, PlayerWorldHit? bodyContact, ushort surfaceClearance, IPlayerAirborneWallAttachmentHost host)
    {
        PlayerCombatState combat = melee.Combat;
        if ((combat.ActionState & 4) == 0 || (combat.CollisionFlags & 1) == 0 || bodyContact is not { } hit) return false;
        PlayerGroundContact contact = hit.Contact;
        if (contact.Normal.Y is > 3400 or < -2600 || (contact.SurfaceFlags & 0x40000) != 0 ||
            host.GroundHeight(combat.Position, 0, 180) != -1) return false;
        // The original bad-normal print is nonfatal; its orientation fallback retains source behavior.
        combat.CrawlSurfaceNormal = contact.Normal;
        combat.IsCrawling = true;
        host.OrientToNormal(true, new(0, 4096, 0));
        combat.Velocity = default;
        combat.HeadingTurnTicksRemaining = 0;
        combat.Position = contact.Position + combat.CrawlSurfaceNormal * surfaceClearance;
        host.PlayAnimation(combat.AnimationId == 232 ? (ushort)234 : (ushort)227);
        combat.ActionState = 1;
        host.PlaySound(9, 8192, 0);
        return true;
    }
}
