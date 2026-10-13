using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public readonly record struct MissionCommandContext(TriggerNode Node, byte PulsesReceived);

/// <summary>Implementations own a concrete gameplay command; the interpreter owns event order.</summary>
public interface IMissionCommandHandler
{
    ushort Opcode { get; }
    void Execute(MissionCommandContext context, TriggerInstruction instruction);
}

/// <summary>A command can hold the interpreter at its next instruction while an asynchronous scene action runs.</summary>
public interface IMissionBlockingCommandHandler : IMissionCommandHandler
{
    bool IsBlocking { get; }
}

/// <summary>Pulses targeting non-command nodes request spawns from the scene's actor factory.</summary>
public interface IMissionActorFactory
{
    void Spawn(TriggerNode node);
}

public enum MissionTraceKind { Queued, CommandStarted, CommandCompleted, BudgetReached, Faulted }

public readonly record struct MissionTrace(MissionTraceKind Kind, ushort NodeId, ushort? Opcode, int SourceOffset)
{
    public TriggerInstructionSourceKind SourceKind { get; init; }
}
