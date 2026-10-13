using System.Buffers.Binary;

namespace OpenSpidey.Assets.Fonts;

/// <summary>Font::Font(43E140) and PCTex_CreateTexture16's source palette/nibble expansion.</summary>
public static class OriginalBitmapFontReader
{
    public static OriginalBitmapFont Read(ReadOnlySpan<byte> bytes, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Require(bytes, 0, 4);
        int count = Word(bytes, 0);
        if (count is < 1 or > 254) throw new InvalidDataException("Original byte-indexed font requires1..254 glyphs.");
        int paletteOffset = checked(4 + 16 * count);
        Require(bytes, paletteOffset, 32);
        var palette = new ushort[16];
        for (int i = 0; i < 16; i++) palette[i] = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(paletteOffset + 2 * i, 2));
        var glyphs = new BitmapFontGlyph[count + 1];
        int cursor = paletteOffset + 32;
        for (int index = 0; index < count; index++)
        {
            int record = 4 + index * 16;
            int sliceWidth = Word(bytes, record), height = Word(bytes, record + 4);
            int baseline = Word(bytes, record + 8), width = Word(bytes, record + 12);
            if (sliceWidth is < 1 or > 160 || height is < 0 or > 255 || baseline is < 0 or > 255 || width is < 0 or > 255)
                throw new InvalidDataException("Original glyph dimensions exceed their source image/byte metric limits.");
            int length = checked(sliceWidth * height * 2);
            Require(bytes, cursor, length);
            byte[] packed = bytes.Slice(cursor, length).ToArray();
            glyphs[index] = Decode(index, sliceWidth, (byte)width, (byte)height, (byte)baseline, packed, palette, false);
            cursor = checked(cursor + ((length + 3) & ~3));
            Require(bytes, cursor, 0);
        }
        BitmapFontGlyph first = glyphs[0];
        var fallback = new byte[first.PackedPixels.Length];
        Array.Fill(fallback, (byte)0x12);
        glyphs[count] = Decode(count, first.SliceWidth, first.Width, first.Height, first.Baseline, fallback, palette, true);
        return new(name, palette, glyphs);
    }

    private static BitmapFontGlyph Decode(int index, int sliceWidth, byte width, byte height, byte baseline,
        byte[] packed, ushort[] palette, bool fallback)
    {
        int textureWidth = sliceWidth * 4;
        var pixels = new byte[checked(textureWidth * height * 4)];
        // All shipped fonts have a mixture of transparent0 and bit15-set palette entries:
        // ConvertPSXPaletteToPC(flags1) selects ARGB1555 and treats every nonzero colour as opaque.
        bool alpha = palette.Any(colour => (colour & 0x8000) != 0) && palette.Any(colour => (colour & 0x8000) == 0);
        for (int pixel = 0; pixel < textureWidth * height; pixel++)
        {
            int sample = (packed[pixel / 2] >> ((pixel & 1) * 4)) & 15;
            ushort colour = palette[sample];
            int destination = pixel * 4;
            pixels[destination] = Expand5(colour);
            pixels[destination + 1] = Expand5(colour >> 5);
            pixels[destination + 2] = Expand5(colour >> 10);
            // The no-alpha branch creates RGB565; its six-bit green is reconstructed from expandedPSX5.
            if (!alpha)
            {
                int green8 = Expand5(colour >> 5), green6 = green8 >> 2;
                pixels[destination + 1] = (byte)((green6 << 2) | (green6 >> 4));
            }
            pixels[destination + 3] = alpha && colour == 0 ? (byte)0 : (byte)255;
        }
        return new(index, sliceWidth, width, height, baseline, packed, pixels, fallback);
    }
    private static byte Expand5(int value) { int channel = value & 31; return (byte)((channel << 3) | (channel >> 2)); }
    private static int Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException("Original font glyph data extends beyond its archive member.");
    }
}
