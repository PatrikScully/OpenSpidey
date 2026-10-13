using Godot;
using System.Runtime.CompilerServices;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Required native platform fields and its distinct CPlatform::Hit override.</summary>
public interface IGodotActorSessionPlatformHost
{
    ActorCollisionBody Snapshot(GodotPlatformActor actor);
    bool Hit(GodotPlatformActor actor, ActorHit hit);
}

/// <summary>Scene generation plus a recoverable native pointer. Failed recovery clears only the pointer.</summary>
public sealed class GodotActorSessionHandle
{
    internal WeakReference<GodotScriptActor>? Pointer { get; private set; }
    public uint Validation { get; }
    public bool HasPointer => Pointer is not null;
    internal GodotActorSessionHandle(GodotScriptActor actor, uint validation)
    { Pointer = new(actor); Validation = validation; }
    internal void ClearPointer() => Pointer = null;
}

/// <summary>Scene actor identities, source-list ownership and cross-family virtual/message routing.</summary>
public sealed class GodotActorSessionDirectory : IDisposable
{
    private readonly GodotActorRegistry registry;
    private readonly GodotPlayerMeleeDirectory melee;
    private readonly IGodotActorSessionPlatformHost platforms;
    private readonly Dictionary<GodotScriptActor, uint> generations = [];
    private sealed record StateOwner(WeakReference<GodotScriptActor> Actor, uint Validation);
    private readonly ConditionalWeakTable<ActorProcedureState, StateOwner> stateOwners = new();
    private readonly Dictionary<GodotScriptActor, ActorProcedureState> bodyViews = [];
    private readonly HashSet<GodotScriptActor> constructing = [];
    private readonly Dictionary<GodotScriptActor, GodotNativeDestructionObserver> lifetimeObservers = [];
    private uint nextGeneration;
    private bool disposed;
    public GodotActorRegistry Registry => registry;
    public IReadOnlyList<ActorCollisionBody> SourceBaddyBodies => GodotActorCollisionDirectory.SourceBaddyBodies(melee);

    public GodotActorSessionDirectory(GodotActorRegistry registry, GodotPlayerMeleeDirectory melee,
        IGodotActorSessionPlatformHost platforms)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.melee = melee ?? throw new ArgumentNullException(nameof(melee));
        this.platforms = platforms ?? throw new ArgumentNullException(nameof(platforms));
        registry.Spawned += OnSpawned; registry.ActorRemoved += Remove;
        // Melee list lifecycle binding remains owned by the canonical scene body/list owner.
        foreach (ActorUpdateGroup group in Enum.GetValues<ActorUpdateGroup>())
            foreach (GodotScriptActor actor in registry.SourceActors(group)) OnSpawned(actor);
        foreach (GodotPlayerMeleeDirectory.Body body in melee.SourceBodies) RegisterIdentity(body.Native);
        foreach (GodotSwitchActor control in melee.SourceControls) RegisterIdentity(control);
    }

    /// <summary>Source constructors attach before native registry adoption; detach also handles constructor failure.</summary>
    public void AttachSourceBody(GodotScriptActor actor)
    {
        CheckAlive(); RequireBodyFamily(actor);
        if (!registry.Owns(actor) && !melee.Contains(actor)) constructing.Add(actor);
        RegisterIdentity(actor);
        if (!melee.Contains(actor)) melee.RegisterBody(actor);
    }
    public void DetachSourceBody(GodotScriptActor actor) => Remove(actor);

    public void RegisterConstructingActor(GodotScriptActor actor)
    {
        CheckAlive(); RequireKnownFamily(actor);
        if (!registry.Owns(actor) && !melee.Contains(actor)) constructing.Add(actor);
        RegisterIdentity(actor);
    }
    private void OnSpawned(GodotScriptActor actor)
    {
        // Other independently ported scene families may coexist; they get no unsupported body adapter here.
        if (!KnownFamily(actor)) return;
        RegisterIdentity(actor); constructing.Remove(actor);
        if (IsBaddyBody(actor) && !melee.Contains(actor)) melee.RegisterBody(actor);
    }
    private void RegisterIdentity(GodotScriptActor actor)
    {
        if (!GodotObject.IsInstanceValid(actor) || actor.IsQueuedForDeletion())
            throw new ArgumentException("Source identity requires an allocated native actor.", nameof(actor));
        if (generations.ContainsKey(actor)) return;
        if (++nextGeneration == 0) throw new InvalidOperationException("Scene actor generation counter exhausted.");
        generations.Add(actor, nextGeneration);
        var observer = new GodotNativeDestructionObserver { Name = "SourceActorDirectoryLifetime", Destroyed = () => Remove(actor) };
        lifetimeObservers.Add(actor, observer);
        try
        {
            actor.AddChild(observer);
            if (actor is GodotThugActor thug) RegisterStateOwner(thug.Actor, actor);
            else if (actor is GodotHostageActor hostage) RegisterStateOwner(hostage.Actor, actor);
        }
        catch { Remove(actor); throw; }
    }
    public void Remove(GodotScriptActor actor)
    {
        StopObserving(actor);
        generations.Remove(actor); constructing.Remove(actor); bodyViews.Remove(actor);
        melee.Remove(actor);
        // Keep weak procedure identity tombstones: a captured state must never acquire a replacement owner.
    }
    private bool Live(GodotScriptActor actor) => GodotObject.IsInstanceValid(actor) && !actor.IsQueuedForDeletion() &&
        (registry.Owns(actor) || melee.Contains(actor) || constructing.Contains(actor));

    public GodotActorSessionHandle StoreHandle(GodotScriptActor actor)
    {
        CheckAlive(); RequireKnownFamily(actor);
        if (!Live(actor)) throw new ArgumentException("Actor handle requires this scene's allocated source identity.", nameof(actor));
        RegisterIdentity(actor);
        return new(actor, generations[actor]);
    }
    public GodotScriptActor? RecoverHandle(GodotActorSessionHandle handle)
    {
        CheckAlive(); ArgumentNullException.ThrowIfNull(handle);
        if (handle.Pointer is { } pointer && pointer.TryGetTarget(out GodotScriptActor? actor) && Live(actor) &&
            generations.TryGetValue(actor, out uint generation) && generation == handle.Validation) return actor;
        handle.ClearPointer(); return null;
    }
    public GodotScriptActor? RecoverNative(object? handle)
    {
        CheckAlive();
        return handle switch
        {
            null => null,
            GodotActorSessionHandle typed => RecoverHandle(typed),
            PlayerKickHandle kick => melee.Recover(ref kick),
            ActorProcedureState state => RecoverNativeActor(state),
            GodotScriptActor actor => Live(actor) ? actor : null,
            // Existing source message producers pass native sender pointers before storing a handle.
            WeakReference<GodotThugActor> weak => weak.TryGetTarget(out GodotThugActor? actor) && Live(actor) ? actor : null,
            _ => throw new NotSupportedException($"Actor recovery has no source handle adapter for {handle.GetType().Name}.")
        };
    }
    public ActorProcedureState? RecoverActor(object? handle)
    {
        GodotScriptActor? actor = RecoverNative(handle);
        if (actor is null) return null;
        if (actor is GodotThugActor thug) return thug.Actor;
        if (actor is GodotHostageActor hostage) return hostage.Actor;
        // Body-only views bridge the required host's procedure-state identity to real CBody motion;
        // they do not execute a fabricated baddy procedure list for environmental objects or chunks.
        ActorBodyMotion body = RequireNativeBody(actor);
        if (!bodyViews.TryGetValue(actor, out ActorProcedureState? view))
        {
            RegisterIdentity(actor);
            bodyViews.Add(actor, view = new(body)); RegisterStateOwner(view, actor);
        }
        return view;
    }
    public GodotScriptActor? RecoverNativeActor(ActorProcedureState state)
    {
        CheckAlive(); ArgumentNullException.ThrowIfNull(state);
        if (!stateOwners.TryGetValue(state, out StateOwner? owner))
            throw new ArgumentException("Procedure identity is outside this scene directory.", nameof(state));
        return owner.Actor.TryGetTarget(out GodotScriptActor? actor) && Live(actor) &&
            generations.TryGetValue(actor, out uint validation) && validation == owner.Validation ? actor : null;
    }
    private void RegisterStateOwner(ActorProcedureState state, GodotScriptActor actor)
    {
        uint validation = generations[actor];
        if (stateOwners.TryGetValue(state, out StateOwner? existing))
        {
            if (existing.Validation != validation || !existing.Actor.TryGetTarget(out GodotScriptActor? previous) || !ReferenceEquals(previous, actor))
                throw new InvalidOperationException("A captured procedure-state identity cannot acquire another source lifetime.");
            return;
        }
        stateOwners.Add(state, new(new(actor), validation));
    }
    private void StopObserving(GodotScriptActor actor)
    {
        if (!lifetimeObservers.Remove(actor, out GodotNativeDestructionObserver? observer)) return;
        bool inNativeDestruction = observer.Destroyed is null;
        observer.Destroyed = null;
        // The observer clears its callback before invoking us from NotificationPredelete.
        if (!inNativeDestruction && GodotObject.IsInstanceValid(observer)) observer.Free();
    }
    public ActorBodyMotion? RecoverBody(ActorProcedureState state) =>
        RecoverNativeActor(state) is { } actor ? RequireNativeBody(actor) : null;
    private static ActorBodyMotion RequireNativeBody(GodotScriptActor actor) => actor switch
    {
        GodotThugActor thug => thug.Actor.Body,
        GodotHostageActor hostage => hostage.Actor.Body,
        GodotBlackCatActor cat => cat.Body,
        GodotManipulationDebris debris => debris.Body,
        IGodotEnvironmentalActor environmental => environmental.SupportMotion,
        _ => throw new NotSupportedException($"Source actor {actor.GetType().Name} has no physical CBody motion.")
    };

    public void SendMessage(GodotScriptActor sender, object? targetHandle, int code)
    {
        GodotScriptActor? target = RecoverNative(targetHandle);
        if (target is null) return;
        GodotActorSessionHandle source = StoreHandle(sender);
        switch (target)
        {
            case GodotThugActor thug: thug.ReceiveMessage(code, source); break;
            case GodotHostageActor hostage: hostage.ReceiveMessage(code, source); break;
            case GodotBlackCatActor cat: cat.ReceiveMessage(code, source); break;
            default: throw new NotSupportedException($"Source message{code} recipient {target.GetType().Name} has no CMessage queue.");
        }
    }
    public bool ApplyHit(object identity, ActorHit hit)
    {
        CheckAlive();
        if (identity is ActorCollisionBody snapshot) identity = snapshot.Identity;
        if (ReferenceEquals(identity, registry.Player)) return registry.Player!.Combat.Hit(hit);
        GodotScriptActor? target = RecoverNative(identity);
        if (target is null) return false;
        return target switch
        {
            GodotThugActor thug => thug.Hit(hit),
            GodotHostageActor hostage => hostage.Hit(hit),
            GodotBlackCatActor cat => cat.Hit(hit),
            GodotManipulationDebris debris => debris.Hit(hit),
            GodotManipulationActor manipulation => manipulation.Hit(hit),
            GodotPlatformActor platform => platforms.Hit(platform, hit),
            _ => throw new NotSupportedException($"Source {target.GetType().Name} has no Hit virtual adapter.")
        };
    }
    public ActorCollisionBody DescribeEnvironmentalContact(GodotActorWorldHit hit)
    {
        CheckAlive(); ArgumentNullException.ThrowIfNull(hit);
        if (hit.SupportId is not { } id || GodotObject.InstanceFromId(id) is not GodotScriptActor actor || !Live(actor))
            throw new InvalidDataException("Environmental recoil contact requires a real owned CBody collider identity.");
        return actor switch
        {
            GodotManipulationActor manipulation => new(manipulation, 401, manipulation.Body.Position,
                manipulation.Variables.CollisionRadius, manipulation.State.BodyFlags, manipulation.Variables.Health),
            GodotPlatformActor platform => ValidatePlatformSnapshot(platform),
            _ => throw new NotSupportedException($"Environmental contact family {actor.GetType().Name} has no source metadata adapter.")
        };
    }
    private ActorCollisionBody ValidatePlatformSnapshot(GodotPlatformActor platform)
    {
        ActorCollisionBody snapshot = platforms.Snapshot(platform);
        if (!ReferenceEquals(snapshot.Identity, platform) || snapshot.Type != 402 || snapshot.Position != platform.SourcePosition)
            throw new InvalidDataException("Platform metadata must describe the actual source402 body.");
        return snapshot;
    }
    public void SendSignal(GodotScriptActor source)
    {
        RequireSignalSource(source);
        foreach (ushort link in source.Definition.Links)
        {
            if (link >= registry.Triggers.Nodes.Count) throw new InvalidDataException($"Signal references missing node{link}.");
            if (registry.Triggers.Nodes[link].TypeCode is not (1 or 7)) continue;
            // Source list order is BaddyList, ControlBaddyList, EnviroList. Registered nested
            // fixture bodies/controls share these actual lists; no substitute registry is created.
            foreach (GodotPlayerMeleeDirectory.Body body in melee.SourceBodies)
                if (body.Native.SourceNodeId == link) body.Native.ScriptState.PendingSignals |= 1;
            foreach (GodotSwitchActor control in melee.SourceControls)
                if (control.SourceNodeId == link) control.ScriptState.PendingSignals |= 1;
            foreach (GodotScriptActor actor in registry.SourceActors(ActorUpdateGroup.Control))
                if (actor.SourceNodeId == link && actor is not GodotSwitchActor) actor.ScriptState.PendingSignals |= 1;
            foreach (GodotScriptActor actor in registry.SourceActors(ActorUpdateGroup.Environment))
                if (actor.SourceNodeId == link) actor.ScriptState.PendingSignals |= 1;
        }
    }
    public void SendDeathPulse(GodotScriptActor source)
    {
        RequireSignalSource(source);
        if (source.SourceNodeId == ushort.MaxValue) return;
        foreach (ushort link in source.Definition.Links) registry.ReceivePulse(link);
    }
    private void RequireSignalSource(GodotScriptActor source)
    {
        CheckAlive(); if (!Live(source)) throw new ArgumentException("Mission signal requires an allocated scene actor.", nameof(source));
        if (source.Definition is null) throw new NotSupportedException("Transient CBody has no authored mission links.");
    }
    private static bool IsBaddyBody(GodotScriptActor actor) => actor is GodotThugActor or GodotHostageActor or GodotBlackCatActor or GodotManipulationDebris;
    private static bool KnownFamily(GodotScriptActor actor) => IsBaddyBody(actor) || actor is GodotManipulationActor or GodotPlatformActor or GodotSwitchActor;
    private static void RequireKnownFamily(GodotScriptActor actor)
    { if (!KnownFamily(actor)) throw new NotSupportedException($"Scene identity family {actor.GetType().Name} has no supported source adapter."); }
    private static void RequireBodyFamily(GodotScriptActor actor)
    { if (!IsBaddyBody(actor)) throw new NotSupportedException("Source BaddyList attachment requires thug, hostage or transient chunk."); }
    private void CheckAlive() => ObjectDisposedException.ThrowIf(disposed, this);
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        registry.Spawned -= OnSpawned; registry.ActorRemoved -= Remove;
        foreach (GodotScriptActor actor in lifetimeObservers.Keys.ToArray()) StopObserving(actor);
        generations.Clear(); constructing.Clear(); stateOwners.Clear(); bodyViews.Clear();
    }
}
