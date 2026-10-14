using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public interface IMissionEndLevelNodeHost
{
    void SetEndLevelNode(ushort node);
}

/// <summary>The source302 global exists before CPlayer or its scene-transition services.</summary>
public sealed class EndLevelNodeCommandHandler(IMissionEndLevelNodeHost host) : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.EndLevelNode;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: EndLevelNode requires one source word.");
        host.SetEndLevelNode(BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span));
    }
}
