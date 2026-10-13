using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Missions;

/// <summary>Actual196 BuzzSpideySense: two unsigned words write shared EC8 kind and ECC duration without advancing or allocating effects.</summary>
public sealed class PlayerSenseBuzzCommandHandler(Func<PlayerAdditionalEffectState> currentPlayer) : IMissionCommandHandler
{
    private readonly Func<PlayerAdditionalEffectState> currentPlayer = currentPlayer ?? throw new ArgumentNullException(nameof(currentPlayer));
    public ushort Opcode => 196;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 4)
            throw new InvalidDataException($"Node{context.Node.Id} source196 requires its two unsigned sense words.");
        PlayerAdditionalEffectState state = currentPlayer() ?? throw new InvalidOperationException("Source196 requires the actual player sense owner.");
        state.SenseKind = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
        state.SenseTicks = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span[2..]);
    }
}
