using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Game.Actors;

/// <summary>Direct source signals visit allocated active native lists; pulses use the actual mission factory/queue owner.</summary>
public sealed class GodotActorDirectEventCommands(GodotActorRegistry registry) : IActorDirectEventHost
{
    public static bool TryExecute(GodotActorRegistry registry, ushort opcode, ActorScriptCursor operands,
        Func<ushort, ActorScriptCursor, short> readVariable)
    {
        if (opcode is not (0x42B3 or 0x42B4)) return false;
        ArgumentNullException.ThrowIfNull(registry);
        return ActorDirectEventCommands.TryExecute(opcode, operands, readVariable, new GodotActorDirectEventCommands(registry));
    }
    public void SignalActors(ushort nodeId) => ActorDirectEventCommands.SignalActors(nodeId,
        States(ActorUpdateGroup.Control), States(ActorUpdateGroup.Enemy), States(ActorUpdateGroup.Environment));
    public void PulseNode(ushort nodeId) => registry.ReceivePulse(nodeId);
    private IEnumerable<ActorScriptState> States(ActorUpdateGroup group) => registry.SourceList(group).SourceOrder
        .OfType<GodotScriptActor>().Where(registry.Owns).Select(actor => actor.ScriptState);
}
