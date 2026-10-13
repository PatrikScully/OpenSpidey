using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerWallAttachmentFrame(FixedVector3 Position, uint Action,
    ushort CollisionFlags, PlayerWorldHit? BodyHit, ushort SurfaceClearance, ushort AnimationId);

/// <summary>The controller must apply this position/normal, stop velocity and turning, and enter crawl idle.</summary>
public readonly record struct PlayerWallAttachment(FixedVector3 Position, FixedVector3 Normal, ushort AnimationId);

/// <summary>Recovered falling-to-wall eligibility; shoulder-only hits cannot start attachment.</summary>
public static class PlayerWallAttachmentRules
{
    public static PlayerWallAttachment? Resolve(PlayerWallAttachmentFrame frame, IPlayerWorldQuery world)
    {
        if ((frame.Action & 4) == 0 || (frame.CollisionFlags & 1) == 0 || frame.BodyHit is not { } hit)
            return null;
        PlayerGroundContact contact = hit.Contact;
        if (contact.Normal.Y is > 3400 or < -2600 || (contact.SurfaceFlags & 0x40000) != 0)
            return null;
        PlayerWorldHit? nearbyGround = world.Trace(frame.Position, frame.Position + new FixedVector3(0, 180 << 12, 0));
        // Preserve the original height routine's -1 sentinel, rather than testing only the hit pointer.
        if (nearbyGround is { } ground && ground.Contact.Position.Y != -1) return null;
        return new(contact.Position + contact.Normal * frame.SurfaceClearance, contact.Normal,
            (ushort)(frame.AnimationId == 232 ? 234 : 227));
    }
}
