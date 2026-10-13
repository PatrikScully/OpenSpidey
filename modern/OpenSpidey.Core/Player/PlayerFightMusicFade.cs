namespace OpenSpidey.Core.Player;

/// <summary>CPlayer constructor's C5C level gate and exact pre-input C64 gain step at4C66E6.</summary>
public static class PlayerFightMusicFade
{
    public static void InitializeLevel(PlayerCombatState state, int levelId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (levelId is 0x202 or 0x401 or 0x501 or 0x502 or 0x505 or 0x601 or 0x602 or 0x603 or
            0x702 or 0x704 or 0x705 or 0x804 or 0x805 or 0x806) state.FightMusicEnabled = 0;
    }

    public static void Advance(PlayerCombatState state, int elapsedTicks)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FightMusicEnabled == 0) return;
        if (state.FightMusicFadeDown != 0)
        {
            int gain = unchecked(state.FightMusicGain - elapsedTicks * 64);
            state.FightMusicGain = gain < 0 ? 0 : gain;
        }
        else if (state.FightMusicFadeUp != 0)
        {
            int gain = unchecked(state.FightMusicGain + elapsedTicks * 64);
            state.FightMusicGain = gain > 4096 ? 4096 : gain;
        }
    }
}
