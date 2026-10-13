using System.Buffers.Binary;
using System.Numerics;

namespace OpenSpidey.Assets.Textures;

/// <summary>Dreamcast texture layouts embedded in the PC game's PSX files.</summary>
public static class PvrDecoder
{
    public static byte[] Decode(int width, int height, uint format, ReadOnlySpan<byte> source)
    {
        if (width < 2 || height < 2 || width > 2048 || height > 2048 ||
            !BitOperations.IsPow2(width) || !BitOperations.IsPow2(height))
            throw new InvalidDataException("Invalid PVR dimensions.");
        int layout = (int)(format & 0xff00);
        int colorFormat = (int)(format & 255);
        if (colorFormat is not (0 or 1 or 2))
            throw new NotSupportedException($"PVR color format {colorFormat}.");
        bool vectorQuantized = layout is 0x300 or 0x400;
        bool twiddled = layout is 0x100 or 0x200 or 0xd00;
        if (!vectorQuantized && !twiddled && layout != 0x900)
            throw new NotSupportedException($"PVR layout 0x{layout:X}.");
        int dataOffset = vectorQuantized ? 2048 : 0;
        if (layout == 0x200) dataOffset += MipOffset(width / 2, height / 2, 2);
        if (layout == 0x400 && (width != 2 || height != 2))
            dataOffset += MipOffset(width / 4, height / 4, 0.25f) + width * height / 16;
        int dataSize = vectorQuantized ? width * height / 4 : width * height * 2;
        if (dataOffset > source.Length || dataSize > source.Length - dataOffset)
            throw new InvalidDataException("Truncated PVR pixels.");
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int offset;
            if (vectorQuantized)
            {
                int index = source[dataOffset + MortonIndex(x / 2, y / 2, width / 2, height / 2)];
                offset = index * 8 + ((x & 1) * 2 + (y & 1)) * 2;
            }
            else offset = dataOffset + 2 * (twiddled ? MortonIndex(x, y, width, height) : y * width + x);
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));
            WriteColor(pixels.AsSpan((y * width + x) * 4, 4), value, colorFormat);
        }
        return pixels;
    }

    private static int MipOffset(int width, int height, float bytesPerPixel)
    {
        if (width != height || width < 1)
            throw new InvalidDataException("Mipmapped PVR textures must have a square mip chain.");
        int result = (int)bytesPerPixel;
        while (width > 1)
        {
            result += (int)(width * height * bytesPerPixel);
            width /= 2; height /= 2;
        }
        return result;
    }

    private static int MortonIndex(int x, int y, int width, int height)
    {
        int side = Math.Min(width, height), mask = side - 1;
        int shift = BitOperations.Log2((uint)side);
        return (((x | y) & ~mask) << shift) | Spread(x & mask) << 1 | Spread(y & mask);
    }

    private static int Spread(int value)
    {
        int result = 0;
        for (int bit = 0; value != 0; bit++, value >>= 1)
            result |= (value & 1) << (2 * bit);
        return result;
    }

    private static void WriteColor(Span<byte> output, ushort value, int format)
    {
        if (format == 2)
        {
            output[0] = (byte)(((value >> 8) & 15) * 17);
            output[1] = (byte)(((value >> 4) & 15) * 17);
            output[2] = (byte)((value & 15) * 17);
            output[3] = (byte)((value >> 12) * 17);
        }
        else
        {
            output[0] = Expand5(value >> (format == 1 ? 11 : 10));
            int green = (value >> 5) & (format == 1 ? 63 : 31);
            output[1] = format == 1 ? (byte)((green << 2) | (green >> 4)) : Expand5(green);
            output[2] = Expand5(value);
            output[3] = (byte)(format == 1 || (value & 0x8000) != 0 ? 255 : 0);
        }
    }

    internal static byte Expand5(int value) => (byte)(((value & 31) << 3) | ((value & 31) >> 2));
}
