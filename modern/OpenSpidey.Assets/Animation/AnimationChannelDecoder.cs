using System.Buffers.Binary;

namespace OpenSpidey.Assets.Animation;

/// <summary>Reads one original pose channel, returning the bytes consumed, including its header.</summary>
public static class AnimationChannelDecoder
{
    public static int Decode(ReadOnlySpan<byte> source, Span<short> destination, int sampleCount, int stride = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stride);
        if ((long)(sampleCount - 1) * stride >= destination.Length)
            throw new ArgumentException("Destination cannot hold the strided channel.", nameof(destination));
        if (source.IsEmpty) throw new InvalidDataException("Missing animation channel header.");

        int mode = source[0] & 15;
        int blockSize = (source[0] >> 4) + 1;
        int cursor = 1;
        short value = mode == 15 ? (short)0 : ReadSample(source, ref cursor);
        destination[0] = value;
        if (mode >= 14)
        {
            for (int frame = 1; frame < sampleCount; frame++) destination[frame * stride] = value;
            return cursor;
        }

        long bitCursor = 0;
        int bitstreamStart = cursor;
        int frameIndex = 1;
        while (frameIndex < sampleCount)
        {
            int length = Math.Min(blockSize, sampleCount - frameIndex);
            int delta;
            short endpoint;
            if (mode == 0)
            {
                endpoint = ReadSample(source, ref cursor);
                delta = endpoint - value;
            }
            else
            {
                delta = ReadSignedBits(source[bitstreamStart..], ref bitCursor, mode + 1);
                endpoint = unchecked((short)(value + delta));
            }

            // Preserve truncation towards zero and the original 16-bit wrap at each step.
            int step = delta / length;
            for (int index = 1; index < length; index++)
            {
                value = unchecked((short)(value + step));
                destination[frameIndex++ * stride] = value;
            }
            destination[frameIndex++ * stride] = endpoint;
            value = endpoint;
        }
        return mode == 0 ? cursor : checked(bitstreamStart + (int)((bitCursor + 7) / 8));
    }

    private static short ReadSample(ReadOnlySpan<byte> source, ref int cursor)
    {
        if (cursor > source.Length - 2) throw new InvalidDataException("Truncated animation channel sample.");
        short result = BinaryPrimitives.ReadInt16LittleEndian(source[cursor..]);
        cursor += 2;
        return result;
    }

    private static int ReadSignedBits(ReadOnlySpan<byte> source, ref long cursor, int width)
    {
        if (cursor + width > (long)source.Length * 8)
            throw new InvalidDataException("Truncated animation channel delta.");
        int value = 0;
        for (int bit = 0; bit < width; bit++, cursor++)
            value = (value << 1) | ((source[(int)(cursor / 8)] >> (7 - (int)(cursor % 8))) & 1);
        return (value << (32 - width)) >> (32 - width);
    }
}
