using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerSwingBasisResult(SourceMatrix Matrix, int VerticalAlignment);

/// <summary>Actual CalculateSwingWebParameters: normalize the anchor axis and its Q12 cross with full DA0.</summary>
public static class PlayerSwingBasis
{
    public static PlayerSwingBasisResult Calculate(FixedVector3 position, FixedVector3 anchor,
        FixedVector3 surfaceNormal, SourceMatrix previous)
    {
        FixedVector3 down = FixedVectorMath.Normalize((position - anchor) >> 12);
        FixedVector3 side = FixedVectorMath.Normalize(Cross(down, surfaceNormal));
        FixedVector3 forward = Cross(down, side);
        return new(previous with
        {
            M00 = unchecked((short)side.X), M10 = unchecked((short)side.Y), M20 = unchecked((short)side.Z),
            M01 = unchecked((short)down.X), M11 = unchecked((short)down.Y), M21 = unchecked((short)down.Z),
            M02 = unchecked((short)forward.X), M12 = unchecked((short)forward.Y), M22 = unchecked((short)forward.Z)
        }, GameWord.Absolute(down.Y));
    }
    private static FixedVector3 Cross(FixedVector3 a, FixedVector3 b) => new(
        unchecked(a.Y * b.Z - a.Z * b.Y) >> 12,
        unchecked(a.Z * b.X - a.X * b.Z) >> 12,
        unchecked(a.X * b.Y - a.Y * b.X) >> 12);
}
