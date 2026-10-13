using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Assets.Animation;

public readonly record struct SpriteAnimationFrame(sbyte OffsetX, sbyte OffsetY, byte Width, byte Height, uint TextureChecksum);
public sealed record SpriteAnimation(string Name, IReadOnlyList<SpriteAnimationFrame> Frames);

/// <summary>Immutable PSX sequencer packet import; file texture indices become checksums instead of runtime pointers.</summary>
public static class SpriteAnimationReader
{
    public static IReadOnlyList<SpriteAnimation> Read(ReadOnlySpan<byte> bytes)
    {
        Require(bytes, 0, 16);
        int instances = Count(bytes, 8);
        int modelOffset = checked(12 + instances * 36);
        int models = Count(bytes, modelOffset);
        int offset = Offset(bytes, 4);
        if (offset == 0) return [];
        var packets = new List<(int Offset, int Length)>();
        while (true)
        {
            uint kind = Word(bytes, offset);
            if (kind == uint.MaxValue) break;
            int length = Count(bytes, offset + 4);
            int data = checked(offset + 8);
            Require(bytes, data, length);
            if (kind == 69) packets.Add((data, length));
            offset = checked(data + length);
        }
        if (packets.Count == 0) return [];
        if (instances != 0 || models != 0) throw new InvalidDataException("Sprite sequencers must not contain model instances.");
        int table = checked(offset + 4 + models * 4);
        int textures = Count(bytes, table);
        table += 4;
        Require(bytes, table, checked(textures * 4));
        var animations = new List<SpriteAnimation>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((int start, int length) in packets)
        {
            ReadOnlySpan<byte> packet = bytes.Slice(start, length);
            int count = Count(packet, 0);
            if (count >= 200) throw new InvalidDataException("Sprite sequencer has too many animations.");
            int cursor = 4;
            for (int index = 0; index < count; index++)
            {
                Require(packet, cursor, 12);
                string name = Encoding.ASCII.GetString(packet.Slice(cursor, 8)).TrimEnd('\0', ' ');
                if (name.Length == 0 || !names.Add(name)) throw new InvalidDataException("Sprite sequencer has an empty or duplicate animation name.");
                int frames = Count(packet, cursor + 8);
                if (frames is <= 0 or >= 1000) throw new InvalidDataException($"Sprite animation {name} has invalid frame count.");
                cursor += 12;
                Require(packet, cursor, checked(frames * 8));
                var decoded = new SpriteAnimationFrame[frames];
                for (int frame = 0; frame < frames; frame++, cursor += 8)
                {
                    int texture = Offset(packet, cursor + 4);
                    if (texture >= textures) throw new InvalidDataException($"Sprite animation {name} references an invalid texture index.");
                    // Menu packets also use zero rectangle bytes; consumers that draw their
                    // texture quads use texture dimensions instead of the flat-sprite rectangle.
                    decoded[frame] = new(unchecked((sbyte)packet[cursor]), unchecked((sbyte)packet[cursor + 1]),
                        packet[cursor + 2], packet[cursor + 3], Word(bytes, table + texture * 4));
                }
                animations.Add(new(name, Array.AsReadOnly(decoded)));
            }
            if (cursor != packet.Length) throw new InvalidDataException("Sprite sequencer has unexpected trailing data.");
        }
        return animations.AsReadOnly();
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset)
    {
        Require(bytes, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }
    private static int Count(ReadOnlySpan<byte> bytes, int offset) => checked((int)Word(bytes, offset));
    private static int Offset(ReadOnlySpan<byte> bytes, int offset) => checked((int)Word(bytes, offset));
    private static void Require(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length - length)
            throw new InvalidDataException("Sprite sequencer extends beyond its asset buffer.");
    }
}
