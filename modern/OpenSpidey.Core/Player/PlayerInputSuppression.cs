namespace OpenSpidey.Core.Player;

public readonly record struct PlayerInputSuppressionStep(bool ClearButtons, bool BlockMovement, int? RestoredAnimationSpeed);

/// <summary>Recovered physical-input countdown, including exact-zero expiry and script-driven suspension.</summary>
public sealed class PlayerInputSuppression(Action? exitLookaround = null)
{
    private PlayerCombatState? sourceState;
    private Action? exit = exitLookaround;
    private int remainingTicks;
    private short savedAnimationSpeed;
    public int RemainingTicks
    {
        get => sourceState?.FieldE18 ?? remainingTicks;
        private set { if (sourceState is { } state) state.FieldE18 = value; else remainingTicks = value; }
    }
    public short SavedAnimationSpeed
    {
        get => sourceState?.SavedInputAnimationSpeed ?? savedAnimationSpeed;
        private set { if (sourceState is { } state) state.SavedInputAnimationSpeed = value; else savedAnimationSpeed = value; }
    }

    /// <summary>Input decay, damage eligibility and the timer HUD all read the same original E18/E12 fields.</summary>
    public void BindSourceState(PlayerCombatState state, Action exitLookaround)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(exitLookaround);
        if (sourceState is not null) throw new InvalidOperationException("The source input countdown already has a player owner.");
        sourceState = state; exit = exitLookaround;
    }

    public void Set(int duration, int animationSpeed, bool lookaroundActive = false)
    {
        RemainingTicks = duration;
        if (duration == 0) return;
        SavedAnimationSpeed = unchecked((short)animationSpeed);
        if (lookaroundActive)
        {
            if (exit is null) throw new NotSupportedException("Input suppression needs the active lookaround exit adapter.");
            exit();
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
