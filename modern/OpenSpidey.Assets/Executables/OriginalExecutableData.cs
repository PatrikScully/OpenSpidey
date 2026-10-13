using System.Buffers.Binary;

namespace OpenSpidey.Assets.Executables;

/// <summary>Read-only stored PE data addressing for original asset tables and strings; no code is loaded or executed.</summary>
public sealed class OriginalExecutableData
{
    private readonly ReadOnlyMemory<byte> image;
    private readonly uint imageBase;
    private readonly (uint Address, uint Size, uint Offset)[] sections;

    public OriginalExecutableData(ReadOnlyMemory<byte> image)
    {
        this.image = image;
        if (image.Length < 64 || Half(0) != 0x5A4D) throw new InvalidDataException("Original executable data requires an MZ image.");
        int pe = checked((int)Word(60));
        if (Word(pe) != 0x4550 || Half(pe + 4) != 0x14C) throw new InvalidDataException("Original executable data requires an x86 PE image.");
        int count = Half(pe + 6), size = Half(pe + 20), optional = checked(pe + 24);
        if (count is <= 0 or > 96 || size < 32 || Half(optional) != 0x10B)
            throw new InvalidDataException("Original executable data has malformed PE sections.");
        imageBase = Word(optional + 28);
        int table = checked(optional + size);
        Require(table, checked(count * 40));
        sections = new (uint, uint, uint)[count];
        for (int index = 0; index < count; index++)
        {
            int entry = checked(table + index * 40);
            uint address = Word(entry + 12), rawSize = Word(entry + 16), offset = Word(entry + 20);
            Require(checked((int)offset), checked((int)rawSize));
            sections[index] = (address, rawSize, offset);
        }
    }

    public ReadOnlyMemory<byte> Slice(uint address, int count)
    {
        if (count < 0 || address < imageBase) throw new InvalidDataException("Original data address is outside its stored image.");
        uint relative = address - imageBase;
        foreach (var section in sections)
        {
            if (relative < section.Address || (ulong)relative + (uint)count > (ulong)section.Address + section.Size) continue;
            int offset = checked((int)((ulong)section.Offset + relative - section.Address));
            Require(offset, count);
            return image.Slice(offset, count);
        }
        throw new InvalidDataException("Original data address is outside stored PE section bytes.");
    }

    public uint ReadPointer(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Slice(address, 4).Span);

    public ReadOnlyMemory<byte> ReadString(uint address, int maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        for (int index = 0; index < maximumBytes; index++)
            if (Slice(checked(address + (uint)index), 1).Span[0] == 0)
                return Slice(address, index).ToArray();
        throw new InvalidDataException("Original data string has no bounded terminator.");
    }

    private ushort Half(int offset) { Require(offset, 2); return BinaryPrimitives.ReadUInt16LittleEndian(image.Span[offset..]); }
    private uint Word(int offset) { Require(offset, 4); return BinaryPrimitives.ReadUInt32LittleEndian(image.Span[offset..]); }
    private void Require(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > image.Length || count > image.Length - offset)
            throw new InvalidDataException("Original executable data is truncated or out of bounds.");
    }
}
