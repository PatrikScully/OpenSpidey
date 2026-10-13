using System.Buffers.Binary;
using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Assets.Models;

/// <summary>Read-only original Black Cat pose linkage; executable addresses stay at this import boundary.</summary>
public sealed record OriginalBlackCatData(ActorPoseLinkPacket PoseLinks, SourceModelLight Light)
{
    public static OriginalBlackCatData Read(ReadOnlyMemory<byte> image, int partCount)
    {
        OriginalExecutableData data = new(image);
        ReadOnlyMemory<byte> header = data.Slice(0x548660, 4);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(header.Span[2..]);
        if (count != 16) throw new InvalidDataException("Original Black Cat data requires the source sixteen-link pose packet.");
        return new(ActorPoseLinkPacket.Read(data.Slice(0x548660, checked(4 + count * 12)).Span, partCount),
            SourceModelLight.Read(data.Slice(0x548728, 52).Span));
    }
}
