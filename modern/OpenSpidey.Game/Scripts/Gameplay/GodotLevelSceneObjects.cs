using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.UI;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Gameplay;

public enum LevelSceneObjectPhase
{
    Player, Camera, Enemy, Control, Environment, Background, PowerUp, Bullet, Suspended, Miscellaneous, Web, Bits, Messages
}

/// <summary>One real destructor invocation, retaining its source-list phase and identity for native lifetime diagnostics.</summary>
public readonly record struct LevelSceneObjectDeletion(LevelSceneObjectPhase Phase, object Identity);

public sealed class GodotLevelSceneObjectServices
{
    public required Func<GodotPlayer?> Player { get; init; }
    public required PlayerFollowCamera Camera { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required GodotPlayerWebRuntime Web { get; init; }
    public required GodotPlayerCombatRuntime Combat { get; init; }
    public required GodotLevelObjectRuntime Objects { get; init; }
    public required GodotEnemyRuntime Enemies { get; init; }
    public required GodotSpeechPlayer Speech { get; init; }
    public required GodotMissionMessages Messages { get; init; }
    /// <summary>Stops the actual scene clock, mission/player simulation and cinematic callbacks before any source owner is released.</summary>
    public required Action StopProcessing { get; init; }
    /// <summary>Clears the live scene's player pointer after its actual native destruction.</summary>
    public required Action ClearPlayer { get; init; }
}

/// <summary>Init_KillAll's captured source lists and real native destructors; retry UI, static world and archive ownership stay with the scene.</summary>
public sealed class GodotLevelSceneObjects
{
    private readonly GodotLevelSceneObjectServices services;
    private readonly List<LevelSceneObjectDeletion> deletions = [];
    private bool killing;
    public bool IsDestroyed { get; private set; }
    public LevelExitReason? ExitReason { get; private set; }
    public LevelSceneObjectPhase? CurrentPhase { get; private set; }
    public IReadOnlyList<LevelSceneObjectDeletion> Deletions => deletions.AsReadOnly();
    public event Action<LevelSceneObjectPhase>? PhaseStarted;

    public GodotLevelSceneObjects(GodotLevelSceneObjectServices services)
    {
        this.services = services ?? throw new ArgumentNullException(nameof(services));
        ArgumentNullException.ThrowIfNull(services.Player); ArgumentNullException.ThrowIfNull(services.Camera);
        ArgumentNullException.ThrowIfNull(services.Registry); ArgumentNullException.ThrowIfNull(services.Web);
        ArgumentNullException.ThrowIfNull(services.Combat); ArgumentNullException.ThrowIfNull(services.Objects);
        ArgumentNullException.ThrowIfNull(services.Enemies); ArgumentNullException.ThrowIfNull(services.Speech);
        ArgumentNullException.ThrowIfNull(services.Messages); ArgumentNullException.ThrowIfNull(services.StopProcessing);
        ArgumentNullException.ThrowIfNull(services.ClearPlayer);
        if (!ReferenceEquals(services.Combat.Actors.Registry, services.Registry) ||
            !ReferenceEquals(services.Enemies.Grenades.Registry, services.Registry))
            throw new ArgumentException("Scene teardown requires the one canonical actor/combat/enemy registry.", nameof(services));
    }

    public void KillAllSceneObjects(LevelExitReason reason)
    {
        if (IsDestroyed) return;
        if (killing) throw new InvalidOperationException("Scene destruction cannot re-enter its captured source list traversal.");
        killing = true;
        ExitReason = reason;
        services.StopProcessing();
        Stop(services.Registry); Stop(services.Web); Stop(services.Combat); Stop(services.Objects); Stop(services.Enemies); Stop(services.Speech);
        GodotPlayer? player = services.Player();
        if (player is not null && GodotObject.IsInstanceValid(player)) Stop(player);
        if (GodotObject.IsInstanceValid(services.Camera)) Stop(services.Camera);

        Begin(LevelSceneObjectPhase.Player);
        if (player is not null && GodotObject.IsInstanceValid(player))
        {
            if ((player.ActorState.BodyFlags & 0x20) != 0)
                throw new InvalidOperationException("Init_KillAll retained a protected source player.");
            // CPlayer::~CPlayer deletes these protected companions before the global Bit_DeleteAll.
            if (player.Combat.State.LoopingSoundHandle is int sound && sound != 0)
                player.Combat.StopSound(sound);
            player.Combat.State.LoopingSoundHandle = player.Combat.State.Field528 = player.Combat.State.Field52C = 0;
            services.Web.Swing?.Dispose();
            foreach (WebStrandOwner web in services.Web.Strands.Owners.ToArray()) web.Dispose();
            if (services.Web.SmokeTrails is { } smoke && GodotObject.IsInstanceValid(smoke)) smoke.Free();
            Free(player);
        }
        services.ClearPlayer();

        Begin(LevelSceneObjectPhase.Camera); Free(services.Camera);
        DeleteGroup(LevelSceneObjectPhase.Enemy, ActorUpdateGroup.Enemy);
        DeleteGroup(LevelSceneObjectPhase.Control, ActorUpdateGroup.Control);
        DeleteGroup(LevelSceneObjectPhase.Environment, ActorUpdateGroup.Environment);
        DeleteGroup(LevelSceneObjectPhase.Background, ActorUpdateGroup.Background);
        DeleteGroup(LevelSceneObjectPhase.PowerUp, ActorUpdateGroup.PowerUp);
        DeleteGroup(LevelSceneObjectPhase.Bullet, ActorUpdateGroup.Bullet);
        Begin(LevelSceneObjectPhase.Suspended); DeleteList(services.Registry.SuspendedSourceList);
        DeleteGroup(LevelSceneObjectPhase.Miscellaneous, ActorUpdateGroup.Miscellaneous);
        Begin(LevelSceneObjectPhase.Web);
        foreach (WebStrandOwner web in services.Web.Strands.Owners.ToArray())
            if ((web.BodyFlags & 0x20) == 0) { web.Dispose(); Record(web); }

        AssertActorListsEmpty();
        Begin(LevelSceneObjectPhase.Bits);
        // Renderer destruction invokes the real bit owners while their source scheduler and registry remain allocated.
        Free(services.Enemies);
        Free(services.Objects);
        Free(services.Web);
        foreach (Node child in services.Combat.GetChildren().ToArray()) Free(child);
        services.Combat.Particles.DeleteAll();
        Free(services.Combat);
        Begin(LevelSceneObjectPhase.Messages);
        services.Messages.DeleteAllMessages();
        services.Speech.Controller.Stop();
        IsDestroyed = true;
    }

    private void DeleteGroup(LevelSceneObjectPhase phase, ActorUpdateGroup group)
    {
        Begin(phase);
        DeleteList(services.Registry.SourceList(group));
        // Ordinary AI deletion already detached these CBody identities, but Godot's queued native Free may still be pending.
        foreach (GodotScriptActor orphan in services.Registry.GetChildren().OfType<GodotScriptActor>()
            .Where(actor => actor.UpdateGroup == group && !services.Registry.Owns(actor)).ToArray())
            if (GodotObject.IsInstanceValid(orphan)) Free(orphan);
    }

    private void DeleteList(SourceActorList list)
    {
        // DeleteList in Init_KillAll captures mNextItem before the virtual deleting destructor.
        foreach (object identity in list.SourceOrder)
        {
            if (!list.SourceOrder.Any(current => ReferenceEquals(current, identity))) continue;
            if ((BodyFlags(identity) & 0x20) != 0) continue;
            switch (identity)
            {
                case GodotScriptActor actor when services.Registry.Owns(actor):
                    services.Registry.DestroyImmediately(actor); Record(actor); break;
                case Node node:
                    Free(node); break;
                case WebDome or WebDomePiece or WebDomeRing:
                    if (!services.Web.Domes!.DestroySourceBody(identity))
                        throw new InvalidOperationException("A source dome identity lost its genuine deleting destructor.");
                    Record(identity); break;
                default: throw new NotSupportedException($"Source scene destructor for {identity.GetType().Name} has not been ported.");
            }
            if (list.SourceOrder.Any(current => ReferenceEquals(current, identity)))
                throw new InvalidOperationException("The real source body destructor did not detach its own list identity.");
        }
    }

    private ushort BodyFlags(object identity) => identity switch
    {
        GodotThugActor actor => unchecked((ushort)actor.State.BodyFlags),
        GodotHostageActor actor => unchecked((ushort)actor.BodyFlags),
        GodotBlackCatActor actor => unchecked((ushort)actor.State.BodyFlags),
        GodotScorpionEncounterActor actor => actor.State.BodyFlags,
        IGodotSourceBaddyBody actor => actor.BodyFlags,
        GodotManipulationDebris actor => actor.BodyFlags,
        GodotManipulationActor actor => actor.State.BodyFlags,
        GodotPlatformActor actor => actor.BodyFlags,
        GodotSwitchActor actor => actor.State.BodyFlags,
        GodotPowerUpActor actor => actor.BodyFlags,
        GodotEnvironmentChunkControl actor => actor.BodyFlags,
        // CControl's native adapter only changes lifecycle bits; its CBody constructor never sets protection20.
        GodotControlActor actor => (ushort)(actor.Definition.ClearsBodyFlag2 ? 0x14 : 0x16),
        GodotGrenadeProjectile actor => actor.State.BodyFlags,
        // The genuine C3DExplosion constructor has unprotected CBody flags16; protection belongs its bit companions.
        GodotExplosionModel => 0x16,
        GodotSourceBackdrop backdrop => unchecked((ushort)backdrop.State.BodyFlags),
        WebDome or WebDomePiece or WebDomeRing => (services.Web.Domes ??
            throw new InvalidOperationException("Misc dome bodies require their genuine renderer owner.")).SourceBodyFlags(identity),
        _ => throw new NotSupportedException($"Source body protection metadata for {identity.GetType().Name} has not been ported.")
    };

    private void AssertActorListsEmpty()
    {
        if (services.Registry.LiveCount != 0 || services.Registry.SuspendedSourceList.Count != 0 ||
            Enum.GetValues<ActorUpdateGroup>().Any(group => services.Registry.SourceList(group).Count != 0) ||
            services.Web.Strands.Owners.Count != 0 || services.Registry.BaddyConstructionState.Count != 0)
            throw new InvalidOperationException($"Init_KillAll retained source owners: active/suspended={services.Registry.LiveCount}, " +
                $"suspended-head={services.Registry.SuspendedSourceList.Count}, leases={services.Registry.BaddyConstructionState.Count}, " +
                $"webs={services.Web.Strands.Owners.Count}; " + string.Join(", ", Enum.GetValues<ActorUpdateGroup>()
                    .Select(group => $"{group}={services.Registry.SourceList(group).Count}")));
    }
    private void Begin(LevelSceneObjectPhase phase) { CurrentPhase = phase; PhaseStarted?.Invoke(phase); }
    private void Free(Node node)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        node.Free(); Record(node);
    }
    private void Record(object identity) => deletions.Add(new(CurrentPhase ?? throw new InvalidOperationException("Source deletion has no list phase."), identity));
    private static void Stop(Node node)
    {
        if (!GodotObject.IsInstanceValid(node)) return;
        node.SetProcess(false); node.SetPhysicsProcess(false);
    }
}
