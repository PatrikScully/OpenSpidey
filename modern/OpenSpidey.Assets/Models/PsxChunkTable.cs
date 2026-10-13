using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

public sealed record PsxChunkRecord(uint Checksum, uint SourceByteOffset,
    IReadOnlyList<uint> StaticRuinModels, IReadOnlyList<uint> MovingRuinChunks);

/// <summary>Original Chnk packet, preserving entry order and the source offset's integer division by four.</summary>
public sealed record PsxChunkTable(IReadOnlyList<PsxChunkRecord> Records)
{
    public PsxChunkRecord? Find(uint checksum) => Records.FirstOrDefault(record => record.Checksum == checksum);

    public static PsxChunkTable? Read(ReadOnlySpan<byte> bytes)
    {
        Require(bytes, 0, 8);
        uint version = Word(bytes, 0);
        if (version is not (0x20003 or 0x20004 or 0x20006))
            throw new InvalidDataException($"Unsupported chunk source PSX version:{version:X8}.");
        uint start = Word(bytes, 4);
        if (start == 0) return null;
        int offset = checked((int)start);
        while (true)
        {
            Require(bytes, offset, 4);
            uint kind = Word(bytes, offset);
            if (kind == uint.MaxValue) return null;
            Require(bytes, offset, 8);
            int size = checked((int)Word(bytes, offset + 4));
            Require(bytes, offset + 8, size);
            if (kind == 0x6B6E6843) return ReadPacket(bytes.Slice(offset + 8, size));
            offset = checked(offset + 8 + size);
        }
    }

    public static PsxChunkTable ReadPacket(ReadOnlySpan<byte> packet)
    {
        Require(packet, 0, 4);
        int count = checked((int)Word(packet, 0));
        int dataOffset = checked(4 + count * 8);
        Require(packet, 4, checked(count * 8));
        var records = new PsxChunkRecord[count];
        for (int index = 0; index < count; index++)
        {
            uint checksum = Word(packet, 4 + index * 8);
            uint sourceOffset = Word(packet, 8 + index * 8);
            int offset = checked(dataOffset + (int)(sourceOffset >> 2) * 4);
            Require(packet, offset, 4);
            uint packedCounts = Word(packet, offset);
            int staticCount = (ushort)packedCounts, movingCount = (ushort)(packedCounts >> 16);
            Require(packet, offset + 4, checked((staticCount + movingCount) * 4));
            var stationary = new uint[staticCount]; var moving = new uint[movingCount];
            for (int piece = 0; piece < stationary.Length; piece++) stationary[piece] = Word(packet, offset + 4 + piece * 4);
            for (int piece = 0; piece < moving.Length; piece++) moving[piece] = Word(packet, offset + 4 + (staticCount + piece) * 4);
            records[index] = new(checksum, sourceOffset, Array.AsReadOnly(stationary), Array.AsReadOnly(moving));
        }
        return new(Array.AsReadOnly(records));
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > bytes.Length || count > bytes.Length - offset)
            throw new InvalidDataException("Original Chnk table or record extends beyond its source packet.");
    }
}
