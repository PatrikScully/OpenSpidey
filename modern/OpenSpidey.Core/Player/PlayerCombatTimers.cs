namespace OpenSpidey.Core.Player;

public interface IPlayerDamageTimerHost
{
    bool VibrationEnabled { get; }
    void ActuatorOn(int controller, ushort time, int argument, int strength);
    void UnElectrify();
}

/// <summary>Recovered updateDamageTimers, run during player frame preparation before physics and action dispatch.</summary>
public static class PlayerCombatTimers
{
    public static void Advance(PlayerCombatState state, IPlayerDamageTimerHost host)
    {
        if (state.Field36C != 0) state.Field36C = Math.Max(0, unchecked(state.Field36C - state.ElapsedTicks));
        if (state.Field534 != 0) state.Field534 = Math.Max(0, unchecked(state.Field534 - state.ElapsedTicks));
        if (state.ElectrificationTicks != 0)
        {
            if (host.VibrationEnabled) host.ActuatorOn(0, unchecked((ushort)state.ElectrificationTicks), 1, 0xDB);
            state.ElectrificationTicks = unchecked(state.ElectrificationTicks - state.ElapsedTicks);
            if (state.ElectrificationTicks <= 0)
            {
                state.ElectrificationTicks = 0;
                host.UnElectrify();
            }
        }
    }
}
