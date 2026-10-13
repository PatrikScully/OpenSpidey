using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct LaserFlashParameters(FixedVector3 Position, ushort Scale, ushort HalfMask,
    byte Red, byte Green, byte Blue, byte Lifetime);

/// <summary>Actual Effects_LaserFlash: one16-section CGlowFlash, then the source angle and optional half-ring mask.</summary>
public static class LaserFlash
{
    public static GlowFlashState Create(LaserFlashParameters parameters, Func<int, int> random)
    {
        ArgumentNullException.ThrowIfNull(random);
        int scale = parameters.Scale;
        var state = new GlowFlashState(new(parameters.Position, 8, 255, 255, 255, 0,
            parameters.Red, parameters.Green, parameters.Blue, 0, 30 * scale >> 8, 0, 1,
            40 * scale >> 8, 130 * scale >> 8, 20 * scale >> 8, 65 * scale >> 8,
            4 * scale >> 8, parameters.Lifetime), random);
        if (parameters.HalfMask != 0)
        {
            state.Angle = unchecked((ushort)(random(401) - 1224));
            int half = state.Sections.Count >> 1;
            state.Mask = ((1u << half) - 1) & ~(1u << (half - 1)) & ~1u;
        }
        else state.Angle = unchecked((ushort)random(4096));
        return state;
    }
}
