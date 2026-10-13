namespace OpenSpidey.Assets.Scripting;

public enum TriggerInstructionSourceKind { TriggerFile, OriginalExecutable }

/// <summary>Original instruction ID and packed operands, with source coordinates for diagnostics.</summary>
public sealed record TriggerInstruction(ushort Opcode, int SourceOffset, ReadOnlyMemory<byte> Arguments)
{
    /// <summary>SourceOffset is a file coordinate for authored commands or a virtual address for a static executable program.</summary>
    public TriggerInstructionSourceKind SourceKind { get; init; }
}
