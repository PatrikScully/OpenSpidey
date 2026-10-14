using System.Buffers.Binary;
using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Assets.Models;

/// <summary>Read-only original CTorch pose linkage and M3d_TorchLight; executable addresses stay at this import boundary.</summary>
public sealed record OriginalTorchData(ActorPoseLinkPacket PoseLinks, SourceModelLight Light)
{
    public static OriginalTorchData Read(ReadOnlyMemory<byte> image, int partCount)
    {
        OriginalExecutableData data = new(image);
        ReadOnlyMemory<byte> header = data.Slice(0x557F20, 4);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(header.Span[2..]);
        if (count != 16) throw new InvalidDataException("Original torch data requires the source sixteen-link pose packet.");
        return new(ActorPoseLinkPacket.Read(data.Slice(0x557F20, checked(4 + count * 12)).Span, partCount),
            SourceModelLight.Read(data.Slice(0x557FE8, 52).Span));
    }
}
