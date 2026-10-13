using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>The actual player's source C60 fight-music duration field.</summary>
public interface IMissionFightMusicHost
{
    int FightMusicTime { get; set; }
}

public sealed class FightMusicTimeCommandHandler(IMissionFightMusicHost host) : IMissionCommandHandler
{
    public ushort Opcode => 213;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: SetFightMusicTime requires one source word.");
        host.FightMusicTime = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
    }
}
