using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Simby;

/// <summary>Source persistent CAIProc_StateSwitchSendMessage (0x401E60) on CSimby, retiring after a changed state.</summary>
public sealed class SimbyStateSwitchMessageProcedure : IActorProcedure
{
    private readonly SimbyState state;
    private readonly int initialState;
    private readonly int message;
    private readonly Action<int> send;
    public int SourceType => 0x60601;
    public int Flags { get; set; }
    public Action? ExecuteNext { get; set; }

    public SimbyStateSwitchMessageProcedure(SimbyState state, int message, Action<int> send)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(send);
        this.state = state;
        initialState = state.Stage;
        this.message = message;
        this.send = send;
    }

    public void Execute(int elapsedTicks)
    {
        if (state.Stage != initialState)
        {
            send(message);
            Flags |= 1;
        }
        ExecuteNext?.Invoke();
    }
}
