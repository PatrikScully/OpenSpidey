namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorPlayerScriptHost
{
    void SetIgnoreInputTimer(ushort frames);
    /// <summary>Returns bytes consumed by the original embedded zero/FF stream walker.</summary>
    int SwitchToSynthesizedInput(ReadOnlyMemory<byte> source, int sourceOffset);
    /// <summary>CBaddy 0x42BA: MechList player +0x8EB = (operand != 0).</summary>
    void SetMechListField8EB(bool enabled);
}

/// <summary>CScriptOnlyBaddy 0x42BA / 0x470D / 0x470E leaves used by control and baddy scripts.</summary>
public static class ActorPlayerScriptCommands
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, IActorPlayerScriptHost host)
    {
        if (opcode is not (0x42BA or 0x470D or 0x470E)) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(host);
        if (opcode == 0x42BA)
        {
            ushort value = operands.ReadWord();
            host.SetMechListField8EB(value != 0);
            return true;
        }
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
