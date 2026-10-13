using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

/// <summary>Reads M3dZone_SetZone's authentic packet10 and item indices without rewriting them into 32-bit pointers.</summary>
public static class PsxZoneGridReader
{
    public static PsxZoneGrid? Read(ReadOnlySpan<byte> bytes, int instanceCount)
    {
        Require(bytes, 0, 8);
        int offset = checked((int)Word(bytes, 4));
        if (offset == 0) return null;
        while (true)
        {
            Require(bytes, offset, 4);
            uint type = Word(bytes, offset);
            if (type == uint.MaxValue) return null;
            Require(bytes, offset, 8);
            int size = checked((int)Word(bytes, offset + 4));
            Require(bytes, offset + 8, size);
            if (type == 10) return ReadPacket(bytes.Slice(offset + 8, size), instanceCount);
            offset = checked(offset + 8 + size);
        }
    }

    private static PsxZoneGrid ReadPacket(ReadOnlySpan<byte> bytes, int instanceCount)
    {
        Require(bytes, 0, 20);
        short width = BinaryPrimitives.ReadInt16LittleEndian(bytes[16..]);
        short height = BinaryPrimitives.ReadInt16LittleEndian(bytes[18..]);
        int minimumX = unchecked((int)Word(bytes, 0)), minimumZ = unchecked((int)Word(bytes, 4));
        int maximumX = unchecked((int)Word(bytes, 8)), maximumZ = unchecked((int)Word(bytes, 12));
        if (width is <= 0 or > 20 || height is <= 0 or > 20 || maximumX <= minimumX || maximumZ <= minimumZ ||
            unchecked(maximumX - minimumX) / width <= 0)
            throw new InvalidDataException("Invalid original zone dimensions.");
        var cells = new IReadOnlyList<int>[width * height];
        int offset = 20;
        for (int z = 0; z < height; z++)
        for (int x = 0; x < width; x++)
        {
            // Each packet cell has two source metadata words, count, ordered indices, then a zero terminator.
            Require(bytes, offset, 12);
            int count = checked((int)Word(bytes, offset + 8));
            offset = checked(offset + 12);
            Require(bytes, offset, checked(count * 4 + 4));
            var items = new int[count];
            for (int index = 0; index < count; index++)
            {
                int item = checked((int)Word(bytes, offset + index * 4));
                if ((uint)item >= (uint)instanceCount) throw new InvalidDataException("Zone cell references a missing original instance.");
                items[index] = item;
            }
            if (Word(bytes, offset + count * 4) != 0) throw new InvalidDataException("Original zone cell has no null terminator.");
            offset = checked(offset + count * 4 + 4);
            cells[z * width + x] = Array.AsReadOnly(items);
        }
        return new(minimumX, minimumZ, maximumX, maximumZ, width, height, Array.AsReadOnly(cells));
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length - count) throw new InvalidDataException("Truncated original zone packet.");
    }
}
