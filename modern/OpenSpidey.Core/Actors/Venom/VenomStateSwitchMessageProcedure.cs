using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>Source persistent CAIProc_StateSwitchSendMessage (0x401E60), retiring after a changed AI state.</summary>
public sealed class VenomStateSwitchMessageProcedure(VenomState state, int message, Action<int> send) : IActorProcedure
{
    private readonly int initialState = state.AiState;
    public int SourceType => 0x60601;
    public int Flags { get; set; }
    public Action? ExecuteNext { get; set; }

    public void Execute(int elapsedTicks)
    {
        if (state.AiState != initialState)
        {
            send(message);
            Flags |= 1;
        }
        ExecuteNext?.Invoke();
    }
}
