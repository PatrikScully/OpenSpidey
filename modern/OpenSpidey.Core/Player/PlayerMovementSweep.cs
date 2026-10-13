using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerSweepResult(FixedVector3 Displacement, ushort CollisionFlags,
    PlayerWorldHit? LastHit, PlayerWorldHit? BodyHit, int Attempts);

/// <summary>Original carried-object, body and shoulder sweeps with at most two collision attempts.</summary>
public static class PlayerMovementSweep
{
    public static PlayerSweepResult Resolve(FixedVector3 position, FixedVector3 displacement, int reach,
        int elapsedTicks, bool holdingObject, IPlayerWorldQuery world, ushort collisionFlags = 0)
    {
        int attempts = 0;
        PlayerWorldHit? hit = null;
        PlayerWorldHit? bodyHit = null;
        int length = FixedVectorMath.OriginalSquareRoot(PlayerSweepGeometry.ScaledSquaredLength(displacement));
        while (length != 0)
        {
            int horizontalLength = FixedVectorMath.OriginalSquareRoot(
                PlayerSweepGeometry.ScaledSquaredLength(displacement, horizontalOnly: true));
            if (horizontalLength == 0) break;
            FixedVector3 ray = PlayerSweepGeometry.MainRay(displacement, reach, elapsedTicks, length);
            FixedVector3 origin = position with { Y = unchecked(position.Y - 0x40000) };
            if (holdingObject)
            {
                FixedVector3 carriedOrigin = origin with { Y = unchecked(origin.Y - 0x80000) };
                if (world.Trace(carriedOrigin, carriedOrigin + ray) is not null) collisionFlags |= 0x40;
            }
            hit = world.Trace(origin - ray / 4, origin + ray);
            attempts++;
            if (hit is not null)
            {
                collisionFlags = (ushort)((collisionFlags & 0xFFBF) | 1);
                bodyHit = hit;
                FixedVector3 normal = hit.Value.Contact.Normal;
                if (normal == default) throw new InvalidDataException("Player body sweep hit has a zero normal.");
                displacement = PlayerMotion.SlideHorizontally(displacement,
                    FixedVectorMath.Normalize(normal with { Y = 0 }));
            }
            else
            {
                FixedVector3 side = PlayerSweepGeometry.ShoulderSide(displacement, reach, length);
                FixedVector3 forward = PlayerSweepGeometry.ShoulderForward(displacement, reach, elapsedTicks, length, horizontalLength);
                origin = position with { Y = unchecked(position.Y + 0x10000) };
                FixedVector3 start = (origin + side / 4 - forward / 2) with { Y = origin.Y };
                hit = world.Trace(start, origin + side + forward);
                if (hit is null)
                {
                    start = (origin - forward / 2 - side / 4) with { Y = origin.Y };
                    hit = world.Trace(start, origin - side + forward);
                    if (hit is null) break;
                }
                collisionFlags = (ushort)((collisionFlags & 0xFFBF) | 1);
                displacement = PlayerMotion.SlideHorizontally(displacement, hit.Value.Contact.Normal with { Y = 0 });
            }
            if (attempts >= 2) break;
            length = FixedVectorMath.OriginalSquareRoot(PlayerSweepGeometry.ScaledSquaredLength(displacement));
        }
        return new(displacement, collisionFlags, hit, bodyHit, attempts);
    }
}
