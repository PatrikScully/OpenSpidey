using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Unsigned source module arguments; the palette owner performs any later byte conversion.</summary>
public readonly record struct PaletteFadeColour(uint Red, uint Green, uint Blue);

public interface IMissionPaletteFadeHost
{
    void FadeUp(PaletteFadeColour colour);
    void FadeDown();
}

/// <summary>Original303/304 dispatch to the loaded Mysterio module without taking ownership of palette animation.</summary>
public sealed class PaletteFadeCommandHandler : IMissionCommandHandler
{
    private readonly IMissionPaletteFadeHost host;
    public ushort Opcode { get; }

    private PaletteFadeCommandHandler(IMissionPaletteFadeHost host, MissionOpcode opcode)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        Opcode = (ushort)opcode;
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(IMissionPaletteFadeHost host)
    {
        yield return new PaletteFadeCommandHandler(host, MissionOpcode.FadePalettesUp);
        yield return new PaletteFadeCommandHandler(host, MissionOpcode.FadePalettesDown);
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        int length = Opcode == (ushort)MissionOpcode.FadePalettesUp ? 4 : 0;
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != length)
            throw new InvalidDataException($"Node {context.Node.Id}: palette command{Opcode} requires its original{length}-byte operands.");
        if (length == 0) { host.FadeDown(); return; }

        ReadOnlySpan<byte> words = instruction.Arguments.Span;
        uint blue = BinaryPrimitives.ReadUInt16LittleEndian(words);
        uint red = BinaryPrimitives.ReadUInt16LittleEndian(words[2..]);
        host.FadeUp(new(red, red >> 8, blue));
    }
}
