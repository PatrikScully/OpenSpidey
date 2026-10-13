using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>SetSuspendDistance stores a zero-extended source word in the actual actor scheduler.</summary>
public sealed class ActorSuspensionDistanceCommandHandler(Action<int> setDistance) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.SetActorSuspensionDistance;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(setDistance);
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: suspension distance requires one source word.");
        setDistance(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span));
    }
}
