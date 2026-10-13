using System.Buffers.Binary;

namespace OpenSpidey.Assets.Models;

/// <summary>Original eight-byte SHook: signed part-local coordinates and a source part index.</summary>
public readonly record struct ActorHook(short X, short Y, short Z, ushort PartIndex);

/// <summary>Bounded reader for the static hook packets passed to M3dUtils_ReadHooksPacket.</summary>
public sealed record ActorHookPacket(ushort HeaderMetadata, IReadOnlyList<ActorHook> Hooks)
{
    public int ByteLength => checked(4 + Hooks.Count * 8);

    public static ActorHookPacket Read(ReadOnlySpan<byte> source, int partCount)
    {
        if (partCount is <= 0 or > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(partCount));
        if (source.Length < 4) throw new InvalidDataException("Actor hook packet header is truncated.");
        // Like the adjacent links packets, the count occupies the second halfword.
        // ReadHooksPacket itself skips this header; retain the first halfword without assigning it invented flags.
        ushort metadata = BinaryPrimitives.ReadUInt16LittleEndian(source);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(source[2..]);
        if (source.Length < 4 + count * 8) throw new InvalidDataException("Actor hook packet payload is truncated.");
        var hooks = new ActorHook[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = source.Slice(4 + index * 8, 8);
            ushort part = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]);
            if (part >= partCount) throw new InvalidDataException($"Actor hook {index} references missing part {part}.");
            hooks[index] = new(BinaryPrimitives.ReadInt16LittleEndian(entry),
                BinaryPrimitives.ReadInt16LittleEndian(entry[2..]), BinaryPrimitives.ReadInt16LittleEndian(entry[4..]), part);
        }
        return new(metadata, Array.AsReadOnly(hooks));
    }
}
