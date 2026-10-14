using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Original197 (0x4E04F1) stores its signed word in player0xE34, the ReadAnalogueInput reference heading.</summary>
public sealed class PlayerMotionAngleOffsetCommandHandler(Action<short> setReferenceHeading) : IMissionCommandHandler
{
    private readonly Action<short> setReferenceHeading = setReferenceHeading ?? throw new ArgumentNullException(nameof(setReferenceHeading));
    public ushort Opcode => (ushort)MissionOpcode.SetMotionAngleOffset;
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node{context.Node.Id} source197 requires its original signed word.");
        setReferenceHeading(BinaryPrimitives.ReadInt16LittleEndian(instruction.Arguments.Span));
    }
}
