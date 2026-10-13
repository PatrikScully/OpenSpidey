namespace OpenSpidey.Assets.Scripting;

/// <summary>Trig_ParseTRGFile assigns kind2/9 contacts the PC's shared SendPulse/FFFF program, not bytes after their ENV checksum.</summary>
public static class OriginalContactCommandProgram
{
    public const int SourceAddress = 0x005580C0;
    public const ushort SendPulseOpcode = 3;
    public const ushort Terminator = ushort.MaxValue;
    public static IReadOnlyList<TriggerInstruction> Instructions { get; } = Array.AsReadOnly(new[]
    {
        new TriggerInstruction(SendPulseOpcode, SourceAddress, ReadOnlyMemory<byte>.Empty)
        { SourceKind = TriggerInstructionSourceKind.OriginalExecutable }
    });
}
