namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::EnterWaitState (0x4ECED0) and ExitWaitState (0x4ECF00).</summary>
public static class VenomWaitState
{
    public static void Enter(VenomState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Field3B4 = 0;
        if (state.AiState != 1)
        {
            state.AiState = 1;
            state.Substate = 0;
        }
    }

    public static void Exit(VenomState state, ushort command, ushort operand)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Field3B4 = 0;
        if (state.AiState == 1)
        {
            state.Field3B0 = command;
            if (command == 4) state.Field3B4 = operand;
        }
    }
}
