using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

public enum TriggerNodeKind : ushort
{
    Object = 1,
    Autoexec = 4,
    CommandPoint = 6,
    Restart = 8,
    AlternativeAutoexec = 15,
    Terminator = 255
}

/// <summary>Immutable file-relative node data. Unknown node kinds remain available to the interpreter.</summary>
public sealed record TriggerNode(ushort Id, ushort TypeCode, int SourceOffset, ReadOnlyMemory<byte> Data);

public sealed record TriggerFile(IReadOnlyList<TriggerNode> Nodes)
{
    public static TriggerFile Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x4752545f)
            throw new InvalidDataException("Expected a _TRG file.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]) != 0x10002)
            throw new InvalidDataException("Unsupported Spider-Man trigger version.");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]);
        int tableEnd = checked(12 + count * 4);
        if (count == 0 || tableEnd > bytes.Length)
            throw new InvalidDataException("Invalid trigger node table.");

        var offsets = new int[count];
        for (int index = 0; index < count; index++)
        {
            uint offset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12 + index * 4, 4));
            if ((offset & 1) != 0 || offset < tableEnd || offset > bytes.Length - 2)
                throw new InvalidDataException("Trigger node offset is outside the file.");
            offsets[index] = (int)offset;
        }
        int[] ordered = offsets.Distinct().Order().Append(bytes.Length).ToArray();
        var nodes = new TriggerNode[count];
        for (int index = 0; index < count; index++)
        {
            int offset = offsets[index];
            int end = ordered[Array.BinarySearch(ordered, offset) + 1];
            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
            nodes[index] = new((ushort)index, type, offset, bytes.Slice(offset, end - offset).ToArray());
        }
        if (nodes[^1].TypeCode != (ushort)TriggerNodeKind.Terminator)
            throw new InvalidDataException("Trigger file is missing its final terminator node.");
        return new(Array.AsReadOnly(nodes));
    }
}
