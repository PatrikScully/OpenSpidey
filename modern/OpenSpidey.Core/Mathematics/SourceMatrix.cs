using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Mathematics;

/// <summary>Original MATRIX: nine signed Q12 rotation words, one reserved word and three full signed translation words.</summary>
public readonly record struct SourceMatrix(short M00, short M01, short M02, short M10, short M11, short M12,
    short M20, short M21, short M22, ushort Reserved, FixedVector3 Translation)
{
    public static SourceMatrix FromRotation(FixedJointTransform rotation, FixedVector3 translation = default, ushort reserved = 0) =>
        new(rotation.M00, rotation.M01, rotation.M02, rotation.M10, rotation.M11, rotation.M12,
            rotation.M20, rotation.M21, rotation.M22, reserved, translation);

    /// <summary>Actual MulMatrix0 rotates without changing the destination's reserved or translation fields.</summary>
    public SourceMatrix MultiplyRotation(SourceMatrix right) => this with
    {
        M00 = Dot(M00, M01, M02, right.M00, right.M10, right.M20),
        M01 = Dot(M00, M01, M02, right.M01, right.M11, right.M21),
        M02 = Dot(M00, M01, M02, right.M02, right.M12, right.M22),
        M10 = Dot(M10, M11, M12, right.M00, right.M10, right.M20),
        M11 = Dot(M10, M11, M12, right.M01, right.M11, right.M21),
        M12 = Dot(M10, M11, M12, right.M02, right.M12, right.M22),
        M20 = Dot(M20, M21, M22, right.M00, right.M10, right.M20),
        M21 = Dot(M20, M21, M22, right.M01, right.M11, right.M21),
        M22 = Dot(M20, M21, M22, right.M02, right.M12, right.M22)
    };
    private static short Dot(short a, short b, short c, short x, short y, short z) =>
        unchecked((short)(unchecked(a * x + b * y + c * z) >> 12));
}
