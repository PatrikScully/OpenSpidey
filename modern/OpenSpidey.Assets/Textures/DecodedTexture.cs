namespace OpenSpidey.Assets.Textures;

public sealed record DecodedTexture(uint Checksum, int Width, int Height, uint Flags, byte[] RgbaPixels)
{
    public uint SourceFormat { get; init; }
}
