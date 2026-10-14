using System.Buffers.Binary;
using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Core.Actors.Rhino;

/// <summary>Loads the PC rhino XA dispatch table embedded at 0x552208.</summary>
public static class RhinoSpeechTable
{
    public static RhinoSpeechEntry[] Read(ReadOnlyMemory<byte> originalImage)
    {
        var data = new OriginalExecutableData(originalImage);
        var entries = new RhinoSpeechEntry[RhinoState.DataCount];
        for (int index = 0; index < entries.Length; index++)
        {
            ReadOnlySpan<byte> row = data.Slice(RhinoState.DataAddress + (uint)(index * 0xC), 0xC).Span;
            int field8 = BinaryPrimitives.ReadInt32LittleEndian(row[8..]);
            if (field8 != index)
                throw new InvalidDataException($"gRhinoData[{index}].field_8 must equal its index (Matt's checksum).");
            entries[index] = new(
                BinaryPrimitives.ReadInt16LittleEndian(row),
                BinaryPrimitives.ReadInt16LittleEndian(row[2..]),
                BinaryPrimitives.ReadUInt16LittleEndian(row[4..]),
                BinaryPrimitives.ReadInt16LittleEndian(row[6..]),
                field8);
        }
        return entries;
    }
}
