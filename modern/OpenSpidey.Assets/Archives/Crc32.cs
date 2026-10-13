namespace OpenSpidey.Assets.Archives;

internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint checksum = uint.MaxValue;
        foreach (byte value in bytes)
            checksum = Table[(checksum ^ value) & 255] ^ (checksum >> 8);
        return ~checksum;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
                value = (value >> 1) ^ ((value & 1) != 0 ? 0xedb88320u : 0);
            table[index] = value;
        }
        return table;
    }
}
