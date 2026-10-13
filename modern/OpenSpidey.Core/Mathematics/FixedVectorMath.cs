namespace OpenSpidey.Core.Mathematics;

public static class FixedVectorMath
{
    public static int OriginalSquareRoot(int value)
    {
        if (value <= -32768) return 32768;
        if (value < 0) return 0;
        return (int)Math.Sqrt(value);
    }

    /// <summary>Utils_Dist shifts the signed difference before squaring; Utils_CrapDist is a separate approximation.</summary>
    public static int OriginalDistance(FixedVector3 first, FixedVector3 second)
    {
        FixedVector3 delta = (first - second) >> 12;
        return OriginalSquareRoot(unchecked(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z));
    }

    public static FixedVector3 Normalize(FixedVector3 vector)
    {
        // PC VectorNormal stores float inputs, retains x87 precision through the square sum,
        // then stores its length as float. These rounded integer inputs fit an exact ulong sum.
        long x = (long)(float)vector.X, y = (long)(float)vector.Y, z = (long)(float)vector.Z;
        ulong squared = (ulong)(x * x) + (ulong)(y * y) + (ulong)(z * z);
        float squaredLength = squared;
        if (squaredLength == 0) return new(0, 4096, 0);
        float length = (float)Math.Sqrt(squaredLength);
        // The x87 quotient reaches _ftol without an intervening float store.
        return new(GameWord.TruncateToInt32((double)(float)(vector.X << 12) / length),
            GameWord.TruncateToInt32((double)(float)(vector.Y << 12) / length),
            GameWord.TruncateToInt32((double)(float)(vector.Z << 12) / length));
    }
}
