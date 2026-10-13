using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>The trigger global written by147. This value is independent of gameplay difficulty.</summary>
public interface IMissionGameLevelHost
{
    int GameLevel { get; set; }
}

public sealed class GameLevelCommandHandler(IMissionGameLevelHost host) : IMissionCommandHandler
{
    public ushort Opcode => 147;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: SetGameLevel requires one source word.");
        host.GameLevel = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span);
    }
}
