using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

/// <summary>Original kind2/9 static ENV reference: source links followed by an absolutely aligned model checksum.</summary>
public static class TriggerEnvironmentReference
{
    public static uint ReadChecksum(TriggerNode node)
    {
        if (node.TypeCode is not (2 or 9))
            throw new InvalidDataException($"Node {node.Id}: kind{node.TypeCode} is not a static ENV reference.");
        ReadOnlySpan<byte> bytes = node.Data.Span;
        if (bytes.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != node.TypeCode)
            throw new InvalidDataException($"Node {node.Id}: truncated or mismatched static ENV header.");
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
        int offset = checked(4 + count * 2);
        offset = checked(offset + ((4 - ((node.SourceOffset + offset) & 3)) & 3));
        if (offset > bytes.Length - 4)
            throw new InvalidDataException($"Node {node.Id}: static ENV checksum extends beyond its source record.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    }
}
