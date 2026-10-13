namespace OpenSpidey.Core.Player;

public readonly record struct PlayerInputSuppressionStep(bool ClearButtons, bool BlockMovement, int? RestoredAnimationSpeed);

/// <summary>Recovered physical-input countdown, including exact-zero expiry and script-driven suspension.</summary>
public sealed class PlayerInputSuppression(Action? exitLookaround = null)
{
    public int RemainingTicks { get; private set; }
    public short SavedAnimationSpeed { get; private set; }

    public void Set(int duration, int animationSpeed, bool lookaroundActive = false)
    {
        RemainingTicks = duration;
        if (duration == 0) return;
        SavedAnimationSpeed = unchecked((short)animationSpeed);
        if (lookaroundActive)
        {
            if (exitLookaround is null) throw new NotSupportedException("Input suppression needs the active lookaround exit adapter.");
            exitLookaround();
        }
    }

    public PlayerInputSuppressionStep Advance(int ticks, bool scriptDriven)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        if (RemainingTicks == 0 || scriptDriven) return new(false, RemainingTicks != 0 && !scriptDriven, null);
        RemainingTicks = unchecked(RemainingTicks - ticks);
        int? restored = null;
        if (RemainingTicks < 0) { RemainingTicks = 0; restored = SavedAnimationSpeed; }
        return new(true, RemainingTicks != 0, restored);
    }
}
