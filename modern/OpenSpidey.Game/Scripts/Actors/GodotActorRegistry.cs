using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Owns source actors in creation order and schedules them after the player.</summary>
public partial class GodotActorRegistry : Node3D, IMissionActorFactory, IMissionActorSignals, IActorScriptVariableWorld, IActorSpeechCommandHost
{
    private readonly List<GodotScriptActor> active = [];
    private readonly List<GodotScriptActor> suspended = [];
    private readonly Dictionary<short, Func<TriggerNode, GodotActorRegistry, GodotScriptActor>> factories = new()
    {
        [203] = GodotControlActor.Create,
        [402] = GodotPlatformActor.Create
    };
    private static readonly ActorUpdateGroup[] updateGroups = Enum.GetValues<ActorUpdateGroup>();
    private int physicsTicks;
    private bool destroyed;
    private GodotBaddyConstructionOwner? baddyConstruction;
    public BaddyConstructionCount BaddyConstructionState { get; init; } = new();
    public GodotBaddyConstructionOwner BaddyConstruction =>
        baddyConstruction ??= new(this, BaddyConstructionState);
    public TriggerFile Triggers { get; init; } = null!;
    public GodotPlayer? Player { get; set; }
    public IGodotPowerUpFactory? PowerUps { get; set; }
    public Func<int, int> RandomSource { get; init; } = new GameRandom(0x12A6CC58).Next;
    public FixedVector3 Gravity { get; set; } = new(0, 0x8000, 0);
    public SoundChannelController? Sounds { get; init; }
    public GodotControlSceneServices? ControlServices { get; set; }
    public IActorEnvironmentChunkHost? EnvironmentChunks { get; set; }
    public IActorScreenFlashHost? ScreenFlash { get; set; }
    public IActorSparkEmitter? Sparks { get; set; }
    public IActorCameraShakeHost? CameraShake { get; set; }
    public IActorVisualEffectHost? ActorVisualEffects { get; set; }
    public IActorPlayerScriptHost? PlayerScriptHost { get; set; }
    public IActorCinematicScreenHost? CinematicScreenHost { get; set; }
    public GodotControlSceneServices RequireControlServices() => ControlServices ??
        throw new InvalidOperationException("Source control effects require the actual scene compass, sound channels, listener and clock.");
    public Func<SoundListener> SoundListener { get; init; } =
        () => throw new InvalidOperationException("The sound listener has not been connected.");
    public Action<int, int, int> VoicePlayback { get; init; } =
        (_, _, _) => throw new InvalidOperationException("Actor speech has not been connected.");
    public Action<int, int, FixedVector3, int> PositionVoicePlayback { get; init; } =
        (_, _, _, _) => throw new InvalidOperationException("Actor positional XA speech has not been connected.");
    public Func<PsxAssetLibrary> ObjectLibrary { get; init; } =
        () => throw new InvalidOperationException("The actor object library has not been selected.");
    public Action<string, int> ClaimObjectModel { get; init; } =
        (_, _) => throw new InvalidOperationException("Object-model ownership has not been connected.");
    public Func<IReadOnlyList<DecodedTexture>> ObjectTextures { get; init; } =
        () => throw new InvalidOperationException("Actor textures have not been connected.");
    public Action<GodotPlatformActor, bool, bool> ShatterPlatform { get; init; } =
        (_, _, _) => throw new InvalidOperationException("Original platform shatter effects are not connected.");
    public ActorScriptEnvironment Environment { get; set; }
    public Action<ushort> ReceivePulse { get; set; } = _ => throw new InvalidOperationException("Mission pulses are not connected.");
    public event Action<Exception>? Faulted;
    public event Action<GodotScriptActor>? Spawned;
    public event Action<GodotScriptActor>? ActorSuspended;
    public event Action<GodotScriptActor>? ActorResumed;
    public event Action<GodotScriptActor>? ActorRemoved;
    public event Action<GodotScriptActor, uint>? ActorDistanceMeasured;
    public int LiveCount => active.Count + suspended.Count;
    public int DeletedCount { get; private set; }
    public int SourceUpdates { get; private set; }
    private readonly Dictionary<ActorUpdateGroup, SourceActorList> actorLists =
        Enum.GetValues<ActorUpdateGroup>().ToDictionary(group => group, _ => new SourceActorList());
    public SourceActorList SourceList(ActorUpdateGroup group) => actorLists[group];
    public SourceActorList SuspendedSourceList { get; } = new();
    public uint SuspensionDistance { get; set; } = 13000;
    public int Difficulty { get; set; } = 1;
    public int MovieInputPolls { get; set; }
    public FixedVector3? PlayerPosition => Player?.Controller.Position;

    public IReadOnlyList<GodotScriptActor> SourceActors(ActorUpdateGroup group) =>
        active.Where(actor => actor.UpdateGroup == group).ToArray();

    public bool Owns(GodotScriptActor actor) => GodotObject.IsInstanceValid(actor) &&
        !actor.IsQueuedForDeletion() && (active.Contains(actor) || suspended.Contains(actor));

    public void RegisterFactory(short actorType, Func<TriggerNode, GodotActorRegistry, GodotScriptActor> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!factories.TryAdd(actorType, factory))
            throw new InvalidOperationException($"Source actor type {actorType} already has a session factory.");
    }

    public override void _Ready() => ProcessPhysicsPriority = 10;

    public void Spawn(TriggerNode node)
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        if (node.TypeCode is 5 or 20)
        {
            (PowerUps ?? throw new NotSupportedException("The authored power-up constructor is not connected.")).Create(node, this);
            return;
        }
        short type = TriggerActorDefinition.Read(node).ActorType;
        if (!factories.TryGetValue(type, out var factory))
            throw new NotSupportedException($"Actor type {type}, node {node.Id} has not been ported.");
        GodotScriptActor actor = GodotBaddyConstructionOwner.IsSupportedBaddyType(type)
            ? BaddyConstruction.Create(type, () => factory(node, this))
            : factory(node, this);
        Adopt(actor, actor.Definition.StartsSuspended);
    }

    /// <summary>Own an actual source body created by an effect constructor, without inventing a trigger record.</summary>
    public void Adopt(GodotScriptActor actor, bool startsSuspended = false)
    {
        ObjectDisposedException.ThrowIf(destroyed, this);
        ArgumentNullException.ThrowIfNull(actor);
        if (actor.GetParent() is not null || active.Contains(actor) || suspended.Contains(actor))
            throw new InvalidOperationException("Source actor adoption requires an unattached native body.");
        active.Insert(0, actor);
        SourceList(actor.UpdateGroup).Attach(actor, () => AdvanceActor(actor));
        try
        {
            actor.AddChild(new GodotNativeDestructionObserver
            {
                Name = "SourceRegistryDestructor",
                Destroyed = () => { if (Retire(actor)) DeletedCount++; }
            });
            AddChild(actor);
            actor.Begin();
            if (startsSuspended) Suspend(actor);
            Spawned?.Invoke(actor);
        }
        catch
        {
            Retire(actor);
            if (ReferenceEquals(actor.GetParent(), this)) RemoveChild(actor);
            if (GodotObject.IsInstanceValid(actor)) actor.Free();
            throw;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (++physicsTicks % 2 != 0) return;
        try
        {
            SourceUpdates++;
            foreach (ActorUpdateGroup group in updateGroups)
            {
                SourceList(group).Update();
            }
            // Resume nearby suspended actors after active actor updates, as in Ob_MaybeUnSuspendOrCull.
            SuspendedSourceList.Update();
        }
        catch (Exception exception)
        {
            SetPhysicsProcess(false);
            GD.PushError(exception.ToString());
            Faulted?.Invoke(exception);
        }
    }

    public void SignalActiveActors(ushort nodeId)
    {
        foreach (GodotScriptActor actor in active.Where(actor => actor.SourceNodeId == nodeId))
            actor.ScriptState.PendingSignals |= 1;
    }

    private void AdvanceActor(GodotScriptActor actor)
    {
        if (!Owns(actor)) return;
        uint distance = PlayerPosition is { } player ? ActorLifecycle.Distance(player, actor.SourcePosition) : 0;
        if (actor.Lifecycle.Phase == ActorLifePhase.Active) ActorDistanceMeasured?.Invoke(actor, distance);
        switch (actor.Lifecycle.PrepareUpdate(distance, SuspensionDistance))
        {
            case ActorUpdateAction.RunBehavior: actor.Advance(); break;
            case ActorUpdateAction.Skip:
                if (actor.Lifecycle.Phase == ActorLifePhase.Suspended) MoveToSuspended(actor);
                break;
            case ActorUpdateAction.Delete:
                Retire(actor);
                actor.QueueFree();
                DeletedCount++;
                break;
        }
    }
    /// <summary>Immediate source module cleanup; this executes the destructor instead of the ordinary two-update death path.</summary>
    public void DestroyImmediately(GodotScriptActor actor)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!Owns(actor)) throw new ArgumentException("Immediate destruction requires an allocated actor owned by this scene.", nameof(actor));
        Retire(actor);
        actor.Free();
        DeletedCount++;
    }

    private bool Retire(GodotScriptActor actor)
    {
        bool removed = active.Remove(actor) | suspended.Remove(actor);
        if (!removed) return false;
        SourceList(actor.UpdateGroup).Detach(actor);
        SuspendedSourceList.Detach(actor);
        ActorRemoved?.Invoke(actor);
        return true;
    }
    public void SuspendActiveActors(ushort nodeId)
    {
        foreach (GodotScriptActor actor in active.Where(actor => actor.SourceNodeId == nodeId).ToArray()) Suspend(actor);
    }
    public void ActivateActiveActors(ushort nodeId)
    {
        // Recovered SendUnSuspend searches active lists; initial suspended actors resume through the distance pass.
        foreach (GodotScriptActor actor in active.Where(actor => actor.SourceNodeId == nodeId && actor.Lifecycle.Phase == ActorLifePhase.Active))
            actor.Lifecycle.ResumeNearby(0, SuspensionDistance);
    }

    private void Suspend(GodotScriptActor actor)
    {
        actor.Lifecycle.Suspend();
        MoveToSuspended(actor);
    }
    private void MoveToSuspended(GodotScriptActor actor)
    {
        if (!active.Remove(actor)) return;
        SourceList(actor.UpdateGroup).Detach(actor);
        suspended.Insert(0, actor);
        SuspendedSourceList.Attach(actor, () => ResumeActor(actor));
        ActorSuspended?.Invoke(actor);
    }

    private void ResumeActor(GodotScriptActor actor)
    {
        if (PlayerPosition is not { } player) return;
        if (!actor.Lifecycle.ResumeNearby(ActorLifecycle.Distance(player, actor.SourcePosition), SuspensionDistance)) return;
        suspended.Remove(actor);
        SuspendedSourceList.Detach(actor);
        active.Insert(0, actor);
        SourceList(actor.UpdateGroup).Attach(actor, () => AdvanceActor(actor));
        ActorResumed?.Invoke(actor);
    }

    public ActorScriptCursor ScriptAtNode(ushort nodeId)
    {
        TriggerNode node = GetNode(nodeId);
        TriggerScriptFragment fragment = TriggerPlacement.Read(node).ReadScriptAfterAngles(node);
        return new(fragment.Data, fragment.SourceOffset);
    }
    public ushort FirstLink(ushort nodeId) => MissionRuntime.ReadLinks(GetNode(nodeId)).FirstOrDefault();
    public void PlayGlobalSpeech(SpeechRequest request) => VoicePlayback(request.Bank, request.Slot, request.Priority);
    public void PlayPositionSpeech(SpeechRequest request, FixedVector3 position) =>
        PositionVoicePlayback(request.Bank, request.Slot, position, request.Priority);
    private TriggerNode GetNode(ushort id) => id < Triggers.Nodes.Count ? Triggers.Nodes[id]
        : throw new InvalidDataException($"Actor references missing trigger node {id}.");
    public int Random(int range) => RandomSource(range);
    public void SelectModel(uint checksum) => throw new NotSupportedException($"Control actor model {checksum:X8} needs its rendering adapter.");
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || destroyed) return;
        destroyed = true;
        // Tree exit precedes a scene's Predelete callback. Its captured source heads must
        // remain intact for Init_KillAll, and a live reparent retains the same identities.
        // If startup failed before that scene owner was available, destroy actual adopted
        // native bodies here rather than erasing their list entries or constructor leases.
        foreach (ActorUpdateGroup group in new[] { ActorUpdateGroup.Enemy, ActorUpdateGroup.Control,
            ActorUpdateGroup.Environment, ActorUpdateGroup.Background, ActorUpdateGroup.PowerUp, ActorUpdateGroup.Bullet })
            DestroyOwnedBodies(SourceList(group));
        DestroyOwnedBodies(SuspendedSourceList);
        DestroyOwnedBodies(SourceList(ActorUpdateGroup.Miscellaneous));
        foreach (GodotScriptActor orphan in GetChildren().OfType<GodotScriptActor>().ToArray())
            if (GodotObject.IsInstanceValid(orphan)) orphan.Free();
        baddyConstruction?.Dispose();
    }

    private void DestroyOwnedBodies(SourceActorList list)
    {
        foreach (GodotScriptActor actor in list.SourceOrder.OfType<GodotScriptActor>())
        {
            if (!Retire(actor)) continue;
            if (GodotObject.IsInstanceValid(actor)) actor.Free();
            DeletedCount++;
        }
    }
    public void FlashCompass() => RequireControlServices().FlashCompass();
    public bool PlayerCeilingCheck(short minimum, short maximum)
    {
        if (minimum > maximum) throw new InvalidDataException("Player ceiling range is reversed.");
        return Player is { } player && player.Controller.OnCeiling && player.Controller.Position.Y >= (minimum << 12) &&
            player.Controller.Position.Y <= (maximum << 12);
    }
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to)
    {
        using var query = PhysicsRayQueryParameters3D.Create(PsxCoordinates.FixedWorldPosition(from),
            PsxCoordinates.FixedWorldPosition(to), CollisionLayers.LineOfSightTerrain);
        query.HitBackFaces = false;
        query.CollideWithAreas = false;
        using var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0;
    }
}
