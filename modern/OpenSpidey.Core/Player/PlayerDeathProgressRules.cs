namespace OpenSpidey.Core.Player;

public interface IPlayerDeathProgressHost
{
    void ResetInitializationFlag();
    void SetLevelStatus(int value);
    void ResetFlash();
}

/// <summary>Recovered dead action dispatch: source timer starts only after the death animation finishes.</summary>
public static class PlayerDeathProgressRules
{
    public static void Step(PlayerCombatState state, IPlayerDeathProgressHost host, bool animationFinished)
    {
        state.FieldEA6 = 0;
        int elapsed = 0;
        if (animationFinished)
        {
            if (state.DeathTimer >= 120)
            {
                host.ResetInitializationFlag();
                host.SetLevelStatus(2);
                host.ResetFlash();
            }
            elapsed = unchecked(state.DeathTimer + state.ElapsedTicks);
        }
        state.DeathTimer = elapsed;
    }
}
