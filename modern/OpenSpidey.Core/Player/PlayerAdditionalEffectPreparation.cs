namespace OpenSpidey.Core.Player;

public readonly record struct PlayerAdditionalBodyCreation(PlayerKickHandle Handle, PlayerAdditionalBody Body);

public interface IPlayerAdditionalEffectPreparationHost
{
    PlayerAdditionalBodyCheats Cheats { get; }
    bool VibrationEnabled { get; }
    ushort ActuatorTime { get; }
    uint PlaySenseSound();
    void StopSound(uint handle);
    void ActivateSenseActuator(ushort duration);
    PlayerAdditionalBody? Recover(ref PlayerKickHandle handle);
    PlayerAdditionalBodyCreation CreateSense();
    PlayerAdditionalBodyCreation CreateFist(int slot);
    void Delete(PlayerAdditionalBody body);
}

/// <summary>Source preparation's actual ECC sense countdown/audio/body and 5AC/5B0 two-fist growth/shrink, before first-ground/web regeneration.</summary>
public static class PlayerAdditionalEffectPreparation
{
    public static void Prepare(PlayerCombatState combat, PlayerAdditionalEffectState state, PlayerWaterEffectState water,
        IPlayerAdditionalEffectPreparationHost host)
    {
        if (state.SenseTicks != 0)
        {
            state.SenseTicks = unchecked(state.SenseTicks - combat.ElapsedTicks);
            if (state.SenseTicks <= 0) state.SenseTicks = 0;
            else
            {
                if (state.SenseKind == 1) water.Value = 1;
                if (state.SenseSoundHandle == 0) state.SenseSoundHandle = host.PlaySenseSound();
                if (host.ActuatorTime < state.SenseTicks && host.VibrationEnabled)
                    host.ActivateSenseActuator(unchecked((ushort)state.SenseTicks));
                PlayerKickHandle handle = state.SenseBody;
                PlayerAdditionalBody? body = host.Recover(ref handle);
                state.SenseBody = handle;
                if (body is null) state.SenseBody = host.CreateSense().Handle;
            }
        }
        if (state.SenseTicks == 0)
        {
            if (state.SenseSoundHandle != 0) { host.StopSound(state.SenseSoundHandle); state.SenseSoundHandle = 0; }
            PlayerKickHandle handle = state.SenseBody;
            PlayerAdditionalBody? body = host.Recover(ref handle);
            state.SenseBody = handle;
            if (body is not null) { host.Delete(body); state.SenseBody = default; }
        }
        if (combat.Field5AC != 0)
        {
            combat.Field5B0 = combat.Field5B0 < 8192 ? unchecked(combat.Field5B0 + combat.ElapsedTicks * 384) : 8192;
            for (int slot = 0; slot < state.BonusFists.Length; slot++)
            {
                PlayerKickHandle handle = state.BonusFists[slot];
                PlayerAdditionalBody? body = host.Recover(ref handle);
                state.BonusFists[slot] = handle;
                if (body is null)
                {
                    PlayerAdditionalBodyCreation created = host.CreateFist(slot);
                    state.BonusFists[slot] = created.Handle;
                    body = created.Body;
                }
                if (combat.Field5B0 < 8192) PlayerAdditionalBodyRules.SetFistScale(body, combat.Field5B0 / 2, host.Cheats, true);
            }
        }
        else if (combat.Field5B0 > 0)
        {
            combat.Field5B0 = unchecked(combat.Field5B0 - combat.ElapsedTicks * 512);
            if (combat.Field5B0 < 0) combat.Field5B0 = 0;
            int half = combat.Field5B0 / 2;
            for (int slot = 0; slot < state.BonusFists.Length; slot++)
            {
                PlayerKickHandle handle = state.BonusFists[slot];
                PlayerAdditionalBody? body = host.Recover(ref handle);
                state.BonusFists[slot] = handle;
                if (body is null) continue;
                if (half != 0) PlayerAdditionalBodyRules.SetFistScale(body, half, host.Cheats, false);
                else { host.Delete(body); state.BonusFists[slot] = handle with { Target = null }; }
            }
        }
    }
}
