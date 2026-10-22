using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;

namespace OpenSpidey.Game.Actors;

/// <summary>Original type411 CSimbyDroplet factory slot1 (0x4A25E0): flat-bit motion and owner-node pulse on death.</summary>
public partial class GodotSimbyDropletActor : GodotScriptActor
{
    private GodotActorRegistry registry = null!;
    private SimbyDropletState state = null!;
    private bool retired;

    public SimbyDropletState State => state;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Background;
    public override FixedVector3 SourcePosition => state.Position;

    public static GodotSimbyDropletActor Create(TriggerNode node, GodotActorRegistry registry, GodotSimbyConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != SimbyState.DropletTriggerType)
            throw new NotSupportedException("Simby module slot1 requires trigger type411 CSimbyDroplet.");
        var actor = new GodotSimbyDropletActor
        {
            Name = $"SimbyDroplet{node.Id}", registry = registry, Definition = definition
        };
        try
        {
            actor.ScriptState = new() { ActorType = SimbyState.DropletTriggerType, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry) { Position = default, ItemFlags = 0 };
            actor.Lifecycle = new(false, false);
            actor.state = configuration.Droplets.Emit(new SimbyDropletState(node, configuration.GroundY));
            actor.Variables.Position = actor.state.Position;
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin() { }

    internal override void Advance()
    {
        SourceUpdates++;
        Variables.Position = state.Position;
        if (state.Alive || retired) return;
        retired = true;
        TriggerNode owner = registry.Triggers.Nodes[state.OwnerNode];
        foreach (ushort link in MissionRuntime.ReadLinks(owner)) registry.ReceivePulse(link);
        registry.DestroyImmediately(this);
    }
}
