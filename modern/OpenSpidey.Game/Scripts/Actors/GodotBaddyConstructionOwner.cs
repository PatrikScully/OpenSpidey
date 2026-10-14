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

    public static bool IsSupportedBaddyType(short sourceType) =>
        sourceType is 203 or 402 or 304 or 312 or 305 or 315 or 319 or 310 or 316 or 303 or 326 or 719 or 328 or 317 or 324 or 412 or 307 or 313 or 306 or 320 or 318 or 322 or 323;

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
            if (!GodotObject.IsInstanceValid(actor) || actor.IsQueuedForDeletion() || actor.SourceActorType != SourceBodyType(sourceType))
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
        306 or 320 => actor is GodotCopActor,
        305 or 315 => actor is GodotHostageActor,
        318 => actor is GodotChopperActor,
        322 => actor is GodotChopperSearchlightActor,
        323 => actor is GodotChopperSniperActor,
        319 => actor is GodotBlackCatActor,
        310 or 316 => actor is GodotScorpionEncounterActor,
        303 or 326 or 719 => actor is GodotIdleNpcActor,
        328 => actor is GodotTorchActor,
        317 => actor is GodotLizManActor,
        324 => actor is GodotSimbyActor,
        412 => actor is GodotPunchObActor,
        307 => actor is GodotRhinoActor,
        313 => actor is GodotVenomActor,
        _ => false
    };
    /// <summary>CSubmariner's constructor stores the CLizard type326 for trigger type719.</summary>
    private static short SourceBodyType(short sourceType) => sourceType == 719 ? (short)326 : sourceType;

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
