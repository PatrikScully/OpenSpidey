using System.Buffers.Binary;

namespace OpenSpidey.Assets.Scripting;

/// <summary>The recovered shared node-position layout, retaining whole units and absolute file alignment.</summary>
public sealed record TriggerPlacement(TriggerPosition Position, int PositionOffset, int AfterPositionOffset)
{
    public static bool IsSupported(ushort kind) => kind is 1 or 7 or 5 or 20 or 500 or 501 or 3 or 8 or 10 or 12 or 13 or 1000 or 1001 or 1002;

    public static TriggerPlacement Read(TriggerNode node)
    {
        ReadOnlySpan<byte> bytes = node.Data.Span;
        Require(node, 0, 2);
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes) != node.TypeCode)
            throw new InvalidDataException($"Node {node.Id}: placement kind does not match its header.");
        int rawOffset;
        switch (node.TypeCode)
        {
            case 1: case 7:
                Require(node, 0, 8);
                int actorLinks = BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]);
                int markerStart = checked(8 + actorLinks * 2);
                Require(node, markerStart, 1);
                int markerLength = bytes[markerStart..].IndexOf((byte)255);
                if (markerLength < 0) throw new InvalidDataException($"Node {node.Id}: placement markers have no terminator.");
                rawOffset = checked(markerStart + markerLength + 4);
                break;
            case 5: case 20: case 1002:
                Require(node, 0, 6);
                rawOffset = checked(9 + BinaryPrimitives.ReadUInt16LittleEndian(bytes[4..]) * 2);
                break;
            case 500: case 501: rawOffset = 5; break;
            case 3: case 8: case 10: case 12: case 13: case 1000: case 1001:
                Require(node, 0, 4);
                rawOffset = checked(7 + BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]) * 2);
                break;
            default: throw new InvalidDataException($"Node {node.Id}: kind {node.TypeCode} has no position layout.");
        }
        // Trig_GetPosition rounds the derived absolute pointer down, including light-node headers.
        int offset = checked(((node.SourceOffset + rawOffset) & ~3) - node.SourceOffset);
        Require(node, offset, 12);
        var position = new TriggerPosition(BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]),
            BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 4)..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 8)..]));
        return new(position, offset, offset + 12);
    }

    public TriggerOrientation ReadOrientation(TriggerNode node)
    {
        Require(node, AfterPositionOffset, 6);
        ReadOnlySpan<byte> bytes = node.Data.Span[AfterPositionOffset..];
        return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]),
            BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
    }

    public TriggerScriptFragment ReadScriptAfterAngles(TriggerNode node)
    {
        int offset = checked(AfterPositionOffset + 6);
        Require(node, offset, 2);
        return new(node.Data[offset..], checked(node.SourceOffset + offset));
    }

    private static void Require(TriggerNode node, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > node.Data.Length || count > node.Data.Length - offset)
            throw new InvalidDataException($"Node {node.Id}: placement data extends beyond its record.");
    }
}

public sealed record TriggerScriptFragment(ReadOnlyMemory<byte> Data, int SourceOffset);
