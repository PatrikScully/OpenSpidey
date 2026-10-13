using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Manipulation;

public readonly record struct ManipulationDebrisRequest(uint ModelChecksum, FixedVector3 Position, FixedVector3 Velocity);

public sealed class ManipulationDebrisState
{
    public ActorBodyMotion Body { get; } = new();
    public uint ModelChecksum { get; private set; }
    public ushort BodyFlags { get; set; } = 0x16;
    public ushort ItemFlags { get; set; }
    public int HitCount { get; set; }
    public int RemainingTicks { get; set; }
    public static ManipulationDebrisState Create(uint model, FixedVector3 position, FixedVector3 velocity, Func<int, int> random)
    {
        var state = new ManipulationDebrisState();
        state.Body.LinearDamping = new(12, 12, 12); state.Body.Position = position; state.Body.Velocity = velocity;
        state.Body.Angles = new(unchecked((short)random(4096)), unchecked((short)random(4096)), 0);
        state.Body.Acceleration = new(0, 4096, 0);
        state.Body.AngularVelocity = new(unchecked((short)(64 - random(32))), unchecked((short)(64 - random(32))), 0);
        state.ModelChecksum = model;
        state.RemainingTicks = unchecked(random(30) + 60); return state;
    }
    public void Advance(int elapsedTicks, IManipulationDebrisHost host, Func<int, int> random)
    {
        RemainingTicks = unchecked(RemainingTicks - elapsedTicks);
        if (RemainingTicks <= 0) { BodyFlags |= 0x40; return; }
        FixedVector3 previous = Body.Position;
        for (int tick = 0; tick < elapsedTicks; tick++)
        {
            Body.Velocity += Body.Acceleration;
            Body.Velocity = new(Damp(Body.Velocity.X, Body.LinearDamping.X), Damp(Body.Velocity.Y, Body.LinearDamping.Y), Damp(Body.Velocity.Z, Body.LinearDamping.Z));
            Body.Position += Body.Velocity; Body.Angles += Body.AngularVelocity;
        }
        FixedVector3 direction = FixedVectorMath.Normalize((Body.Position - previous) >> 12), offset = direction * 64;
        if (host.LineToItem(previous, Body.Position + offset, 1) is not { } hit) return;
        HitCount = unchecked(HitCount + 1);
        // Original counter starts0, so the first normal hit already dies. Preserve the signed-negative bounce branch too.
        if (HitCount >= 1) { BodyFlags |= 0x40; return; }
        short yawVelocity = Body.AngularVelocity.Y;
        Body.AngularVelocity = Body.AngularVelocity with { X = unchecked((short)(random(128) - 64)), Y = unchecked((short)(random(2 * yawVelocity) - yawVelocity)) };
        Body.Position = hit.Position - offset;
        FixedVector3 wholeVelocity = Body.Velocity >> 12;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked(wholeVelocity.X * wholeVelocity.X + wholeVelocity.Y * wholeVelocity.Y + wholeVelocity.Z * wholeVelocity.Z));
        Body.Velocity /= length;
        FixedVector3 normal = new(unchecked((short)hit.Normal.X), unchecked((short)hit.Normal.Y), unchecked((short)hit.Normal.Z));
        int twiceDot = unchecked((unchecked(-(normal.X * Body.Velocity.X + normal.Y * Body.Velocity.Y + normal.Z * Body.Velocity.Z)) >> 12) * 2);
        Body.Velocity = new(unchecked(((unchecked(normal.X * twiceDot) >> 12) + Body.Velocity.X) * length),
            unchecked(((unchecked(normal.Y * twiceDot) >> 12) + Body.Velocity.Y) * length), unchecked(((unchecked(normal.Z * twiceDot) >> 12) + Body.Velocity.Z) * length));
    }
    private static int Damp(int value, byte shift) => unchecked(value - (value >> shift));
}
public interface IManipulationDebrisHost
{
    ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode);
}
