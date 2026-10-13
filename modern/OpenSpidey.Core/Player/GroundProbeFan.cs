namespace OpenSpidey.Core.Player;

public readonly record struct GroundProbePoint(float X, float Z);

/// <summary>Original wide-ground probe order: eight surrounding points, then the center again.</summary>
public static class GroundProbeFan
{
    public static GroundProbePoint Point(float x, float z, float spread, uint index)
    {
        float half = spread * 0.5f;
        return index switch
        {
            0 => new(x + spread, z),
            1 => new(x - spread, z),
            2 => new(x, z + spread),
            3 => new(x, z - spread),
            4 => new(half + x, half + z),
            5 => new(half + x, z - half),
            6 => new(x - half, half + z),
            7 => new(x - half, z - half),
            _ => new(x, z)
        };
    }
}
