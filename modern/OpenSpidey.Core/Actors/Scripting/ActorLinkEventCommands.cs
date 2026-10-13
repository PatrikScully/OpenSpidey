namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorLinkEventHost
{
    IReadOnlyList<ushort> Links(ushort nodeId);
    void SendSignal(ushort nodeId);
    void SendPulse(ushort nodeId);
}

/// <summary>Inherited42B1/42B2 resolve one node operand, then dispatch its actual links in file order.</summary>
public static class ActorLinkEventCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable, IActorLinkEventHost host)
    {
        if (opcode is not (0x42B1 or 0x42B2)) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(readVariable); ArgumentNullException.ThrowIfNull(host);
        ushort nodeId = operands.ReadWord();
        //405EAF masks the variable result to16bits before Trig_GetLinksPointer.
        if ((nodeId & 0x2000) != 0) nodeId = unchecked((ushort)readVariable(nodeId, operands));
        foreach (ushort target in host.Links(nodeId))
        {
            if (opcode == 0x42B1) host.SendSignal(target);
            else host.SendPulse(target);
        }
        return true;
    }

    /// <summary>Trig_SendSignalToLinks ignores other node kinds and visits only its three active source heads.</summary>
    public static void SendSignal(ushort nodeId, ushort nodeKind, IEnumerable<ActorScriptState> baddies,
        IEnumerable<ActorScriptState> controls, IEnumerable<ActorScriptState> environment)
    {
        if (nodeKind is not (1 or 7)) return;
        foreach (ActorScriptState state in baddies.Concat(controls).Concat(environment))
            if (state.NodeId == nodeId) state.PendingSignals |= 1;
    }
}
