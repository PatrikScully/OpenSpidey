using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Mathematics;

/// <summary>Recovered signed Q12 quaternion, retaining the original matrix and interpolation conventions.</summary>
public readonly record struct FixedQuaternion(int X, int Y, int Z, int W)
{
    public static FixedQuaternion Identity => new(0, 0, 0, 4096);

    public FixedJointTransform ToMatrix()
    {
        int xx = unchecked(X * X) >> 11, yy = unchecked(Y * Y) >> 11, zz = unchecked(Z * Z) >> 11;
        int xy = unchecked(Y * X) >> 11, xz = unchecked(Z * X) >> 11, yz = unchecked(Z * Y) >> 11;
        int xw = unchecked(X * W) >> 11, yw = unchecked(Y * W) >> 11, zw = unchecked(Z * W) >> 11;
        return new(unchecked((short)(4096 - zz - yy)), unchecked((short)(zw + xy)), unchecked((short)(xz - yw)),
            unchecked((short)(xy - zw)), unchecked((short)(4096 - xx - zz)), unchecked((short)(xw + yz)),
            unchecked((short)(yw + xz)), unchecked((short)(yz - xw)), unchecked((short)(4096 - xx - yy)), 0, 0, 0);
    }

    public static FixedQuaternion FromMatrix(FixedJointTransform matrix)
    {
        ReadOnlySpan<short> values = [matrix.M00, matrix.M01, matrix.M02, matrix.M10, matrix.M11, matrix.M12,
            matrix.M20, matrix.M21, matrix.M22];
        int trace = matrix.M00 + matrix.M11 + matrix.M22;
        if (trace > 0)
        {
            int root = FixedVectorMath.OriginalSquareRoot(unchecked((trace + 4096) << 12));
            int scale = 0x800000 / root;
            return new(unchecked((matrix.M12 - matrix.M21) * scale) >> 12,
                unchecked((matrix.M20 - matrix.M02) * scale) >> 12,
                unchecked((matrix.M01 - matrix.M10) * scale) >> 12, root >> 1);
        }
        int axis = matrix.M11 > matrix.M00 ? 1 : 0;
        if (matrix.M22 > values[axis * 3 + axis]) axis = 2;
        int next = (axis + 1) % 3, last = (next + 1) % 3;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked((values[axis * 3 + axis] -
            values[last * 3 + last] - values[next * 3 + next] + 4096) << 12));
        if (length == 0) throw new InvalidDataException("Camera matrix cannot be converted to a nonzero quaternion.");
        int factor = 0x800000 / length;
        Span<int> components = stackalloc int[3];
        components[axis] = length >> 1;
        components[next] = unchecked((values[axis * 3 + next] + values[next * 3 + axis]) * factor) >> 12;
        components[last] = unchecked((values[axis * 3 + last] + values[last * 3 + axis]) * factor) >> 12;
        int w = unchecked((values[next * 3 + last] - values[last * 3 + next]) * factor) >> 12;
        return new(components[0], components[1], components[2], w);
    }

    public static FixedQuaternion Slerp(ref FixedQuaternion start, FixedQuaternion target, int fraction)
    {
        // The recovered zero-sum guards do not return; subsequent interpolation overwrites their result.
        int dot = unchecked(target.X * start.X + start.Y * target.Y + target.Z * start.Z + target.W * start.W) >> 12;
        if (dot < 0)
        {
            start = new(unchecked(-start.X), unchecked(-start.Y), unchecked(-start.Z), unchecked(-start.W));
            dot = unchecked(-dot);
        }
        if (unchecked(dot + 4096) <= 128)
        {
            var perpendicular = new FixedQuaternion(unchecked(-start.Y), unchecked(-start.X), unchecked(-start.W), start.Z);
            int first = GameAngle.Heading(unchecked((4096 - fraction) * 6434) >> 12).Sine;
            int second = GameAngle.Heading(unchecked(fraction * 6434) >> 12).Sine;
            return Blend(start, perpendicular, first, second);
        }
        if (unchecked(4096 - dot) <= 128) return Blend(start, target, unchecked(4096 - fraction), fraction);
        float cosine = dot;
        float arc = (float)Math.Acos(cosine / 4096f);
        int angle = GameWord.TruncateToInt32(arc * 2048f / 3.1415927f);
        int sine = GameAngle.Heading(angle).Sine;
        if (sine == 0) throw new InvalidDataException("Quaternion interpolation has a zero sine denominator.");
        int firstWeight = unchecked(GameAngle.Heading(unchecked((4096 - fraction) * angle) >> 12).Sine << 12) / sine;
        int secondWeight = unchecked(GameAngle.Heading(unchecked(fraction * angle) >> 12).Sine << 12) / sine;
        return Blend(start, target, firstWeight, secondWeight);
    }

    private static FixedQuaternion Blend(FixedQuaternion first, FixedQuaternion second, int firstWeight, int secondWeight) =>
        new(unchecked(first.X * firstWeight + second.X * secondWeight) >> 12,
            unchecked(first.Y * firstWeight + second.Y * secondWeight) >> 12,
            unchecked(first.Z * firstWeight + second.Z * secondWeight) >> 12,
            unchecked(first.W * firstWeight + second.W * secondWeight) >> 12);
}
