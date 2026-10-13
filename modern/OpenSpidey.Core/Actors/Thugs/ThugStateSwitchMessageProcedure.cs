using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Source persistent CAIProc_StateSwitchSendMessage, retiring after a changed state.</summary>
public sealed class ThugStateSwitchMessageProcedure : IActorProcedure
{
    private readonly ThugState state;
    private readonly int initialState;
    private readonly int message;
    private readonly Action<int> send;
    public int SourceType => 0x60601;
    public int Flags { get; set; }
    public Action? ExecuteNext { get; set; }

    public ThugStateSwitchMessageProcedure(ThugState state, int message, Action<int> send)
    {
        this.state = state;
        initialState = state.State;
        this.message = message;
        this.send = send;
    }

    public void Execute(int elapsedTicks)
    {
        if (state.State != initialState)
        {
            send(message);
            Flags |= 1;
        }
        ExecuteNext?.Invoke();
    }
}
