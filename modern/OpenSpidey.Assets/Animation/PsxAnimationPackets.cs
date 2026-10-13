using System.Buffers.Binary;

namespace OpenSpidey.Assets.Animation;

internal static class PsxAnimationPackets
{
    public static int ReadPartCount(ReadOnlySpan<byte> source)
    {
        Require(source, 0, 12);
        uint version = Word(source, 0);
        if (version is not (0x20003 or 0x20004 or 0x20006))
            throw new InvalidDataException("Unsupported animation PSX version.");
        return checked((int)Word(source, 8));
    }

    public static ReadOnlySpan<byte> Find(ReadOnlySpan<byte> source, uint requestedKind)
    {
        _ = ReadPartCount(source);
        int cursor = checked((int)Word(source, 4));
        if (cursor == 0) return default;
        ReadOnlySpan<byte> result = default;
        while (true)
        {
            uint kind = Word(source, cursor);
            if (kind == uint.MaxValue) return result;
            int size = checked((int)Word(source, checked(cursor + 4)));
            Require(source, checked(cursor + 8), size);
            if (kind == requestedKind)
            {
                if (size == 0) throw new InvalidDataException($"Empty animation packet {kind:X8}.");
                if (!result.IsEmpty) throw new InvalidDataException($"Duplicate animation packet {kind:X8}.");
                result = source.Slice(cursor + 8, size);
            }
            cursor = checked(cursor + 8 + size);
        }
    }

    private static uint Word(ReadOnlySpan<byte> source, int offset)
    {
        Require(source, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(source[offset..]);
    }

    private static void Require(ReadOnlySpan<byte> source, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > source.Length - count)
            throw new InvalidDataException("Animation packet range exceeds source data.");
    }
}
