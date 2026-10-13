using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Inherited linked commands mutate canonical active BaddyList identities, never ENV/control/suspended lists.</summary>
public sealed class GodotLinkedBaddyCommands(GodotActorRegistry registry) : IActorLinkedBodyCommandHost
{
    public static bool TryExecute(GodotActorRegistry registry, ushort nodeId, ushort opcode, ActorScriptCursor operands)
    {
        if (opcode is not (0x450F or 0x4511 or 0x4512)) return false;
        ArgumentNullException.ThrowIfNull(registry);
        return ActorLinkedBodyCommands.TryExecute(opcode, operands, nodeId, new GodotLinkedBaddyCommands(registry));
    }

    public IReadOnlyList<ushort> Links(ushort nodeId)
    {
        if (nodeId >= registry.Triggers.Nodes.Count) throw new InvalidDataException($"Source linked-body command references missing node{nodeId}.");
        return MissionRuntime.ReadLinks(registry.Triggers.Nodes[nodeId]);
    }

    public IEnumerable<IActorLinkedBodyTarget> ActiveBaddies() => registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder
        .OfType<GodotScriptActor>().Where(registry.Owns).Select(actor => new Body(actor));

    private sealed class Body(GodotScriptActor actor) : IActorLinkedBodyTarget
    {
        public ushort SourceNodeId => actor.SourceNodeId;
        public SourceSuperItemDepth SourceDepth => actor is GodotThugActor or GodotHostageActor or GodotBlackCatActor
            ? actor.SourceDepth
            : throw new NotSupportedException($"Source active BaddyList type{actor.SourceActorType} is not a CSuper; writing100/102 would exceed its original body layout.");
        public ushort ItemFlags
        {
            get => actor switch
            {
                GodotThugActor thug => thug.ItemFlags,
                GodotHostageActor hostage => hostage.ItemFlags,
                GodotBlackCatActor cat => cat.ItemFlags,
                IGodotSourceActorVisibility source => source.SourceVisibilityFlags,
                _ => throw Unported()
            };
            set
            {
                switch (actor)
                {
                    case GodotThugActor thug: thug.ItemFlags = value; break;
                    case GodotHostageActor hostage: hostage.ItemFlags = value; hostage.Variables.ItemFlags = value; break;
                    case GodotBlackCatActor cat: cat.ItemFlags = value; break;
                    case IGodotSourceActorVisibility source: source.SetSourceVisibilityFlags(value); break;
                    default: throw Unported();
                }
            }
        }
        private NotSupportedException Unported() => new($"Source active BaddyList type{actor.SourceActorType} has no canonical linked visibility flag adapter.");
    }
}
