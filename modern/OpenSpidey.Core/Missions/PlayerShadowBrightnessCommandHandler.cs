using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Missions;

/// <summary>Actual184 consumes RGB words, stores only the first unsigned word in player574, and leaves current lighting to ground/crawl physics.</summary>
public sealed class PlayerShadowBrightnessCommandHandler(PlayerBrightnessState state) : IMissionCommandHandler
{
    private readonly PlayerBrightnessState state = state ?? throw new ArgumentNullException(nameof(state));
    public ushort Opcode => 184;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 6)
            throw new InvalidDataException($"Node{context.Node.Id} source184 requires all three unsigned RGB words.");
        state.SetDarkSurfaceTarget(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span));
    }
}
