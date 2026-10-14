namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorCinematicScreenHost
{
    int WideScreenValue { get; set; }
    int WideScreenShadow { get; set; }
}

/// <summary>CBaddy42B9 writes the process value, advances the actor cursor, then copies the current value to the panel Y shadow.</summary>
public static class ActorCinematicScreenCommand
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, IActorCinematicScreenHost host)
    {
        if (opcode != 0x42B9) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(host);
        host.WideScreenValue = operands.PeekWord();
        operands.Seek(checked(operands.Position + 2));
        host.WideScreenShadow = host.WideScreenValue;
        return true;
    }
}
