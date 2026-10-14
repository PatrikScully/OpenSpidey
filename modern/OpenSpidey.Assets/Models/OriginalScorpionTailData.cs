using System.Buffers.Binary;
using OpenSpidey.Assets.Executables;

namespace OpenSpidey.Assets.Models;

/// <summary>The ten original dynamic hooks used by Scorpion's tail renderer, in callback order.</summary>
public sealed record OriginalScorpionTailData(IReadOnlyList<ActorHook> RenderHooks)
{
    public static OriginalScorpionTailData Read(ReadOnlyMemory<byte> image)
    {
        var executable = new OriginalExecutableData(image);
        var hooks = new ActorHook[10];
        hooks[0] = ReadHook(0x55241C, 2);
        for (int index = 0; index < 4; index++) hooks[index + 1] = ReadHook(0x552404 + (uint)(index * 6), 2);
        hooks[5] = ReadHook(0x55243C, 0);
        for (int index = 0; index < 4; index++) hooks[index + 6] = ReadHook(0x552424 + (uint)(index * 6), 0);
        return new(Array.AsReadOnly(hooks));

        ActorHook ReadHook(uint address, ushort part)
        {
            ReadOnlySpan<byte> data = executable.Slice(address, 6).Span;
            return new(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]),
                BinaryPrimitives.ReadInt16LittleEndian(data[4..]), part);
        }
    }
}
