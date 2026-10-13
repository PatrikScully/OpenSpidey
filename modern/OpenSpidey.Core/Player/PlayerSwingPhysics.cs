using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerSwingPhysicsFrame(FixedVector3 PreviousPosition, FixedVector3 ArcPosition,
    FixedVector3 HookBefore, FixedVector3 HookAfter, ushort SurfaceClearance);
public readonly record struct PlayerSwingPhysicsResult(FixedVector3 Position, FixedVector3 Velocity, ushort CollisionFlags,
    PlayerWorldHit? Contact);

/// <summary>Original DoSwingingPhysics467D20 ordered body-hook/vertical collision rays and source position/velocity response.</summary>
public static class PlayerSwingPhysics
{
    public static PlayerSwingPhysicsResult Step(PlayerSwingPhysicsFrame frame, IPlayerWorldQuery world)
    {
        FixedVector3 extended = new(unchecked(2 * frame.HookAfter.X - frame.HookBefore.X), frame.HookAfter.Y,
            unchecked(2 * frame.HookAfter.Z - frame.HookBefore.Z));
        if (world.Trace(frame.HookBefore, extended) is { } movement)
            return Stop(movement, movement.Contact.Normal.Y >= -2600 ? (ushort)1 : (ushort)2, frame.SurfaceClearance);
        if (world.Trace(extended, extended with { Y = unchecked(extended.Y + 0x80000) }) is { } positiveVertical)
            return Stop(positiveVertical, 2, frame.SurfaceClearance);
        if (world.Trace(extended, extended with { Y = unchecked(extended.Y - 0x20000) }) is { } negativeVertical)
            return Stop(negativeVertical, 1, frame.SurfaceClearance);
        return new(frame.ArcPosition, frame.ArcPosition - frame.PreviousPosition, 0, null);
    }
    private static PlayerSwingPhysicsResult Stop(PlayerWorldHit hit, ushort flags, ushort clearance) =>
        new(hit.Contact.Position + hit.Contact.Normal * clearance, default, flags, hit);
}
