using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Integer movement rules shared by ground, crawl and scripted player physics.</summary>
public static class PlayerMotion
{
    public static PlayerMovementStep IntegrateCollisionFree(FixedVector3 position, FixedVector3 velocity,
        FixedVector3 acceleration, AxisDamping damping, int elapsedTicks)
    {
        FixedVector3 updated = UpdateVelocity(velocity, acceleration, damping);
        return new(position + CollisionFreeDisplacement(updated, elapsedTicks), updated);
    }

    public static FixedVector3 UpdateVelocity(FixedVector3 velocity, FixedVector3 acceleration, AxisDamping damping)
    {
        FixedVector3 accelerated = velocity + acceleration;
        return new(RemoveSmallVelocity(DampedValue(accelerated.X, damping.X)),
            RemoveSmallVelocity(DampedValue(accelerated.Y, damping.Y)),
            RemoveSmallVelocity(DampedValue(accelerated.Z, damping.Z)));
    }

    public static FixedVector3 Displacement(FixedVector3 velocity, int elapsedTicks) =>
        elapsedTicks <= 2 ? velocity : velocity + (velocity >> 1) * unchecked(elapsedTicks - 2);

    public static FixedVector3 CollisionFreeDisplacement(FixedVector3 velocity, int elapsedTicks) =>
        elapsedTicks <= 2 ? velocity : velocity + velocity * unchecked(elapsedTicks - 2);

    public static FixedVector3 VelocityFromDisplacement(FixedVector3 displacement, int elapsedTicks) =>
        elapsedTicks <= 2 ? displacement : (displacement << 1) / elapsedTicks;

    public static int FallDisplacement(int velocity, int elapsedTicks) =>
        elapsedTicks <= 2 ? velocity : unchecked(velocity + (elapsedTicks - 2) * (velocity >> 1));

    public static int DampedValue(int value, byte shift) => unchecked(value - (value >> shift));

    public static FixedVector3 SlideHorizontally(FixedVector3 displacement, FixedVector3 normal)
    {
        int dot = unchecked((displacement.Z >> 6) * normal.Z + (displacement.X >> 6) * normal.X);
        if (dot > 0) return displacement;
        int scale = dot >> 12;
        return new(unchecked(displacement.X - (unchecked(scale * normal.X) >> 6)) >> 2,
            displacement.Y, unchecked(displacement.Z - (unchecked(scale * normal.Z) >> 6)) >> 2);
    }

    private static int RemoveSmallVelocity(int value) => value is >= -2048 and <= 2048 ? 0 : value;
}
