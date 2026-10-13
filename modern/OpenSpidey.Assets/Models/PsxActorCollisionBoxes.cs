using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

/// <summary>Original SModel+12 bounding pairs: upper then lower on each axis, in whole world units.</summary>
public readonly record struct ActorPartBox(short UpperX, short LowerX, short UpperY, short LowerY, short UpperZ, short LowerZ);

public static class PsxActorCollisionBoxes
{
    public static IReadOnlyList<ActorPartBox> Read(ReadOnlySpan<byte> source)
    {
        Require(source, 0, 12);
        uint version = BinaryPrimitives.ReadUInt32LittleEndian(source);
        if (version is not (0x20003 or 0x20004 or 0x20006)) throw new InvalidDataException("Unsupported actor collision PSX version.");
        int parts = Word(source, 8);
        if (parts is <= 0 or > ushort.MaxValue) throw new InvalidDataException("Invalid actor collision part count.");
        int table = checked(12 + parts * 36);
        int models = Word(source, table);
        if (models < parts) throw new InvalidDataException("Actor collision model table omits pose parts.");
        if (models > (source.Length - table - 4) / 4)
            throw new InvalidDataException("Actor collision model pointer table exceeds source data.");
        Require(source, table + 4, models * 4);
        var boxes = new ActorPartBox[parts];
        for (int part = 0; part < parts; part++)
        {
            int model = Word(source, table + 4 + part * 4);
            Require(source, model, 28);
            ReadOnlySpan<byte> box = source.Slice(model + 12, 12);
            boxes[part] = new(Half(box, 0), Half(box, 2), Half(box, 4), Half(box, 6), Half(box, 8), Half(box, 10));
        }
        return Array.AsReadOnly(boxes);
    }

    private static short Half(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source[offset..]);
    private static int Word(ReadOnlySpan<byte> source, int offset)
    {
        Require(source, offset, 4);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
        if (value > int.MaxValue) throw new InvalidDataException("Actor collision table offset/count exceeds bounded source data.");
        return (int)value;
    }
    private static void Require(ReadOnlySpan<byte> source, int offset, int size)
    {
        if (offset < 0 || size < 0 || offset > source.Length - size)
            throw new InvalidDataException("Actor collision box record exceeds source data.");
    }
}
