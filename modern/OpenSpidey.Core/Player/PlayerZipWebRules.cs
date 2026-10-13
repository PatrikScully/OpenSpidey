using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Animation;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerZipFrame(FixedVector3 Position, FixedVector3 Up, FixedVector3 SurfaceNormal,
    uint Action, ushort SurfaceClearance, bool SuppressInput = false, bool HoldingObject = false, bool Locked = false);

public readonly record struct PlayerZipLanding(FixedVector3 Position, FixedVector3 Normal, FixedVector3? OrientationTarget);

/// <summary>Recovered straight zip target checks, pull steering and landing orientation.</summary>
public static class PlayerZipWebRules
{
    public const int PullStartFrame = 13;
    public const int PullSpeed = 240;
    public const int StraightReach = 3072;
    private const uint NonCrawlableFace = 0x40000;
    private const int ArrivalDistance = 128;

    public static bool IsPullWindow(AnimationPlaybackState playback) =>
        (playback.ClipId == PlayerAnimationIds.ZipPull && playback.Frame >= PullStartFrame) ||
        playback.ClipId == PlayerAnimationIds.ZipTravel;

    public static PlayerWorldHit? FindStraightTarget(PlayerZipFrame frame, IPlayerWorldQuery world)
    {
        if (frame.SuppressInput || frame.HoldingObject || frame.Locked) return null;
        PlayerWorldHit? target = world.Trace(frame.Position, frame.Position + frame.Up * StraightReach);
        return target is { } hit && IsAvailable(frame, hit, StraightReach) ? hit : null;
    }

    public static bool IsAvailable(PlayerZipFrame frame, PlayerWorldHit target, int range)
    {
        PlayerGroundContact contact = target.Contact;
        if (contact.Distance <= (frame.Action == 4 ? 8 : 16) || contact.Distance >= range ||
            (contact.SurfaceFlags & NonCrawlableFace) != 0) return false;
        FixedVector3 offset = (frame.Up * frame.SurfaceClearance - frame.Position + contact.Position) >> 12;
        offset = new(unchecked((short)offset.X), unchecked((short)offset.Y), unchecked((short)offset.Z));
        int clearance = unchecked(frame.SurfaceNormal.X * offset.X + frame.SurfaceNormal.Y * offset.Y +
            frame.SurfaceNormal.Z * offset.Z) >> 12;
        return frame.Action == 4 || clearance > 64;
    }

    public static bool HasArrived(FixedVector3 position, PlayerWorldHit target)
    {
        FixedVector3 offset = (position - target.Contact.Position) >> 12;
        FixedVector3 normal = target.Contact.Normal;
        int approach = unchecked(normal.X * offset.X + normal.Z * offset.Z + normal.Y * offset.Y);
        return approach < 0 || FixedAim.Distance(position, target.Contact.Position) < ArrivalDistance;
    }

    public static PlayerZipLanding Land(FixedVector3 origin, PlayerWorldHit target, FixedVector3 previousNormal,
        FixedVector3 forward, ushort clearance)
    {
        FixedVector3 normal = target.Contact.Normal;
        FixedVector3? orientation;
        if (normal.Y is >= -2600 and <= 3400)
        {
            FixedVector3 travel = FixedVectorMath.Normalize((origin - target.Contact.Position) >> 12);
            int alignment = unchecked(((normal.X * travel.X) >> 12) + ((normal.Y * travel.Y) >> 12) + ((normal.Z * travel.Z) >> 12));
            if (alignment < 2048) orientation = travel;
            else if (Math.Abs(previousNormal.Y) > 2048) orientation = previousNormal * -1;
            else orientation = null;
        }
        else if (Math.Abs(forward.Y) < 2048) orientation = forward;
        else
        {
            FixedVector3 travel = FixedVectorMath.Normalize((origin - target.Contact.Position) >> 12);
            orientation = travel.Y < 2048 ? travel : new(0, 0, 4096);
        }
        return new(target.Contact.Position + normal * clearance, normal, orientation);
    }

    public static FixedVector3 TidyLanding(FixedVector3 position, FixedVector3 right, FixedVector3 forward,
        int radius, IPlayerWorldQuery world)
    {
        for (int angle = 0; angle < 4096; angle += 512)
        {
            (short sine, short cosine) = GameAngle.Heading(angle);
            var offset = new FixedVector3(unchecked(radius * (((right.X * cosine) >> 12) + ((forward.X * sine) >> 12))),
                unchecked(radius * (((right.Y * cosine) >> 12) + ((forward.Y * sine) >> 12))),
                unchecked(radius * (((right.Z * cosine) >> 12) + ((forward.Z * sine) >> 12))));
            if (world.Trace(position + offset, position - offset) is { } hit) position += hit.Contact.Normal * radius;
        }
        return position;
    }
}
