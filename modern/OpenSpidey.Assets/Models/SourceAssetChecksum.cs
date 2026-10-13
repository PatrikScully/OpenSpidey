using OpenSpidey.Assets.Archives;

namespace OpenSpidey.Assets.Models;

/// <summary>Original case-sensitive asset-name CRC state, before the usual final complement.</summary>
public static class SourceAssetChecksum
{
    public static uint Compute(ReadOnlySpan<byte> name) => ~Crc32.Compute(name);
}
