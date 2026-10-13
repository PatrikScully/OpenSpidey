using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Assets.Scripting;

/// <summary>Whole world units, before the original runtime's shift to Q12.</summary>
public readonly record struct TriggerPosition(int X, int Y, int Z);

public readonly record struct TriggerOrientation(short X, short Y, short Z);

/// <summary>A checkpoint's spawn metadata and the start of its mission command list.</summary>
public sealed record RestartPoint(ushort NodeId, string Name, TriggerPosition Position,
    TriggerOrientation Orientation, IReadOnlyList<ushort> Links, int CommandOffset)
{
    public static RestartPoint Read(TriggerNode node)
    {
        if (node.TypeCode != (ushort)TriggerNodeKind.Restart)
            throw new InvalidDataException($"Node {node.Id} is not a restart point.");

        ReadOnlySpan<byte> bytes = node.Data.Span;
        Require(node, 0, 4);
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes) != node.TypeCode)
            throw new InvalidDataException($"Node {node.Id} has inconsistent restart metadata.");
        int linkCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]);
        Require(node, 4, linkCount * 2);
        var links = new ushort[linkCount];
        for (int index = 0; index < links.Length; index++)
            links[index] = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(4 + index * 2)..]);

        TriggerPlacement placement = TriggerPlacement.Read(node);
        TriggerOrientation orientation = placement.ReadOrientation(node);
        int offset = placement.AfterPositionOffset + 6;
        int nameLength = bytes[offset..].IndexOf((byte)0);
        if (nameLength < 0)
            throw new InvalidDataException($"Node {node.Id} has an unterminated restart name.");
        string name = Encoding.Latin1.GetString(bytes.Slice(offset, nameLength));
        int commandOffset = Align(node, offset + nameLength + 1, 2);
        Require(node, commandOffset, 2); // Even an empty command list has its FFFF terminator.
        return new(node.Id, name, placement.Position, orientation, Array.AsReadOnly(links), commandOffset);
    }

    private static int Align(TriggerNode node, int offset, int alignment)
    {
        int absolute = checked(node.SourceOffset + offset);
        int aligned = checked(offset + (alignment - absolute % alignment) % alignment);
        Require(node, offset, aligned - offset);
        return aligned;
    }

    private static void Require(TriggerNode node, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > node.Data.Length || count > node.Data.Length - offset)
            throw new InvalidDataException($"Node {node.Id}, file offset 0x{(long)node.SourceOffset + offset:X}: Restart metadata extends beyond the node.");
    }
}
