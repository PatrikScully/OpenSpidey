namespace OpenSpidey.Core.Mathematics;

/// <summary>Per-axis shift counts used by the original velocity damping.</summary>
public readonly record struct AxisDamping(byte X, byte Y, byte Z);
