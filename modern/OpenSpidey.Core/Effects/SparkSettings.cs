using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Process-owned Bit_SetSpark globals; each particle captures these at construction.</summary>
public sealed class SparkSettings
{
    public ActorAngles Trajectory { get; set; }
    public ActorAngles Cone { get; set; }
    public uint Size { get; set; } = 1;
    public bool SemiTransparent { get; set; } = true;
    public byte Red { get; set; } = 128;
    public byte Green { get; set; } = 128;
    public byte Blue { get; set; } = 128;
    public byte FadeRed { get; set; } = 4;
    public byte FadeGreen { get; set; } = 4;
    public byte FadeBlue { get; set; } = 4;
    public int AllocationUsage { get; set; }
}

public readonly record struct SparkSpawnRequest(FixedVector3 Position, int Speed, int VerticalAcceleration, int MaximumLifetime);
