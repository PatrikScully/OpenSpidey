using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>The original PC151 case only advances one word. Godot owns viewport buffers independently of this obsolete console command.</summary>
public sealed class DualBufferSizeCommandHandler : IMissionCommandHandler
{
    public ushort Opcode => (ushort)MissionOpcode.SetDualBufferSize;

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 2)
            throw new InvalidDataException($"Node {context.Node.Id}: SetDualBufferSize requires its original one-word operand.");
        // Whole original4E05A2 performs no allocation, resolution change or game-state write.
    }
}
