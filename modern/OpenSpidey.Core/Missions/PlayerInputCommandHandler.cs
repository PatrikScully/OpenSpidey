using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public sealed class PlayerInputCommandHandler(Action<ushort> suppressPhysicalInput) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.IgnorePlayerInput;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction) =>
        suppressPhysicalInput(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span));
}
