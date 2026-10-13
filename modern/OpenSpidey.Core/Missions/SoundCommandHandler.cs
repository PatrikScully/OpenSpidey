using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Audio;

namespace OpenSpidey.Core.Missions;

/// <summary>Original trigger PlaySound volume0x2000 and ushort StopSound handle through the actual shared sound controller.</summary>
public sealed class SoundCommandHandler : IMissionCommandHandler
{
    private readonly SoundChannelController sounds;
    private readonly MissionOpcode operation;
    public ushort Opcode => (ushort)operation;
    public SoundCommandHandler(MissionOpcode operation, SoundChannelController sounds)
    {
        if (operation is not (MissionOpcode.PlaySound or MissionOpcode.StopSound)) throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
    }
    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: sound command requires its exact opcode and one source word.");
        ushort operand = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
        if (operation == MissionOpcode.PlaySound) sounds.Play(operand, 0x2000);
        else sounds.Stop(operand);
    }
    public static IEnumerable<IMissionCommandHandler> CreateHandlers(SoundChannelController sounds) =>
        new MissionOpcode[] { MissionOpcode.PlaySound, MissionOpcode.StopSound }.Select(operation => new SoundCommandHandler(operation, sounds));
}
