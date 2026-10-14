using System.Buffers.Binary;
using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>gRhinoWallChunk0..3 allow-lists at 0x55AD18..0x55AD60 (rhino.cpp GonnaHitWall).</summary>
public sealed class RhinoWallChunkLists
{
    public uint[] Chunk0 { get; init; } = [];
    public uint[] Chunk1 { get; init; } = [];
    public uint[] Chunk2 { get; init; } = [];
    public uint[] Chunk3 { get; init; } = [];

    public static RhinoWallChunkLists Read(ReadOnlyMemory<byte> originalImage)
    {
        var data = new OriginalExecutableData(originalImage);
        return new()
        {
            Chunk0 = ReadList(data, 0x55AD18, data.ReadPointer(0x55AD5C)),
            Chunk1 = ReadList(data, 0x55ACF8, data.ReadPointer(0x55AD58)),
            Chunk2 = ReadList(data, 0x55AD24, data.ReadPointer(0x55AD54)),
            Chunk3 = ReadList(data, 0x55AD44, data.ReadPointer(0x55AD60)),
        };
    }

    private static uint[] ReadList(OriginalExecutableData data, uint address, uint count)
    {
        if (count == 0) return [];
        int bytes = checked((int)count * 4);
        ReadOnlySpan<byte> span = data.Slice(address, bytes).Span;
        var values = new uint[count];
        for (int index = 0; index < values.Length; index++)
            values[index] = BinaryPrimitives.ReadUInt32LittleEndian(span[(index * 4)..]);
        return values;
    }
}
