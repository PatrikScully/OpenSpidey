namespace OpenSpidey.Core.Mathematics;

/// <summary>Original signed Q12 vector arithmetic with wrapping 32-bit components.</summary>
public readonly record struct FixedVector3(int X, int Y, int Z)
{
    public static FixedVector3 operator +(FixedVector3 left, FixedVector3 right) =>
        new(unchecked(left.X + right.X), unchecked(left.Y + right.Y), unchecked(left.Z + right.Z));

    public static FixedVector3 operator -(FixedVector3 left, FixedVector3 right) =>
        new(unchecked(left.X - right.X), unchecked(left.Y - right.Y), unchecked(left.Z - right.Z));

    public static FixedVector3 operator *(FixedVector3 vector, int scalar) =>
        new(unchecked(vector.X * scalar), unchecked(vector.Y * scalar), unchecked(vector.Z * scalar));

    public static FixedVector3 operator /(FixedVector3 vector, int divisor) =>
        new(vector.X / divisor, vector.Y / divisor, vector.Z / divisor);

    // C# masks int shift counts to five bits, matching the original x86 shifts.
    public static FixedVector3 operator >>(FixedVector3 vector, int shift) =>
        new(vector.X >> shift, vector.Y >> shift, vector.Z >> shift);

    public static FixedVector3 operator <<(FixedVector3 vector, int shift) =>
        new(vector.X << shift, vector.Y << shift, vector.Z << shift);
}
