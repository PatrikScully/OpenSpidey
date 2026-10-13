using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Live environmental list ownership and the player's shared manipulation/lookaround state.</summary>
public sealed class GodotPlayerMeleeSession : IGodotPlayerMeleeSessionHost, IDisposable
{
    /// <summary>Stable identity of an actual imported static item, shared by its collision face groups.</summary>
    public sealed record WorldItem(string SourceAsset, int InstanceIndex, int ModelIndex, uint? ModelChecksum);

    private sealed record Handle(WeakReference<GodotScriptActor> Actor, uint Validation);
    private readonly GodotActorRegistry registry;
    private readonly GodotPlayer player;
    private readonly Func<ImportedLevelWorld> currentWorld;
    private readonly List<GodotSwitchEnvironment> switchEnvironments = [];
    private readonly Dictionary<ImportedLevelSource, Dictionary<int, WorldItem>> worldItems = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<GodotScriptActor, uint> validations = [];
    private readonly Dictionary<Node, GodotNativeDestructionObserver> destructionObservers = [];
    private uint nextValidation;
    private GodotScriptActor? heldActor;
    private bool disposed;

    public GodotPlayerMeleeSession(GodotActorRegistry registry, GodotPlayer player, Func<ImportedLevelWorld> currentWorld)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.currentWorld = currentWorld ?? throw new ArgumentNullException(nameof(currentWorld));
        registry.ActorRemoved += OnActorRemoved;
    }

    public int Difficulty => registry.Difficulty;
    // Source +0x8EA is the lookaround gate. Wall/ceiling attachment does not set it.
    public byte SurfaceTransition => player.Combat.State.LookaroundActive ? (byte)1 : (byte)0;
    public bool IgnoreSphereRadius { get; set; }
    public GodotScriptActor? HeldActor => heldActor;
    public IReadOnlyList<GodotSwitchEnvironment> SourceSwitchEnvironments => switchEnvironments;
    public PlayerKickObject? HeldObject => heldActor is null ? null : RequireEnvironmentalActor(heldActor).KickObject;

    public IReadOnlyList<PlayerKickObject> SourceEnvironmentalOrder => registry.SourceActors(ActorUpdateGroup.Environment)
        .Select(actor => RequireEnvironmentalActor(actor).KickObject).ToArray();

    public void Hold(GodotScriptActor actor)
    {
        PlayerKickObject item = RequireEnvironmentalActor(actor).KickObject;
        if (!registry.Owns(actor) || item.Type != 401)
            throw new ArgumentException("Held objects must be actual type401 actors owned by this session.", nameof(actor));
        if (heldActor is not null) throw new InvalidOperationException("The player already holds a source object.");
        heldActor = actor;
        player.Combat.State.HeldObjectPresent = true;
    }

    public void Release(GodotScriptActor actor)
    {
        if (!ReferenceEquals(heldActor, actor)) throw new InvalidOperationException("The released object is not held by this player.");
        heldActor = null;
        player.Combat.State.HeldObjectPresent = false;
    }

    public PlayerKickHandle MakeEnvironmentalHandle(PlayerKickObject target)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (target.Identity is not GodotScriptActor actor || !registry.Owns(actor))
            throw new ArgumentException("Environmental handles require an owned source actor.", nameof(target));
        RequireEnvironmentalActor(actor);
        if (!validations.TryGetValue(actor, out uint validation))
        {
            validation = unchecked(++nextValidation);
            if (validation == 0) throw new InvalidOperationException("Environmental handle validation counter exhausted.");
            validations.Add(actor, validation);
            ObserveDestruction(actor, () => ForgetActor(actor));
        }
        return new(new Handle(new(actor), validation), validation);
    }

    public GodotScriptActor? RecoverEnvironmentalHandle(ref PlayerKickHandle handle)
    {
        if (handle.Target is Handle stored && stored.Validation == handle.Validation &&
            stored.Actor.TryGetTarget(out GodotScriptActor? actor) && registry.Owns(actor) &&
            validations.TryGetValue(actor, out uint validation) && validation == stored.Validation)
            return actor;
        handle = handle with { Target = null };
        return null;
    }

    public void RegisterSwitchEnvironment(GodotSwitchEnvironment environment)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(environment);
        if (!GodotObject.IsInstanceValid(environment) || switchEnvironments.Contains(environment))
            throw new ArgumentException("Switch ENV ownership must be valid and registered once.", nameof(environment));
        switchEnvironments.Add(environment);
        ObserveDestruction(environment, () => switchEnvironments.Remove(environment));
    }

    public object ResolveWorldContact(GodotActorWorldHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        if (hit.SupportId is { } id)
        {
            if (GodotObject.InstanceFromId(id) is not GodotScriptActor actor || !registry.Owns(actor))
                throw new InvalidDataException("World contact has no owned live environmental actor.");
            RequireEnvironmentalActor(actor);
            return actor;
        }
        foreach (GodotSwitchEnvironment environment in switchEnvironments)
            if (environment.ResolveCollider(hit.Collider) is { } item) return item.State.Identity;

        ImportedLevelWorld world = currentWorld();
        ImportedLevelSource source = world.Sources.SingleOrDefault(source =>
            source.AssetPath.Equals(hit.SourceAsset, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("World contact references an unloaded source library.");
        if ((uint)hit.InstanceIndex >= source.Scene.Instances.Count)
            throw new InvalidDataException("World contact references a missing original instance.");
        int model = source.Scene.Instances[hit.InstanceIndex].ModelIndex;
        if (model != hit.ModelIndex || source.Scene.Models[model].Checksum != hit.ModelChecksum)
            throw new InvalidDataException("World contact model metadata differs from its original instance.");

        return ResolveStaticItem(source, hit.InstanceIndex);
    }

    /// <summary>Canonical identity of an actual static source instance, including items with no drawable mesh.</summary>
    public WorldItem ResolveStaticItem(ImportedLevelSource source, int instanceIndex)
    {
        ArgumentNullException.ThrowIfNull(source);
        ImportedLevelWorld world = currentWorld();
        if (!world.Sources.Any(live => ReferenceEquals(live, source)))
            throw new InvalidDataException("Static item identity references an unloaded source generation.");
        if ((uint)instanceIndex >= source.Scene.Instances.Count)
            throw new InvalidDataException("Static item identity references a missing original instance.");
        int model = source.Scene.Instances[instanceIndex].ModelIndex;

        foreach (ImportedLevelSource stale in worldItems.Keys.Where(source => !world.Sources.Any(live => ReferenceEquals(live, source))).ToArray())
            worldItems.Remove(stale);
        if (!worldItems.TryGetValue(source, out Dictionary<int, WorldItem>? items)) worldItems.Add(source, items = []);
        if (!items.TryGetValue(instanceIndex, out WorldItem? identity))
            items.Add(instanceIndex, identity = new(source.AssetPath, instanceIndex, model, source.Scene.Models[model].Checksum));
        return identity;
    }

    private void ObserveDestruction(Node owner, Action destroyed)
    {
        var observer = new GodotNativeDestructionObserver { Name = "MeleeSessionLifetime" };
        observer.Destroyed = () =>
        {
            destructionObservers.Remove(owner);
            destroyed();
        };
        destructionObservers.Add(owner, observer);
        owner.AddChild(observer);
    }

    private void ForgetActor(GodotScriptActor actor)
    {
        validations.Remove(actor);
        if (!ReferenceEquals(heldActor, actor)) return;
        heldActor = null;
        player.Combat.State.HeldObjectPresent = false;
    }

    private void OnActorRemoved(GodotScriptActor actor)
    {
        ForgetActor(actor);
        if (!destructionObservers.Remove(actor, out GodotNativeDestructionObserver? observer)) return;
        observer.Destroyed = null;
        if (GodotObject.IsInstanceValid(observer)) observer.Free();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        registry.ActorRemoved -= OnActorRemoved;
        foreach (GodotNativeDestructionObserver observer in destructionObservers.Values)
        {
            observer.Destroyed = null;
            if (GodotObject.IsInstanceValid(observer)) observer.Free();
        }
        destructionObservers.Clear();
        validations.Clear();
        switchEnvironments.Clear();
        worldItems.Clear();
        if (heldActor is not null) ForgetActor(heldActor);
    }

    private static IGodotEnvironmentalActor RequireEnvironmentalActor(GodotScriptActor actor) =>
        actor as IGodotEnvironmentalActor ?? throw new NotSupportedException($"Environmental actor {actor.GetType().Name} has no original body adapter.");
}
