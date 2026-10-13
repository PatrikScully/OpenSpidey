using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Canonical source body heads and actual inherited/specialized virtual death behavior for mission11/133.</summary>
public sealed class GodotMissionActorDeath : IMissionActorDeathHost
{
    private readonly GodotActorRegistry registry;
    private readonly GodotEnvironmentVisibility environment;
    public GodotMissionActorDeath(GodotActorRegistry registry, GodotEnvironmentVisibility environment)
    { this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); this.environment = environment ?? throw new ArgumentNullException(nameof(environment)); }

    private SourceActorList List(SourceMissionDeathList list) => list == SourceMissionDeathList.Suspended
        ? registry.SuspendedSourceList : registry.SourceList(list switch
        {
            SourceMissionDeathList.Baddy => ActorUpdateGroup.Enemy,
            SourceMissionDeathList.Control => ActorUpdateGroup.Control,
            SourceMissionDeathList.Environment => ActorUpdateGroup.Environment,
            SourceMissionDeathList.PowerUp => ActorUpdateGroup.PowerUp,
            _ => throw new ArgumentOutOfRangeException(nameof(list))
        });
    public object? ReadHead(SourceMissionDeathList list) => List(list).Head;
    public object? ReadNext(SourceMissionDeathList list, object body) => List(list).ReadNext(body);
    public ushort ReadNode(object body) => RequireBody(body).SourceNodeId;
    public FixedVector3 ReadPosition(object body) => RequireBody(body).SourcePosition;
    public bool IsDead(object body) => (BodyFlags(RequireBody(body)) & 0x40) != 0;
    public void Die(object body)
    {
        GodotScriptActor actor = RequireBody(body);
        switch (actor)
        {
            // These two original classes override CBody::Die. The bomb's override sends its links/stops its timer without generic zombie40.
            case GodotPowerUpActor pickup: pickup.Die(); return;
            case GodotManipulationActor manipulation: manipulation.Die(); return;
            case GodotPlatformActor platform: platform.DieSourceBody(); return;
            case GodotControlActor control: control.DieSourceBody(); return;
        }
        if ((BodyFlags(actor) & 0x40) != 0) return;
        // DieThug/DieHostage/BlackCat's gameplay helpers are separate methods; these classes inherit the actual CBody::Die slot.
        switch (actor)
        {
            case GodotThugActor thug: thug.State.BodyFlags |= 0x40; break;
            case GodotHostageActor hostage: hostage.BodyFlags |= 0x40; break;
            case GodotBlackCatActor cat: cat.State.BodyFlags |= 0x40; break;
            case GodotManipulationDebris debris: debris.State.BodyFlags |= 0x40; break;
            case GodotSwitchActor control: control.State.BodyFlags |= 0x40; break;
            case GodotEnvironmentChunkControl chunk: chunk.BodyFlags |= 0x40; break;
            default: throw new NotSupportedException($"Source CBody::Die for {actor.GetType().Name} has not been ported.");
        }
        actor.Lifecycle.Kill();
    }
    public void DeleteSuspendedBody(object body)
    {
        GodotScriptActor actor = RequireBody(body);
        if (!registry.SuspendedSourceList.SourceOrder.Any(identity => ReferenceEquals(identity, actor)))
            throw new InvalidOperationException("Source133 deleting destructor requires its actual suspended-list identity.");
        registry.DestroyImmediately(actor);
    }
    public void HideEnvironmentItem(uint checksum)
    {
        GodotEnvironmentVisibilityItem? item = environment.SourceItems.FirstOrDefault(item => item.ModelChecksum == checksum);
        if (item is null) return; // Actual Spool_FindEnviroItem permits a missing checksum.
        environment.WriteChunkFlags(item, (ushort)(item.ItemFlags | 1));
        environment.SynchronizeSourceItemFlags(item);
    }
    public void KillSpecialDisplay(ushort node) =>
        throw new NotSupportedException($"Source409 SpecialDisplay9 node{node} requires its actual CBit owner.");
    private GodotScriptActor RequireBody(object identity) => identity is GodotScriptActor actor && registry.Owns(actor)
        ? actor : throw new NotSupportedException("Source mission body death requires the actual allocated canonical actor owner.");
    private static ushort BodyFlags(GodotScriptActor actor) => actor switch
    {
        GodotThugActor thug => unchecked((ushort)thug.State.BodyFlags),
        GodotHostageActor hostage => unchecked((ushort)hostage.BodyFlags),
        GodotBlackCatActor cat => unchecked((ushort)cat.State.BodyFlags),
        GodotManipulationDebris debris => debris.BodyFlags,
        GodotManipulationActor manipulation => manipulation.State.BodyFlags,
        GodotPlatformActor platform => platform.BodyFlags,
        GodotSwitchActor control => control.State.BodyFlags,
        GodotPowerUpActor pickup => pickup.BodyFlags,
        GodotEnvironmentChunkControl chunk => chunk.BodyFlags,
        GodotControlActor control => control.BodyFlags,
        _ => throw new NotSupportedException($"Source CBody::IsDead metadata for {actor.GetType().Name} has not been ported.")
    };
}
