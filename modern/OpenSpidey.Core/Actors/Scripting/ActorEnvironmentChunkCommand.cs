namespace OpenSpidey.Core.Actors.Scripting;

public interface IActorEnvironmentChunkHost
{
    void ChunkEnvironment(uint checksum);
}

/// <summary>CBaddy42B5: absolutely aligned direct checksum, whole source chunk producer, then commit field24C.</summary>
public static class ActorEnvironmentChunkCommand
{
    public static bool TryExecute(ushort opcode, ActorScriptCursor operands, IActorEnvironmentChunkHost host)
    {
        if (opcode != 0x42B5) return false;
        ArgumentNullException.ThrowIfNull(operands); ArgumentNullException.ThrowIfNull(host);
        ActorScriptCursor aligned = operands.Clone();
        uint checksum = aligned.ReadAlignedDword();
        host.ChunkEnvironment(checksum);
        // Original40647D writes field24C after4273D0 returns, including at callback/constructor boundaries.
        operands.Seek(aligned.Position);
        return true;
    }
}
