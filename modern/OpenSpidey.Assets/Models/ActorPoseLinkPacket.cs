using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

public readonly record struct ActorPoseLink(ushort Part, ushort ParentPart, short PivotX, short PivotY,
    short PivotZ, ushort ParentLink);

/// <summary>Bounded source SLink packet, with parent-link indices resolved by the original first-match scan.</summary>
public sealed record ActorPoseLinkPacket(ushort HeaderWord, IReadOnlyList<ActorPoseLink> Links)
{
    public static ActorPoseLinkPacket Read(ReadOnlySpan<byte> bytes, int partCount)
    {
        if (partCount <= 0 || partCount > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(partCount));
        if (bytes.Length < 4) throw new InvalidDataException("Pose-link packet header is truncated.");
        ushort header = Half(bytes, 0), count = Half(bytes, 2);
        if (count == 0 || count > (bytes.Length - 4) / 12) throw new InvalidDataException("Pose-link packet records are truncated or absent.");
        var links = new ActorPoseLink[count];
        for (int index = 0; index < count; index++)
        {
            int offset = 4 + index * 12;
            ushort part = Half(bytes, offset), parent = Half(bytes, offset + 2);
            if (part >= partCount || (parent != ushort.MaxValue && parent >= partCount))
                throw new InvalidDataException("Pose-link packet references a missing source part.");
            links[index] = new(part, parent, Signed(bytes, offset + 4), Signed(bytes, offset + 6), Signed(bytes, offset + 8), ushort.MaxValue);
        }
        var firstPartLinks = new Dictionary<ushort, ushort>();
        for (ushort index = 0; index < count; index++) firstPartLinks.TryAdd(links[index].Part, index);
        for (int index = 0; index < count; index++)
            if (firstPartLinks.TryGetValue(links[index].ParentPart, out ushort parent))
                links[index] = links[index] with { ParentLink = parent };
        return new(header, Array.AsReadOnly(links));
    }

    private static ushort Half(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static short Signed(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt16LittleEndian(bytes[offset..]);
}
