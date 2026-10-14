using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

/// <summary>Original PC300/301 are diagnostic-only console commands with no operands or game-state effects.</summary>
public sealed class SpeedupCommandHandler : IMissionCommandHandler
{
    public ushort Opcode { get; }
    private SpeedupCommandHandler(MissionOpcode opcode) => Opcode = (ushort)opcode;

    public static IEnumerable<IMissionCommandHandler> CreateHandlers()
    {
        yield return new SpeedupCommandHandler(MissionOpcode.AllowSpeedup);
        yield return new SpeedupCommandHandler(MissionOpcode.DisallowSpeedup);
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || !instruction.Arguments.IsEmpty)
            throw new InvalidDataException($"Node {context.Node.Id}: speedup command{Opcode} has no source operands.");
    }
}
