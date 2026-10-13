using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Actors;

/// <summary>Source link events use actual active actor lists and the real mission pulse owner.</summary>
public sealed class GodotActorLinkEventCommands(GodotActorRegistry registry) : IActorLinkEventHost
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable)
    {
        if (opcode is not (0x42B1 or 0x42B2)) return false;
        ArgumentNullException.ThrowIfNull(registry);
        return ActorLinkEventCommands.TryExecute(opcode, operands, readVariable, new GodotActorLinkEventCommands(registry));
    }
    public IReadOnlyList<ushort> Links(ushort nodeId) => MissionRuntime.ReadLinks(Node(nodeId));
    public void SendSignal(ushort nodeId) => ActorLinkEventCommands.SendSignal(nodeId, Node(nodeId).TypeCode,
        States(ActorUpdateGroup.Enemy), States(ActorUpdateGroup.Control), States(ActorUpdateGroup.Environment));
    public void SendPulse(ushort nodeId) => registry.ReceivePulse(nodeId);
    private IEnumerable<ActorScriptState> States(ActorUpdateGroup group) => registry.SourceList(group).SourceOrder
        .OfType<GodotScriptActor>().Where(registry.Owns).Select(actor => actor.ScriptState);
    private OpenSpidey.Assets.Scripting.TriggerNode Node(ushort nodeId) => nodeId < registry.Triggers.Nodes.Count
        ? registry.Triggers.Nodes[nodeId] : throw new InvalidDataException($"Source link event references missing node{nodeId}.");
}
