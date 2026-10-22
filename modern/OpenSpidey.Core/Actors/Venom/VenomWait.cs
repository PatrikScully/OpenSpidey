namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::Wait (0x4ED590): dumbAssPad substates 0/1 on bothFlags==1.</summary>
public static class VenomWait
{
    public static void Tick(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.Substate != 0)
        {
            if (state.Substate != 1)
                return;
            TickSubstateOne(state, host);
            return;
        }

        int span = state.Field464 - state.Field460;
        int pick = state.Field460 + (span <= 0 ? 0 : host.Random(span));
        state.Health = unchecked((short)pick);
        state.Substate = 1;
        state.Field218 &= ~7u;
        host.Body.Velocity = default;
        if ((state.Field218 & 0x100) != 0)
            return;
        state.Field218 &= 0xFFFFFF7Fu;
        host.RunAnimation(0xD);
        state.Field218 |= 0x100u;
        host.PlayPositionalSound(0x80F1);
    }

    private static void TickSubstateOne(VenomState state, IVenomHost host)
    {
        int command = state.Field3B0;
        if (command != 0)
        {
            if (command == 2)
            {
                state.AiState = 2;
                state.Substate = 0;
                state.Field3B0 = 0;
                return;
            }

            if (command == 4)
            {
                state.AiState = 4;
                state.Substate = 0;
            }

            state.Field3B0 = 0;
            return;
        }

        if ((state.Field218 & 0x100) != 0)
            return;

        int remaining = state.Health - host.ElapsedTicks;
        if (remaining > 0)
        {
            state.Health = unchecked((short)remaining);
            return;
        }

        state.Health = 0;
        switch (host.LevelId)
        {
            case 0x502:
                state.AiState = 2;
                state.Substate = 0;
                return;
            case 0x604:
                TransitionL6A4Wait(state, host);
                return;
            case 0x603:
                state.Health = 1800;
                return;
        }
    }

    private static void TransitionL6A4Wait(VenomState state, IVenomHost host)
    {
        int threshold = host.DifficultyLevel switch
        {
            2 => 33,
            0 or 1 => 50,
            _ => 20
        };
        int roll = host.Random(100);
        if (roll < 33)
        {
            state.AiState = 2;
            state.Substate = 0;
            return;
        }

        if (roll >= threshold + 33 && state.Field3B8 != 0)
        {
            state.AiState = 8;
            state.Substate = 0;
            return;
        }

        state.AiState = 4;
        state.Substate = 0;
    }
}
