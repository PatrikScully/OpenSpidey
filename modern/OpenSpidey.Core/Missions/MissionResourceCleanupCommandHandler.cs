using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public interface IMissionResourceCleanupHost
{
    void ClearSceneLibraries();
    void ClearCodeModules();
}

/// <summary>Original resource cleanup remains separate from Init_KillAll and runs at its authored command boundary.</summary>
public sealed class MissionResourceCleanupCommandHandler : IMissionCommandHandler
{
    private readonly MissionOpcode operation;
    private readonly IMissionResourceCleanupHost host;
    public ushort Opcode => (ushort)operation;

    public MissionResourceCleanupCommandHandler(MissionOpcode operation, IMissionResourceCleanupHost host)
    {
        if (operation is not (MissionOpcode.ClearSceneLibraries or MissionOpcode.ClearCodeModules))
            throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        if (instruction.Opcode != Opcode || instruction.Arguments.Length != 0)
            throw new InvalidDataException($"Node {context.Node.Id}: resource cleanup {Opcode} requires no operands.");
        if (operation == MissionOpcode.ClearSceneLibraries) host.ClearSceneLibraries();
        else host.ClearCodeModules();
    }

    public static IEnumerable<IMissionCommandHandler> CreateHandlers(IMissionResourceCleanupHost host) =>
        new MissionOpcode[] { MissionOpcode.ClearSceneLibraries, MissionOpcode.ClearCodeModules }
            .Select(operation => new MissionResourceCleanupCommandHandler(operation, host));
}
