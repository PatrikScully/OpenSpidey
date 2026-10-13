using System.Buffers.Binary;
using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Source SendVisible13 resolves kind2/9 links in order and changes only each matched ENV item's flag0.</summary>
public sealed class LinkedEnvironmentVisibilityCommandHandler(TriggerFile file, IEnvironmentVisibilityHost host)
    : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.SendVisible;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: SendVisible requires its original one-word visibility operand.");
        bool visible = BinaryPrimitives.ReadUInt16LittleEndian(instruction.Arguments.Span) != 0;
        foreach (ushort id in MissionRuntime.ReadLinks(context.Node))
        {
            if (id >= file.Nodes.Count) throw new InvalidDataException($"Node {context.Node.Id}: missing ENV link{id}.");
            TriggerNode node = file.Nodes[id];
            host.SetVisibility(TriggerEnvironmentReference.ReadChecksum(node), visible);
        }
    }
}
