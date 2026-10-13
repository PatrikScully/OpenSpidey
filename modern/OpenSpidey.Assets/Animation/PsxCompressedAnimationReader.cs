using System.Buffers.Binary;

namespace OpenSpidey.Assets.Animation;

/// <summary>Reads the type-44 channel packet and its HIER skeleton. Type-42 matrix clips use a separate format.</summary>
public static class PsxCompressedAnimationReader
{
    public static CompressedAnimationLibrary? Read(ReadOnlySpan<byte> source)
    {
        int parts = PsxAnimationPackets.ReadPartCount(source);
        ReadOnlySpan<byte> animation = PsxAnimationPackets.Find(source, 44);
        if (animation.IsEmpty) return null;
        ReadOnlySpan<byte> hierarchy = PsxAnimationPackets.Find(source, 0x52454948);
        if (parts is <= 0 or > ushort.MaxValue) throw new InvalidDataException("Invalid skeleton part count.");
        Require(hierarchy, 0, checked(parts * 2));
        var parents = new ushort[parts];
        for (int part = 0; part < parts; part++)
            parents[part] = BinaryPrimitives.ReadUInt16LittleEndian(hierarchy[(part * 2)..]);
        ushort[] order = BuildCalculationOrder(parents);

        int count = checked((int)Word(animation, 0));
        int tableEnd = checked(4 + count * 8);
        Require(animation, 0, tableEnd);
        if (count > ushort.MaxValue) throw new InvalidDataException("Compressed clip count exceeds original animation IDs.");
        var offsets = new int[count];
        var frames = new int[count];
        for (int clip = 0; clip < count; clip++)
        {
            offsets[clip] = checked((int)Word(animation, 4 + clip * 8));
            frames[clip] = checked((int)Word(animation, 8 + clip * 8));
            if (offsets[clip] < tableEnd || offsets[clip] >= animation.Length || frames[clip] <= 0)
                throw new InvalidDataException($"Invalid compressed clip {clip} table entry.");
        }
        int[] boundaries = offsets.Append(animation.Length).Distinct().Order().ToArray();
        var clips = new CompressedAnimationClip[count];
        for (int clip = 0; clip < count; clip++)
        {
            int index = Array.BinarySearch(boundaries, offsets[clip]);
            int end = boundaries[index + 1];
            clips[clip] = new(clip, frames[clip], parts, animation.Slice(offsets[clip], end - offsets[clip]));
        }
        return new(Array.AsReadOnly(parents), Array.AsReadOnly(order), Array.AsReadOnly(clips));
    }

    private static ushort[] BuildCalculationOrder(ushort[] parents)
    {
        int root = -1;
        var children = new List<ushort>[parents.Length];
        for (int part = 0; part < parents.Length; part++) children[part] = [];
        for (int part = 0; part < parents.Length; part++)
        {
            int parent = parents[part];
            if (parent >= parents.Length) throw new InvalidDataException("Skeleton references a missing parent.");
            if (parent == part)
            {
                if (root >= 0) throw new InvalidDataException("Skeleton has multiple roots.");
                root = part;
            }
            else children[parent].Add((ushort)part);
        }
        if (root < 0) throw new InvalidDataException("Skeleton has no root.");
        var order = new List<ushort>(parents.Length) { (ushort)root };
        for (int index = 0; index < order.Count; index++) order.AddRange(children[order[index]]);
        if (order.Count != parents.Length) throw new InvalidDataException("Skeleton contains a disconnected parent cycle.");
        return order.ToArray();
    }

    private static uint Word(ReadOnlySpan<byte> source, int offset)
    {
        Require(source, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
    }

    private static void Require(ReadOnlySpan<byte> source, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > source.Length - count)
            throw new InvalidDataException("Animation packet range exceeds source data.");
    }
}
