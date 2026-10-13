namespace OpenSpidey.Core.Player;

/// <summary>Recovered updateKnockdownRecovery action dispatch after source animation follow-ons resolve.</summary>
public static class PlayerHitRecoveryRules
{
    public static void Step(PlayerCombatState state, IPlayerHitHost host, ushort completedAnimation)
    {
        state.FieldEA6 = 0;
        if (state.FlailDropChain != 0)
        {
            if (completedAnimation == 0xAF) PlayerHitRules.Play(state, host, 0xB1);
            else if (completedAnimation == 0xB1)
            {
                state.ActionState = 4;
                state.FlailDropChain = 0;
                return;
            }
        }
        if (state.AirborneTicks > 0x1E0) host.SetLevelStatus(8);
        if (state.AnimationId == 0x11D && state.AnimationFrame >= 7 && unchecked(host.Timer - state.Field5B4) > 0x3C)
        {
            state.Field5AC = 5;
            state.Field5B0 = 0;
            state.Field5B4 = host.Timer;
            host.PlayPositionSound(0x16, state.Position, 0);
        }
        if (completedAnimation is 0xB4 or 0xAE)
        {
            state.Field36C = 30;
            state.FieldAE5 = 0;
            host.SwitchToStandMode();
            return;
        }
        if (completedAnimation is 0xAA or 0xBB or 0xB5 or 0x11D)
        {
            host.SwitchToStandMode();
            return;
        }
        if (state.AnimationId is not (0xAF or 0xB0) || (state.CollisionFlags & 2) == 0) return;
        PlayerHitRules.Play(state, host, 0xB2);
        if (state.Health <= 0) PlayerDeathRules.Enter(state, host);
    }
}
