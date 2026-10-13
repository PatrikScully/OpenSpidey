using System.Buffers.Binary;

namespace OpenSpidey.Assets.Audio;

public sealed record SoundDefinition(uint Flags, uint Parameters, ushort Falloff, ushort Alias)
{
    public int AssetIndex => (int)((Flags >> 16) & 127);
    public byte Pitch => (byte)Parameters;
    public ushort VolumeModifier => (ushort)(Parameters >> 16);
    public bool Looped => (Flags & 0x80000000) != 0;
}

/// <summary>Bounded decoding of the recovered four-word SFX definitions and alias table.</summary>
public sealed record SoundDefinitionBank(IReadOnlyList<SoundDefinition> Sounds)
{
    public static SoundDefinitionBank Read(ReadOnlySpan<byte> bytes, bool levelSpecific = false)
    {
        if (bytes.Length % 4 != 0) throw new InvalidDataException("Sound definition file has a partial word.");
        var definitions = new List<SoundDefinition>();
        int offset = 0;
        while (true)
        {
            if (offset > bytes.Length - 4) throw new InvalidDataException("Sound definition file has no terminator.");
            uint first = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            if (first == uint.MaxValue) return new(definitions.AsReadOnly());
            if (offset > bytes.Length - 16 || definitions.Count == 64)
                throw new InvalidDataException("Sound definition records are truncated or exceed the source bank capacity.");
            uint second = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            uint alias = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]);
            uint flags = levelSpecific ? 0x40000000u : 0;
            if ((first & 255) == 254) flags |= 0x80000000;
            flags |= ((first & 0xFF00) << 8) | (1u << ((int)(first >> 16) & 255));
            uint parameters = (first >> 24) | (second & 0xFFFF0000);
            definitions.Add(new(flags, parameters, (ushort)second, (ushort)alias));
            offset += 16;
        }
    }

    public SoundDefinition? Resolve(uint sound)
    {
        if (sound == 0) return null;
        uint index = sound & 0xFFFF3FFF;
        if ((sound & 0x8000) == 0) return index is > 0 and < 64 && index <= Sounds.Count ? Sounds[(int)index - 1] : null;
        foreach (SoundDefinition definition in Sounds)
        {
            if (definition.Alias == ushort.MaxValue) return null;
            if (definition.Alias == index) return definition;
        }
        return null;
    }
}
