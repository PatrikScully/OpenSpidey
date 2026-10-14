using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Missions;

/// <summary>Actual182 (0x4E1254) consumes RGB words and stores only the first unsigned word in player578.</summary>
public sealed class PlayerLightBrightnessCommandHandler(PlayerBrightnessState state) : IMissionCommandHandler
{
    private readonly PlayerBrightnessState state = state ?? throw new ArgumentNullException(nameof(state));
    public ushort Opcode => (ushort)MissionOpcode.SetPlayerLightBrightness;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 6)
            throw new InvalidDataException($"Node{context.Node.Id} source182 requires all three unsigned RGB words.");
        state.SetLightSurfaceTarget(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span));
    }
}
