using System.Buffers.Binary;

namespace OpenSpidey.Assets.Textures;

public static class PsxTextureReader
{
    public static IReadOnlyList<DecodedTexture> Read(ReadOnlySpan<byte> bytes)
    {
        Require(bytes, 0, 12);
        int instances = checked((int)Word(bytes, 8));
        int modelCountOffset = checked(12 + instances * 36);
        Require(bytes, modelCountOffset, 4);
        int models = checked((int)Word(bytes, modelCountOffset));
        int offset = checked((int)Word(bytes, 4));
        if (offset == 0) return [];
        while (true)
        {
            Require(bytes, offset, 4);
            if (Word(bytes, offset) == uint.MaxValue) break;
            Require(bytes, offset, 8);
            int size = checked((int)Word(bytes, offset + 4));
            Require(bytes, offset + 8, size);
            offset = checked(offset + 8 + size);
        }
        offset = checked(offset + 4 + models * 4);
        Require(bytes, offset, 4);
        int count = checked((int)Word(bytes, offset));
        offset += 4;
        Require(bytes, offset, checked(count * 4));
        var checksums = new uint[count];
        for (int index = 0; index < count; index++) checksums[index] = Word(bytes, offset + index * 4);
        offset += count * 4;
        var palettes = new Dictionary<uint, ushort[]>();
        foreach (int colorCount in new[] { 16, 256 })
        {
            Require(bytes, offset, 4);
            int paletteCount = checked((int)Word(bytes, offset));
            offset += 4;
            int stride = 4 + colorCount * 2;
            Require(bytes, offset, checked(paletteCount * stride));
            for (int index = 0; index < paletteCount; index++, offset += stride)
            {
                uint checksum = Word(bytes, offset);
                var palette = new ushort[colorCount];
                for (int color = 0; color < colorCount; color++) palette[color] = Half(bytes, offset + 4 + color * 2);
                PsxPalette.Process(palette);
                palettes.TryAdd(checksum, palette);
            }
        }
        Require(bytes, offset, 4);
        int recordCount = checked((int)Word(bytes, offset));
        offset += 4;
        Require(bytes, offset, checked(recordCount * 4));
        var textures = new List<DecodedTexture>();
        for (int index = 0; index < recordCount; index++)
        {
            int record = checked((int)Word(bytes, offset + index * 4));
            Require(bytes, record, 20);
            uint flags = Word(bytes, record), format = Word(bytes, record + 4);
            uint paletteChecksum = Word(bytes, record + 8), textureIndex = Word(bytes, record + 12);
            int width = Half(bytes, record + 16), height = Half(bytes, record + 18);
            if (textureIndex >= checksums.Length || width < 1 || height < 1 || width > 2048 || height > 2048)
                throw new InvalidDataException("Invalid PSX texture header.");
            if ((flags & 0x80000000) != 0) throw new NotSupportedException("External PVR texture record.");
            byte[] pixels;
            if (format == 0x10000)
            {
                Require(bytes, record + 20, 8);
                pixels = PvrDecoder.Decode(width, height, Word(bytes, record + 20), bytes[(record + 28)..]);
            }
            else if (format is 16 or 256)
            {
                if (!palettes.TryGetValue(paletteChecksum, out ushort[]? palette) || palette.Length != format)
                    throw new InvalidDataException($"Missing PSX palette {paletteChecksum:X8}.");
                int stride = format == 16 ? ((width + 3) & ~3) / 2 : (width + 1) & ~1;
                Require(bytes, record + 20, checked(stride * height));
                pixels = new byte[width * height * 4];
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int sample = bytes[record + 20 + y * stride + (format == 16 ? x / 2 : x)];
                    if (format == 16) sample = (sample >> ((x & 1) * 4)) & 15;
                    ushort color = palette[sample];
                    int destination = (y * width + x) * 4;
                    pixels[destination] = PvrDecoder.Expand5(color);
                    pixels[destination + 1] = PvrDecoder.Expand5(color >> 5);
                    pixels[destination + 2] = PvrDecoder.Expand5(color >> 10);
                    pixels[destination + 3] = (byte)(color == 0 ? 0 : 255);
                }
            }
            else throw new NotSupportedException($"PSX color count {format}.");
            textures.Add(new(checksums[textureIndex], width, height, flags, pixels) { SourceFormat = format });
        }
        return textures.AsReadOnly();
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static ushort Half(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException("Texture data extends beyond the PSX file.");
    }
}
