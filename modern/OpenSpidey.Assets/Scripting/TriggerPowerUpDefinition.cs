using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

/// <summary>Authored kind5/20 power-up factory data, retaining absolute position alignment and unsigned lifetime/signal words.</summary>
public sealed record TriggerPowerUpDefinition(ushort NodeId, ushort NodeKind, short Type,
    IReadOnlyList<ushort> Links, TriggerPosition Position, uint Flags, ushort Lifetime, ushort SignalNode, int PositionOffset)
{
    public static TriggerPowerUpDefinition Read(TriggerNode node)
    {
        if (node.TypeCode is not (5 or 20)) throw new InvalidDataException($"Node {node.Id} is not an authored power-up record.");
        ReadOnlySpan<byte> bytes = node.Data.Span;
        if (bytes.Length < 6 || BinaryPrimitives.ReadUInt16LittleEndian(bytes) != node.TypeCode)
            throw new InvalidDataException("Truncated or mismatched power-up header.");
        short type = BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]);
        if (count > (bytes.Length - 6) / 2) throw new InvalidDataException("Truncated power-up links.");
        var links = new ushort[count];
        for (int index = 0; index < count; index++) links[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(6 + index * 2)..]);
        TriggerPlacement placement = TriggerPlacement.Read(node);
        int end = placement.AfterPositionOffset;
        int required = node.TypeCode == 20 ? 8 : 6;
        if (end > bytes.Length - required) throw new InvalidDataException("Truncated original power-up constructor parameters.");
        short extraFlag = BinaryPrimitives.ReadInt16LittleEndian(bytes[(end + 2)..]);
        ushort lifetime = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(end + 4)..]);
        ushort signal = node.TypeCode == 20 ? BinaryPrimitives.ReadUInt16LittleEndian(bytes[(end + 6)..]) : ushort.MaxValue;
        return new(node.Id, node.TypeCode, type, Array.AsReadOnly(links), placement.Position,
            extraFlag != 0 ? 0u : 4u, lifetime, signal, placement.PositionOffset);
    }
}
