using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>Source persistent CAIProc_StateSwitchSendMessage (0x401E60) on CRhino, retiring after a changed state.</summary>
public sealed class RhinoStateSwitchMessageProcedure : IActorProcedure
{
    private readonly RhinoState state;
    private readonly int initialState;
    private readonly int message;
    private readonly Action<int> send;
    public int SourceType => 0x60601;
    public int Flags { get; set; }
    public Action? ExecuteNext { get; set; }

    public RhinoStateSwitchMessageProcedure(RhinoState state, int message, Action<int> send)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(send);
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
