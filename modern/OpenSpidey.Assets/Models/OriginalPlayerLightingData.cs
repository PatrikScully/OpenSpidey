using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Assets.Models;

/// <summary>The genuine PC player SLight packet; runtime lighting retains managed matrices and channels without executable pointers.</summary>
public sealed record OriginalPlayerLightingData(SourceModelLight Light)
{
    public static OriginalPlayerLightingData Read(ReadOnlyMemory<byte> image) =>
        new(SourceModelLight.Read(new OriginalExecutableData(image).Slice(0x5559E0, 52).Span));
}
