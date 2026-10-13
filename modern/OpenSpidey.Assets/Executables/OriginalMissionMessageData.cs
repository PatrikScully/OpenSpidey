using System.Buffers.Binary;
using System.Text;

namespace OpenSpidey.Assets.Executables;

public sealed record OriginalMessageSubstitution(string Pattern, string Format, ushort FirstAction, ushort SecondAction);

/// <summary>Original source checkpoint text imported through its data-table pointer, retaining the executable's language bytes.</summary>
public sealed record OriginalMissionMessageData(ReadOnlyMemory<byte> CheckpointText)
{
    public IReadOnlyList<OriginalMessageSubstitution> Substitutions { get; init; } = [];
    public static OriginalMissionMessageData Read(ReadOnlyMemory<byte> image)
    {
        var data = new OriginalExecutableData(image);
        var substitutions = new OriginalMessageSubstitution[9];
        for (int index = 0; index < substitutions.Length; index++)
        {
            ReadOnlySpan<byte> actions = data.Slice(0x55809C + (uint)index * 4, 4).Span;
            substitutions[index] = new(Encoding.Latin1.GetString(data.ReadString(data.ReadPointer(0x558054 + (uint)index * 8), 256).Span),
                Encoding.Latin1.GetString(data.ReadString(data.ReadPointer(0x558058 + (uint)index * 8), 256).Span),
                BinaryPrimitives.ReadUInt16LittleEndian(actions), BinaryPrimitives.ReadUInt16LittleEndian(actions[2..]));
        }
        return new(data.ReadString(data.ReadPointer(0x54B88C), 256)) { Substitutions = Array.AsReadOnly(substitutions) };
    }
}
