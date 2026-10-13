using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerAxes(sbyte Forward, sbyte Sideways);

public readonly record struct PlayerInputSample(bool AnalogAvailable, PlayerAxes Analog,
    bool Up, bool Down, bool Right, bool Left, bool LeftOnePressed, bool LeftTwoTriggered, PlayerAxes? Scripted = null);

public readonly record struct PlayerInputOptions(bool IgnorePhysicalInput, ushort CorrectionNumerator,
    ushort CorrectionDenominator, bool InvertPrimaryForward, bool InvertSecondaryForward);

public readonly record struct PlayerMovementInputState(PlayerAxes Axes, PlayerAxes PreviousAxes,
    int MovementRamp, short Heading, short ReferenceHeading, bool PrimaryCenterPending,
    bool SecondaryCenterPending, bool CenteredLatch);

public readonly record struct PlayerMovementInputResult(PlayerMovementInputState State, bool LeftOnePressed, bool LeftTwoTriggered);

/// <summary>Recovered physical/scripted movement input, including digital fallback and centering events.</summary>
public static class PlayerMovementInput
{
    public static PlayerMovementInputResult Read(PlayerMovementInputState state, PlayerInputSample sample, PlayerInputOptions options)
    {
        PlayerAxes axes = default;
        bool primary = state.PrimaryCenterPending;
        bool secondary = state.SecondaryCenterPending;
        bool centered = state.CenteredLatch;
        bool leftTwoTriggered = sample.LeftTwoTriggered;
        if (sample.Scripted is { } scripted) axes = scripted;
        else if (options.IgnorePhysicalInput)
        {
            return new(state with { Axes = default, PreviousAxes = state.Axes, MovementRamp = 0,
                PrimaryCenterPending = false, SecondaryCenterPending = false }, false, leftTwoTriggered);
        }
        else
        {
            if (sample.AnalogAvailable) axes = sample.Analog;
            if (axes == default)
                axes = new(DigitalAxis(sample.Up, sample.Down, -127), DigitalAxis(sample.Right, sample.Left, 127));
        }
        if (options.CorrectionNumerator != 0)
        {
            if (options.CorrectionDenominator == 0)
                throw new InvalidDataException("Player input correction has a zero denominator.");
            axes = new(Correct(axes.Forward, options), Correct(axes.Sideways, options));
        }
        if (primary && axes == default)
        {
            centered = true;
            primary = false;
            leftTwoTriggered = true;
        }
        else if (secondary && axes == default) secondary = false;
        if ((options.InvertPrimaryForward && primary) || (options.InvertSecondaryForward && secondary))
            axes = axes with { Forward = unchecked((sbyte)-axes.Forward) };
        int ramp = 0;
        short heading = state.ReferenceHeading;
        if (axes != default)
        {
            ramp = Math.Min(unchecked(state.MovementRamp + 32), 256);
            heading = (short)((state.ReferenceHeading - GameAngle.Atan2(-axes.Forward, axes.Sideways) + 1024) & 4095);
        }
        return new(new(axes, state.Axes, ramp, heading, state.ReferenceHeading, primary, secondary, centered),
            sample.LeftOnePressed, leftTwoTriggered);
    }

    private static sbyte Correct(sbyte axis, PlayerInputOptions options) =>
        unchecked((sbyte)(axis - unchecked((sbyte)(options.CorrectionNumerator * axis / options.CorrectionDenominator))));

    private static sbyte DigitalAxis(bool preferred, bool other, sbyte preferredValue)
    {
        if (preferred) return preferredValue;
        if (other) return (sbyte)-preferredValue;
        return 0;
    }

}
