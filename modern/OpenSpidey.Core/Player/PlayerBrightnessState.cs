using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;

namespace OpenSpidey.Core.Player;

public interface IPlayerSurfaceBrightnessHost
{
    void AdjustBrightness(bool darkSurface, int elapsedTicks);
}

/// <summary>Actual source570/574/578,4BAD90 unsigned brightness ramp and the player SLight cache.</summary>
public sealed class PlayerBrightnessState
{
    public uint Current { get; set; } = 208;
    public uint DarkSurfaceTarget { get; private set; } = 160;
    public uint LightSurfaceTarget { get; private set; } = 256;
    public uint LastAppliedBrightness { get; set; } = uint.MaxValue;
    public SourceModelLight Light { get; private set; }
    public int LightUpdates { get; private set; }

    public PlayerBrightnessState(SourceModelLight light) => Light = light ?? throw new ArgumentNullException(nameof(light));
    public void SetDarkSurfaceTarget(ushort value) => DarkSurfaceTarget = value;
    public void SetLightSurfaceTarget(ushort value) => LightSurfaceTarget = value;
    public ushort SurfaceTarget(bool darkSurface) => unchecked((ushort)(darkSurface ? DarkSurfaceTarget : LightSurfaceTarget));

    public bool Adjust(ushort target, int elapsedTicks)
    {
        uint step = unchecked((uint)(8 * elapsedTicks));
        if (Current < target)
        {
            Current = unchecked(Current + step);
            if (Current > target) Current = target;
        }
        else if (Current > target)
        {
            Current = unchecked(Current - step);
            // The original PC clamps on unsigned underflow below the target; the recovered C++ comparison was reversed.
            if (Current < target) Current = target;
        }
        if (LastAppliedBrightness == Current) return false;
        uint brightness = Current;
        short Scaled(uint coefficient) => unchecked((short)(unchecked(coefficient * brightness) >> 8));
        short Multiple(uint coefficient) => unchecked((short)unchecked(coefficient * brightness));
        var colours = new FixedJointTransform(Scaled(3200), Scaled(1040), Multiple(8),
            Scaled(2720), Scaled(1600), Scaled(1920), Scaled(2400), Multiple(10), Multiple(8), 0, 0, 0);
        Light = Light with { Colours = colours,
            AmbientRed = unchecked((int)(unchecked(1800u * brightness) >> 8)),
            AmbientGreen = unchecked((int)(unchecked(1800u * brightness) >> 8)),
            AmbientBlue = unchecked((int)(unchecked(1440u * brightness) >> 8)) };
        LastAppliedBrightness = brightness;
        LightUpdates++;
        return true;
    }
}
