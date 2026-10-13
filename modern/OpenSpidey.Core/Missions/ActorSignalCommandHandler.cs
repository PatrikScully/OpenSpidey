using OpenSpidey.Assets.Scripting;

namespace OpenSpidey.Core.Missions;

public interface IMissionActorSignals
{
    void SignalActiveActors(ushort nodeId);
    void SuspendActiveActors(ushort nodeId);
    void ActivateActiveActors(ushort nodeId);
}

/// <summary>Routes actor signals through source links, preserving link order and actor-node gates.</summary>
public sealed class ActorSignalCommandHandler : IMissionCommandHandler
{
    private readonly TriggerFile file;
    private readonly IMissionActorSignals actors;
    private readonly MissionOpcode operation;
    public ushort Opcode => (ushort)operation;

    public ActorSignalCommandHandler(MissionOpcode operation, TriggerFile file, IMissionActorSignals actors)
    {
        if (operation is not (MissionOpcode.SendSignal or MissionOpcode.SendActivate or MissionOpcode.SendSuspend))
            throw new ArgumentOutOfRangeException(nameof(operation));
        this.operation = operation;
        this.file = file;
        this.actors = actors;
    }

    public void Execute(MissionCommandContext context, TriggerInstruction instruction)
    {
        foreach (ushort id in MissionRuntime.ReadLinks(context.Node))
        {
            if (id >= file.Nodes.Count) throw new InvalidDataException($"Node {context.Node.Id}: missing linked actor {id}.");
            if (file.Nodes[id].TypeCode is not (1 or 7)) continue;
            switch (operation)
            {
                case MissionOpcode.SendSignal: actors.SignalActiveActors(id); break;
                case MissionOpcode.SendSuspend: actors.SuspendActiveActors(id); break;
                case MissionOpcode.SendActivate: actors.ActivateActiveActors(id); break;
            }
        }
    }
}
