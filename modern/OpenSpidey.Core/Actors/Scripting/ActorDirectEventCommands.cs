namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorDirectEventHost
{
    void SignalActors(ushort nodeId);
    void PulseNode(ushort nodeId);
}

/// <summary>Inherited42B3/42B4 resolve one actual node ID and dispatch directly, without reading its links or kind for signals.</summary>
public static class ActorDirectEventCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable, IActorDirectEventHost host)
    {
        if (opcode is not (0x42B3 or 0x42B4)) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(readVariable); ArgumentNullException.ThrowIfNull(host);
        ushort nodeId = operands.ReadWord();
        // The original MOVZX after virtual GetVariable preserves its low16bits.
        if ((nodeId & 0x2000) != 0) nodeId = unchecked((ushort)readVariable(nodeId, operands));
        if (opcode == 0x42B3) host.SignalActors(nodeId);
        else host.PulseNode(nodeId);
        return true;
    }

    /// <summary>Actual CBaddy dispatch calls raw Trig_SendSignal through Control, Baddy, then Environment heads.</summary>
    public static void SignalActors(ushort nodeId, IEnumerable<ActorScriptState> controls,
        IEnumerable<ActorScriptState> baddies, IEnumerable<ActorScriptState> environment)
    {
        foreach (ActorScriptState state in controls.Concat(baddies).Concat(environment))
            if (state.NodeId == nodeId) state.PendingSignals |= 1;
    }
}
