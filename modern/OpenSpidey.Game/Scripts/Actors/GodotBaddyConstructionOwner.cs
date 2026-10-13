using Godot;
using OpenSpidey.Core.Actors;

namespace OpenSpidey.Game.Actors;

/// <summary>Pairs genuine CBaddy allocation scopes with registry deletion and native destruction.</summary>
public sealed class GodotBaddyConstructionOwner : IDisposable
{
    private readonly GodotActorRegistry registry;
    private readonly Dictionary<GodotScriptActor, BaddyConstructionLease> leases = [];
    private readonly Stack<BaddyConstructionLease> constructing = [];
    private bool disposed;
    public BaddyConstructionCount State { get; }
    public int OwnedCount => leases.Count;

    public static bool IsSupportedBaddyType(short sourceType) => sourceType is 203 or 402 or 304 or 312 or 305 or 315 or 319;

    public GodotBaddyConstructionOwner(GodotActorRegistry registry, BaddyConstructionCount state)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        State = state ?? throw new ArgumentNullException(nameof(state));
        registry.ActorRemoved += ReleaseSourceActor;
    }

    public int RequireCurrentOrdinal()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return constructing.TryPeek(out BaddyConstructionLease? lease) ? lease.Ordinal :
            throw new InvalidOperationException("Source CBaddy ordinal requires an active base-constructor allocation scope.");
    }
    public byte RequireCurrentPhase() => unchecked((byte)RequireCurrentOrdinal());

    /// <summary>Called around the real factory, before its derived constructor and before registry adoption.</summary>
    public T Create<T>(short sourceType, Func<T> factory) where T : GodotScriptActor
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(factory);
        if (!IsSupportedBaddyType(sourceType))
            throw new NotSupportedException($"Source type {sourceType} is outside the proven CBaddy constructor set; CBody types 401 and 407 never acquire this count.");
        BaddyConstructionLease lease = State.BeginConstruction();
        constructing.Push(lease);
        GodotScriptActor? allocated = null;
        bool tracked = false;
        try
        {
            T actor = factory();
            allocated = actor;
            if (!GodotObject.IsInstanceValid(actor) || actor.IsQueuedForDeletion() || actor.SourceActorType != sourceType)
                throw new InvalidDataException("The CBaddy factory did not return its actual live source-family actor.");
            if (!MatchesFamily(sourceType, actor))
                throw new InvalidDataException("CBaddy ordinal must belong to the real corresponding native actor adapter.");
            leases.Add(actor, lease);
            tracked = true;
            var observer = new GodotBaddyConstructionLifetime
            {
                Name = "SourceBaddyDestructor",
                ReleaseConstruction = () => ReleaseSourceActor(actor)
            };
            actor.AddChild(observer);
            return actor;
        }
        catch
        {
            if (tracked && allocated is not null) leases.Remove(allocated);
            lease.Dispose();
            throw;
        }
        finally
        {
            BaddyConstructionLease popped = constructing.Pop();
            if (!ReferenceEquals(popped, lease)) throw new InvalidOperationException("Nested source constructor scopes are unbalanced.");
        }
    }

    private static bool MatchesFamily(short sourceType, GodotScriptActor actor) => sourceType switch
    {
        203 => actor is GodotControlActor,
        402 => actor is GodotPlatformActor,
        304 or 312 => actor is GodotThugActor,
        305 or 315 => actor is GodotHostageActor,
        319 => actor is GodotBlackCatActor,
        _ => false
    };

    public void ReleaseSourceActor(GodotScriptActor actor)
    {
        // Registry removal is the source Delete phase; the later native Free must not decrement again.
        if (leases.Remove(actor, out BaddyConstructionLease? lease)) lease.Dispose();
    }

    public void Dispose()
    {
        if (disposed) return;
        if (constructing.Count != 0) throw new InvalidOperationException("Cannot retire an owner during a CBaddy constructor.");
        disposed = true;
        registry.ActorRemoved -= ReleaseSourceActor;
        // Live native actors still own their leases. Their pre-delete observers release them on actual teardown.
    }
}
