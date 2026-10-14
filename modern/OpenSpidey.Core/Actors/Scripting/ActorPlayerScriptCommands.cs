namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorPlayerScriptHost
{
    void SetIgnoreInputTimer(ushort frames);
    /// <summary>Returns bytes consumed by the original embedded zero/FF stream walker.</summary>
    int SwitchToSynthesizedInput(ReadOnlyMemory<byte> source, int sourceOffset);
}

/// <summary>CScriptOnlyBaddy470D/470E: consume the timer before its call, commit the embedded stream cursor after its call.</summary>
public static class ActorPlayerScriptCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, IActorPlayerScriptHost host)
    {
        if (opcode is not (0x470D or 0x470E)) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(host);
        if (opcode == 0x470D)
        {
            ushort frames = operands.ReadWord();
            host.SetIgnoreInputTimer(frames);
        }
        else
        {
            int start = operands.Position;
            int consumed = host.SwitchToSynthesizedInput(operands.Source[start..], operands.AbsolutePosition);
            operands.Seek(checked(start + consumed));
        }
        return true;
    }
}
