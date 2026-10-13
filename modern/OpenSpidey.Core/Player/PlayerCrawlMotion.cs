using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerCrawlFrame(FixedVector3 Position, FixedVector3 Velocity,
    FixedVector3 Acceleration, AxisDamping Damping, int ElapsedTicks, ushort SurfaceClearance,
    FixedVector3 SurfaceNormal, FixedVector3 Up, FixedVector3 Forward, FixedVector3 Right,
    bool OnWall, byte GroundContactGraceUpdates = 0,
    int ForwardClearance = 0, int CornerDistance = 0, bool ExteriorCorner = false,
    PlayerWorldHit? PreviousBodyHit = null);

/// <summary>Surface motion plus effects that must be applied by the owning player controller.</summary>
public readonly record struct PlayerCrawlStep(PlayerMovementStep Movement, ushort CollisionFlags,
    FixedVector3 SurfaceNormal, byte GroundContactGraceUpdates, PlayerWorldHit? BodyHit,
    PlayerWorldHit? GroundHit, PlayerWorldHit? RecoveryCornerHit, bool HardSurface, bool Fence,
    bool ExteriorCorner, int ForwardClearance, int CornerDistance,
    FixedVector3? ShadowPosition, bool? UseAlternateBrightness, bool CeilingControlsChanged,
    bool NotifySupport)
{
    /// <summary>The original primary line is initialized only when movement runs its forward/corner probe.</summary>
    public bool SourceLineWasQueried { get; init; }
    /// <summary>Actual last forward/corner hit before its pItem can be cleared by the source surface filter.</summary>
    public PlayerWorldHit? SourceLineQueryHit { get; init; }
}

/// <summary>Recovered crawl probes and response, independent of animation hooks and gameplay actors.</summary>
public static class PlayerCrawlMotion
{
    private const uint NonCrawlable = 0x40000;
    private const uint DarkSurface = 0x800000;
    private const uint HardSurface = 0x1000000;
    private const uint FenceSurface = 0x8000000;
    private const int SameSurfaceAlignment = 3271;

    public static PlayerCrawlStep Step(PlayerCrawlFrame frame, IPlayerWorldQuery world,
        Func<FixedVector3, FixedVector3> getBodyHook)
    {
        FixedVector3 velocity = PlayerMotion.UpdateVelocity(frame.Velocity, frame.Acceleration, frame.Damping);
        bool moved = velocity != default;
        FixedVector3 position = frame.Position;
        if (moved) position += PlayerMotion.Displacement(velocity, frame.ElapsedTicks);
        FixedVector3 hook = getBodyHook(position);
        ushort collisions = 0;
        PlayerWorldHit? body = frame.PreviousBodyHit;
        PlayerWorldHit? sourceLineQueryHit = null;
        bool exterior = frame.ExteriorCorner;
        int clearance = frame.ForwardClearance, cornerDistance = frame.CornerDistance;

        if (moved)
        {
            FixedVector3 backward = frame.Forward * -96;
            FixedVector3 forwardEnd = hook + backward;
            body = world.Trace(hook - backward / 4, forwardEnd);
            sourceLineQueryHit = body;
            if (body is { } forwardHit)
            {
                if (Alignment(forwardHit.Contact.Normal, frame.SurfaceNormal) > SameSurfaceAlignment)
                    body = null; // Rejected forward hits skip side and corner probes.
                else
                {
                    collisions |= 1;
                    position = frame.Position;
                    if ((forwardHit.Contact.SurfaceFlags & NonCrawlable) != 0 &&
                        (!frame.OnWall || forwardHit.Contact.Normal.Y >= -2600)) body = null;
                    else clearance = forwardHit.Contact.Distance >= 80 ? 0 : unchecked(88 - forwardHit.Contact.Distance);
                }
            }
            else
            {
                if (world.Trace(hook, hook + frame.Right * 16) is not null ||
                    world.Trace(hook, hook - frame.Right * 16) is not null)
                    position = frame.Position;
                FixedVector3 cornerStart = forwardEnd - frame.Up * 70;
                body = world.Trace(cornerStart, cornerStart + frame.Forward * 128);
                sourceLineQueryHit = body;
                if (body is { } corner && (corner.Contact.SurfaceFlags & NonCrawlable) == 0 &&
                    Alignment(corner.Contact.Normal, frame.SurfaceNormal) <= SameSurfaceAlignment)
                {
                    exterior = true;
                    position = frame.Position;
                    cornerDistance = corner.Contact.Distance <= 16 ? 0 : unchecked(corner.Contact.Distance - 8);
                }
                else body = null;
            }
        }

        PlayerWorldHit? ground = TraceGround(position);
        if (ground is null)
        {
            collisions |= 2;
            FixedVector3 recoveryStart = position - frame.Forward * 96 - frame.Up * 70;
            PlayerWorldHit? recovery = world.Trace(recoveryStart, recoveryStart + (frame.Forward << 7));
            if (recovery is { } hit && (hit.Contact.SurfaceFlags & NonCrawlable) == 0) exterior = true;
            else recovery = null;
            position = frame.Position;
            ground = TraceGround(position);
            if (ground is null) collisions &= unchecked((ushort)~2);
            // The source copies only item/position/normal into its forward line here, retaining
            // its old face and distance. Keep that partial update separate from a complete hit.
            return Result(frame.SurfaceNormal, frame.GroundContactGraceUpdates, recovery: recovery);
        }

        PlayerGroundContact contact = ground.Value.Contact;
        collisions |= 2;
        bool alternateBrightness = (contact.SurfaceFlags & DarkSurface) != 0;
        if ((contact.SurfaceFlags & NonCrawlable) != 0)
        {
            bool fence = (contact.SurfaceFlags & FenceSurface) != 0;
            if (!fence) position = frame.Position;
            return Result(frame.SurfaceNormal, 4, fence: fence, brightness: alternateBrightness);
        }
        FixedVector3 target = contact.Position + contact.Normal * frame.SurfaceClearance;
        if (moved || LengthSquared(target - position) > 8) position = target;
        return Result(contact.Normal, 4, hard: (contact.SurfaceFlags & HardSurface) != 0,
            shadow: contact.Position, brightness: alternateBrightness,
            controlsChanged: (contact.Normal.Y > 3400) != (frame.SurfaceNormal.Y > 3400),
            notifySupport: contact.SupportsPlayer);

        PlayerWorldHit? TraceGround(FixedVector3 at) => world.Trace(at + frame.Up * 40, at - frame.Up * 140);
        PlayerCrawlStep Result(FixedVector3 normal, byte grace, PlayerWorldHit? recovery = null,
            bool hard = false, bool fence = false, FixedVector3? shadow = null, bool? brightness = null,
            bool controlsChanged = false, bool notifySupport = false) =>
            new(new(position, velocity), collisions, normal, grace, body, ground, recovery,
                hard, fence, exterior, clearance, cornerDistance, shadow, brightness, controlsChanged, notifySupport)
            { SourceLineWasQueried = moved, SourceLineQueryHit = sourceLineQueryHit };
    }

    private static int Alignment(FixedVector3 left, FixedVector3 right) =>
        unchecked(((left.X * right.X) >> 12) + ((left.Y * right.Y) >> 12) + ((left.Z * right.Z) >> 12));

    private static int LengthSquared(FixedVector3 value)
    {
        FixedVector3 whole = value >> 12;
        return unchecked(whole.X * whole.X + whole.Y * whole.Y + whole.Z * whole.Z);
    }
}
