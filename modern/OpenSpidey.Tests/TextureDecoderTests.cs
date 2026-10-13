using System.Buffers.Binary;
using OpenSpidey.Assets.Textures;

internal static class TextureDecoderTests
{
    public static void Run(Action<bool, string> assert)
    {
        byte[] expected = [255, 0, 0, 255, 0, 255, 0, 255, 0, 0, 255, 255, 255, 255, 255, 255];
        byte[] linear = Words(0xf800, 0x07e0, 0x001f, 0xffff);
        assert(PvrDecoder.Decode(2, 2, 0x901, linear).SequenceEqual(expected), "Linear RGB565 channel order.");
        byte[] twiddled = Words(0xf800, 0x001f, 0x07e0, 0xffff);
        assert(PvrDecoder.Decode(2, 2, 0x101, twiddled).SequenceEqual(expected), "Twiddled axis ordering.");
        byte[] codes = new byte[2049];
        twiddled.CopyTo(codes, 5 * 8); codes[2048] = 5;
        assert(PvrDecoder.Decode(2, 2, 0x301, codes).SequenceEqual(expected), "VQ codebook uses column-major 2x2 blocks.");
        byte[] rectangular = new byte[16];
        twiddled.CopyTo(rectangular, 0); twiddled.CopyTo(rectangular, 8);
        byte[] rectangle = PvrDecoder.Decode(4, 2, 0x101, rectangular);
        assert(rectangle.AsSpan(0, 8).SequenceEqual(expected.AsSpan(0, 8)) &&
            rectangle.AsSpan(8, 8).SequenceEqual(expected.AsSpan(0, 8)) &&
            rectangle.AsSpan(16, 8).SequenceEqual(expected.AsSpan(8, 8)), "Rectangular twiddled tile addressing.");

        byte[] mips = new byte[10 + 32];
        for (int index = 10; index < mips.Length; index += 2)
            BinaryPrimitives.WriteUInt16LittleEndian(mips.AsSpan(index, 2), 0xf800);
        assert(PvrDecoder.Decode(4, 4, 0x201, mips).Chunk(4).All(pixel => pixel.SequenceEqual(new byte[] { 255, 0, 0, 255 })),
            "Twiddled mip prefix is skipped before reading the largest level.");
        byte[] vqMips = new byte[2048 + 1 + 4];
        twiddled.CopyTo(vqMips, 5 * 8);
        Array.Fill(vqMips, (byte)5, 2049, 4);
        assert(PvrDecoder.Decode(4, 4, 0x401, vqMips).AsSpan(0, 8).SequenceEqual(expected.AsSpan(0, 8)),
            "VQ mip index prefix is skipped without moving the codebook.");
        byte[] alpha = PvrDecoder.Decode(2, 2, 0x900, Words(0x8000, 0, 0xffff, 0x7fff));
        assert(alpha[3] == 255 && alpha[7] == 0 && alpha[11] == 255 && alpha[15] == 0,
            "ARGB1555 alpha bit is independent of RGB.");
        byte[] rgba = PvrDecoder.Decode(2, 2, 0x902, Words(0xfabc, 0xfabc, 0xfabc, 0xfabc));
        assert(rgba.AsSpan(0, 4).SequenceEqual(new byte[] { 170, 187, 204, 255 }), "ARGB4444 expansion.");
        foreach (int colors in new[] { 16, 256 })
        {
            DecodedTexture texture = PsxTextureReader.Read(PaletteFixture(colors)).Single();
            assert(texture.Width == 3 && texture.RgbaPixels.SequenceEqual(expected.AsSpan(0, 12).ToArray()),
                $"PSX {colors}-color nibble/index order, palette channels and padded rows.");
        }
        try { PvrDecoder.Decode(2, 2, 0x301, new byte[2048]); throw new Exception("Truncated VQ pixels accepted."); }
        catch (InvalidDataException) { assert(true, "Truncated VQ index buffer rejected."); }
        try { PvrDecoder.Decode(3, 2, 0x101, linear); throw new Exception("Non-power-of-two PVR accepted."); }
        catch (InvalidDataException) { assert(true, "Non-power-of-two PVR rejected."); }
    }

    private static byte[] Words(params ushort[] words)
    {
        byte[] bytes = new byte[words.Length * 2];
        for (int index = 0; index < words.Length; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), words[index]);
        return bytes;
    }

    private static byte[] PaletteFixture(int colors)
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        writer.Write(0x20006u); writer.Write(16u); writer.Write(0u); writer.Write(0u);
        writer.Write(uint.MaxValue); // Packet terminator, then no model checksums.
        writer.Write(1u); writer.Write(0xabcu);
        foreach (int size in new[] { 16, 256 })
        {
            writer.Write(size == colors ? 1u : 0u);
            if (size != colors) continue;
            writer.Write(3u);
            for (int index = 0; index < size; index++)
                writer.Write((ushort)(index switch { 1 => 0x001f, 2 => 0x03e0, 3 => 0x7c00, _ => 0 }));
        }
        writer.Write(1u);
        writer.Write((uint)(output.Position + 4));
        writer.Write(0u); writer.Write((uint)colors); writer.Write(3u); writer.Write(0u);
        writer.Write((ushort)3); writer.Write((ushort)1);
        writer.Write(colors == 16 ? new byte[] { 0x21, 0x03 } : new byte[] { 1, 2, 3, 0 });
        return output.ToArray();
    }
}
