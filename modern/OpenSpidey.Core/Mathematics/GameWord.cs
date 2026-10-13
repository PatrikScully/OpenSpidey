namespace OpenSpidey.Core.Mathematics;

public static class GameWord
{
    /// <summary>Recovered signed absolute value, including wrapped INT32_MIN.</summary>
    public static int Absolute(int value) => unchecked((value ^ (value >> 31)) - (value >> 31));

    /// <summary>Original MSVC _ftol: truncate through signed 64 bits, then retain the low word.</summary>
    public static int TruncateToInt32(float value)
    {
        const float limit = 9223372036854775808.0f;
        // Invalid x87 conversions return INT64_MIN, whose low 32 bits are zero.
        if (!(value >= -limit && value < limit)) return 0;
        return unchecked((int)(long)value);
    }

    public static int TruncateToInt32(double value)
    {
        const double limit = 9223372036854775808.0;
        if (!(value >= -limit && value < limit)) return 0;
        return unchecked((int)(long)value);
    }
}
