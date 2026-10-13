using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

/// <summary>Checked factory metadata; actor-specific constructor/script bytes remain available without pointer relocation.</summary>
public sealed record TriggerActorDefinition(ushort NodeId, ushort NodeKind, short ActorType,
    IReadOnlyList<ushort> Links, IReadOnlyList<byte> Markers, TriggerPosition Position,
    TriggerOrientation Orientation, int ObjectDataOffset, ReadOnlyMemory<byte> BehaviorData)
{
    public bool InitializeOnLoad => NodeKind == 1 && Markers.Contains((byte)1);
    public bool StartsSuspended => !Markers.Contains((byte)2);
    public bool ClearsBodyFlag2 => Markers.Contains((byte)4);

    public static TriggerActorDefinition Read(TriggerNode node)
    {
        if (node.TypeCode is not (1 or 7)) throw new InvalidDataException($"Node {node.Id} is not an actor factory record.");
        ReadOnlySpan<byte> bytes = node.Data.Span;
        Require(0, 8);
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes) != node.TypeCode)
            throw new InvalidDataException($"Node {node.Id}: actor kind does not match its header.");
        short actorType = BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]);
        int count = BinaryPrimitives.ReadInt16LittleEndian(bytes[6..]);
        if (count < 0) throw new InvalidDataException($"Node {node.Id}: negative actor link count.");
        Require(8, count * 2);
        var links = new ushort[count];
        for (int index = 0; index < count; index++) links[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(8 + index * 2)..]);
        int markerStart = 8 + count * 2;
        int terminator = bytes[markerStart..].IndexOf((byte)255);
        if (terminator < 0) throw new InvalidDataException($"Node {node.Id}: actor markers have no terminator.");
        byte[] markers = bytes.Slice(markerStart, terminator).ToArray();
        TriggerPlacement placement = TriggerPlacement.Read(node);
        int offset = placement.PositionOffset;
        TriggerOrientation orientation = placement.ReadOrientation(node);
        return new(node.Id, node.TypeCode, actorType, Array.AsReadOnly(links), Array.AsReadOnly(markers),
            placement.Position, orientation, offset, node.Data[(offset + 18)..]);

        void Require(int start, int length)
        {
            if (start < 0 || length < 0 || start > node.Data.Length || length > node.Data.Length - start)
                throw new InvalidDataException($"Node {node.Id}: actor data extends beyond its record.");
        }
    }
}
