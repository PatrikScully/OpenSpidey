namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorScreenFlashHost
{
    void Screen(byte red, byte green, byte blue, int frames, byte importance, int sort);
}

/// <summary>Original CBaddy429C: five raw words, byte RGB, unsigned duration/sort and importance zero.</summary>
public static class ActorScreenFlashCommand
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, IActorScreenFlashHost host)
    {
        if (opcode != 0x429C) return false;
        ArgumentNullException.ThrowIfNull(operands);
        ArgumentNullException.ThrowIfNull(host);
        ActorScriptCursor read = operands.Clone();
        byte red = unchecked((byte)read.ReadWord());
        byte green = unchecked((byte)read.ReadWord());
        byte blue = unchecked((byte)read.ReadWord());
        ushort frames = read.ReadWord();
        ushort sort = read.ReadWord();
        host.Screen(red, green, blue, frames, 0, sort);
        // Original40644C rereads field24C after Flash_Screen before adding ten bytes.
        operands.Seek(checked(operands.Position + 10));
        return true;
    }
}
