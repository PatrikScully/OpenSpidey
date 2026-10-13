using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered world sweep rays for player motion and shoulder clearance.</summary>
public static class PlayerSweepGeometry
{
    public static int ScaledSquaredLength(FixedVector3 displacement, bool horizontalOnly = false)
    {
        int x = displacement.X >> 9;
        int y = horizontalOnly ? 0 : displacement.Y >> 9;
        int z = displacement.Z >> 9;
        return unchecked(x * x + z * z + y * y);
    }

    public static int ReachForTicks(int reach, int elapsedTicks) =>
        elapsedTicks < 5 ? reach : unchecked(reach + (elapsedTicks - 4) * (reach >> 1));

    public static FixedVector3 MainRay(FixedVector3 displacement, int reach, int elapsedTicks, int length)
    {
        int adjustedReach = ReachForTicks(reach, elapsedTicks);
        var ray = new FixedVector3(Scale(displacement.X, 8, adjustedReach, length),
            Scale(displacement.Y, 8, reach, length), Scale(displacement.Z, 8, adjustedReach, length));
        return ray + (ray >> 3);
    }

    public static FixedVector3 ShoulderSide(FixedVector3 displacement, int reach, int length) =>
        new(Scale(displacement.Z, 4, reach, length), 0, unchecked(-Scale(displacement.X, 4, reach, length)));

    public static FixedVector3 ShoulderForward(FixedVector3 displacement, int reach, int elapsedTicks,
        int length, int horizontalLength)
    {
        int adjustedReach = ReachForTicks(reach, elapsedTicks);
        return new(Scale(displacement.X, 8, adjustedReach, horizontalLength),
            Scale(displacement.Y, 8, reach, length), Scale(displacement.Z, 8, adjustedReach, horizontalLength));
    }

    private static int Scale(int component, int multiplier, int reach, int length) =>
        unchecked(component * multiplier * reach) / length;
}
