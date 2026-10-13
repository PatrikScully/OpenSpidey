using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Mathematics;

/// <summary>Recovered Q12 matrix/vector transforms with wrapping 32-bit products and sums.</summary>
public static class FixedRotation
{
    public static FixedJointTransform TransposeRotation(FixedJointTransform basis) => new(
        basis.M00, basis.M10, basis.M20, basis.M01, basis.M11, basis.M21,
        basis.M02, basis.M12, basis.M22, 0, 0, 0);

    public static FixedVector3 Transform(FixedJointTransform basis, FixedVector3 vector) => new(
        Dot(basis.M00, basis.M01, basis.M02, vector),
        Dot(basis.M10, basis.M11, basis.M12, vector),
        Dot(basis.M20, basis.M21, basis.M22, vector));

    public static FixedVector3 TransformTranspose(FixedJointTransform basis, FixedVector3 vector) => new(
        Dot(basis.M00, basis.M10, basis.M20, vector),
        Dot(basis.M01, basis.M11, basis.M21, vector),
        Dot(basis.M02, basis.M12, basis.M22, vector));

    private static int Dot(short x, short y, short z, FixedVector3 vector) =>
        unchecked(x * vector.X + y * vector.Y + z * vector.Z) >> 12;
}
