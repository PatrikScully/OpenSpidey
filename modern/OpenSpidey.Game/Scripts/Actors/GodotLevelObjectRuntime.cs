using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Gameplay;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Scene-owned composition of source ENV switches, carryable OBJ bodies and the level-one bomb.</summary>
public partial class GodotLevelObjectRuntime : Node3D, IManipulationDebrisHost
{
    private GodotLevelObjectPreparationContext context = null!;
    private GodotPlayerCombatRuntime? combat;
    private PlayerFollowCamera? camera;
    private GodotSwitchEnvironment? environment;
    private GodotActorWorldQuery worldQuery = null!;
    private bool disposed;
    public GodotSwitchEnvironment Environment => EnsureEnvironment();
    public GodotSwitchEnvironment? PreparedEnvironment => environment;
    public bool IsPlayerBound => combat is not null;
    public GodotManipulationEffects Effects { get; private set; } = null!;
    public GodotBigExplosions Explosions { get; private set; } = null!;
    public event Action<uint, FixedVector3, uint>? PositionSoundPlayed;

    public static GodotLevelObjectRuntime Create(PkrArchive archive, PsxLibraryRepository repository,
        ImportedLevelWorld world, GodotActorRegistry registry, GodotPlayerCombatRuntime combat,
        GodotSoundChannels sounds, PlayerFollowCamera camera, MissionCountdown countdown,
        ReadOnlyMemory<byte> triggerData, Func<uint> clock, GodotEnvironmentVisibility visibility)
    {
        GodotLevelObjectRuntime runtime = Prepare(new()
        {
            Archive = archive, Repository = repository, CurrentWorld = () => world, Registry = registry,
            SceneOwnership = combat.SceneOwnership, Sounds = sounds, Listener = () => new(camera.SourcePosition >> 12),
            Countdown = countdown, TriggerData = triggerData, Clock = clock, Visibility = visibility
        });
        try { runtime.BindPlayer(combat, camera); runtime.PrepareEnvironment(); return runtime; }
        catch { runtime.Free(); throw; }
    }

    /// <summary>Register real401/407/600 constructors before autoexec and initial actors, without constructing a player.</summary>
    public static GodotLevelObjectRuntime Prepare(GodotLevelObjectPreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.SceneOwnership.RequireRegistry(context.Registry);
        var runtime = new GodotLevelObjectRuntime
        {
            Name = "LevelObjects", context = context, worldQuery = context.SceneOwnership.World
        };
        try
        {
            runtime.InitializeEffects();
            context.Registry.RegisterFactory(401, (node, owner) =>
                GodotManipulationActor.Create(node, owner, runtime.CreateManipulationConfiguration()));
            context.Registry.RegisterFactory(600, (node, owner) =>
                GodotManipulationActor.Create(node, owner, runtime.CreateManipulationConfiguration()));
            context.Registry.RegisterFactory(407, (node, owner) => GodotSwitchActor.Create(node, owner, new()
            {
                Environment = runtime.EnsureEnvironment(),
                AttachToControlList = actor =>
                {
                    context.SceneOwnership.Actors.RegisterConstructingActor(actor);
                    context.SceneOwnership.Directory.RegisterControl(actor);
                },
                DetachFromControlList = context.SceneOwnership.Actors.Remove,
                HasPlayer = () => context.Registry.Player is { } player && GodotObject.IsInstanceValid(player),
                ReadPlayerCounter = () => context.Registry.Player!.Combat.State.SwitchCounter,
                WritePlayerCounter = value => context.Registry.Player!.Combat.State.SwitchCounter = value
            }));
            return runtime;
        }
        catch { runtime.Free(); throw; }
    }

    /// <summary>Bind actual player services after the census, retaining all previously claimed ENV and actor identities.</summary>
    public void BindPlayer(GodotPlayerCombatRuntime combat, PlayerFollowCamera camera)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(combat); ArgumentNullException.ThrowIfNull(camera);
        if (this.combat is not null) throw new InvalidOperationException("Source level object player binding is once-only.");
        if (!ReferenceEquals(combat.SceneOwnership, context.SceneOwnership) || !ReferenceEquals(combat.World, worldQuery) ||
            !ReferenceEquals(camera.Target, context.Registry.Player))
            throw new ArgumentException("Level objects require the actual registry player and canonical scene body owners.");
        this.combat = combat; this.camera = camera;
        if (environment is not null) combat.Session.RegisterSwitchEnvironment(environment);
    }

    /// <summary>Prepare the actual ENV only after its source library command has loaded the world.</summary>
    public void PrepareEnvironment() => _ = EnsureEnvironment();

    private GodotPlayerCombatRuntime RequireCombat()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return combat ?? throw new InvalidOperationException("This source object action requires CPlayer after the initial actor census.");
    }

    private GodotPlayer RequirePlayer()
    {
        _ = RequireCombat();
        return context.Registry.Player is { } player && GodotObject.IsInstanceValid(player)
            ? player : throw new InvalidOperationException("The original level object action requires a live CPlayer.");
    }

    private GodotSwitchEnvironment EnsureEnvironment()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (environment is not null) return environment;
        ImportedLevelWorld world = context.CurrentWorld();
        ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase));
        PsxAssetLibrary geometry = context.Repository.RequireLoaded(source.AssetPath);
        var textures = context.Repository.Load(source.AssetPath[..^6] + "_L.psx").Textures.Concat(geometry.Textures).ToArray();
        GodotSwitchEnvironment prepared = GodotSwitchEnvironment.Create(world, geometry, textures, context.Archive.Read(source.AssetPath));
        prepared.ItemClaimed += context.Visibility.TransferFlagsToClaimedItem;
        AddChild(prepared);
        environment = prepared;
        combat?.Session.RegisterSwitchEnvironment(prepared);
        return prepared;
    }

    private IReadOnlyList<ManipulationTargetBody> Baddies() => context.SceneOwnership.Directory.SourceBodies.Select(body =>
        new ManipulationTargetBody(new(body.Identity, body.Position, body.Radius, body.BodyFlags), body.Type)).ToArray();

    private void PlayAt(uint sound, FixedVector3 position, int pitch)
    {
        if (pitch != 0) throw new NotSupportedException("Source manipulation positional sounds require their original zero pitch offset.");
        uint handle = context.Sounds.Controller.PlayAt(sound, position, context.Listener());
        PositionSoundPlayed?.Invoke(sound, position, handle);
    }

    private void InitializeEffects()
    {
        GodotActorRegistry registry = context.Registry;
        GodotCombatSceneOwnership ownership = context.SceneOwnership;
        Explosions = GodotBigExplosions.Create(context.Archive.Read("data/expgrnd.psx"));
        AddChild(Explosions);
        Explosions.ModelAttached += model => registry.SourceList(ActorUpdateGroup.Bullet)
            .Attach(model, () => Explosions.AdvanceCapturedModel(model));
        Explosions.ModelDetached += model => registry.SourceList(ActorUpdateGroup.Bullet).Detach(model);
        Effects = GodotManipulationEffects.Create(context.Archive.Read("data/bits.psx"), new()
        {
            Registry = registry,
            Debris = new() { WorldQueries = this, AttachToBaddyList = ownership.Actors.AttachSourceBody,
                DetachFromBaddyList = ownership.Actors.DetachSourceBody },
            SourceBaddyOrder = Baddies, PlayerPosition = () => RequirePlayer().Controller.Position,
            CameraPositionWholeUnits = () => context.Listener().WholePosition,
            GroundHeight = (position, above, below, flags) => flags == 0
                ? worldQuery.GroundHeight(position, above, below)
                : throw new NotSupportedException("Manipulation ground queries require the original flags0 policy."),
            PlayPositionSound = (sound, position, pitch) => PlayAt(sound, position, pitch),
            SendExplosionMessage = (target, position) =>
            {
                switch (target)
                {
                    case GodotThugActor thug: thug.ReceiveMessage(10, null, position); break;
                    case GodotHostageActor hostage: hostage.ReceiveMessage(10, null, position); break;
                    case GodotBlackCatActor cat: cat.ReceiveMessage(10, null, position); break;
                    case IGodotSourceBaddyBody baddy: baddy.ReceiveMessage(10, null, position); break;
                    default: throw new NotSupportedException($"Original message10 with its position vector is not ported for {target.GetType().Name}.");
                }
            },
            CreateBigExplosion = position => Explosions.Emit(position, registry.RandomSource),
            HitPlayer = hit => RequirePlayer().Combat.Hit(hit),
            HitBaddy = (target, hit) => ownership.Actors.ApplyHit(target, hit),
            ReactSymbioteToExplosion = target =>
            {
                if (target is GodotSimbyActor simby) OpenSpidey.Core.Actors.Simby.SimbyReactions.ExplosionReaction(simby.State);
                else throw new NotSupportedException($"The source324 explosion reaction is not ported for {target.GetType().Name}.");
            }
        });
        AddChild(Effects);
        Effects.Smoke.SourceBitCreated += bit => ownership.Particles.Attach(SourceParticleList.Flat, bit);
        Effects.Smoke.SourceBitRemoved += identity => ownership.Particles.Detach(identity);
    }

    private GodotManipulationConfiguration CreateManipulationConfiguration() => new()
    {
        Environment = EnsureEnvironment(),
        AttachToEnvironmentalList = context.SceneOwnership.Actors.RegisterConstructingActor,
        DetachFromEnvironmentalList = context.SceneOwnership.Actors.Remove,
        SourceBaddyOrder = Baddies, IgnoreSphereRadius = () => RequireCombat().Session.IgnoreSphereRadius,
        LineToItem = LineToItem, HitBaddy = (target, hit) => context.SceneOwnership.Actors.ApplyHit(target, hit),
        ShakeCamera = (position, magnitude) => (camera ?? throw new InvalidOperationException("Source object camera is not bound.")).Shake(position, magnitude),
        ImpactEffects = Effects,
        LevelOneBomb = new()
        {
            OriginalTriggerData = context.TriggerData, Countdown = context.Countdown, Clock = context.Clock,
            World = worldQuery, Actors = context.SceneOwnership.Actors, PlayPositionSound = PlayAt
        }
    };

    public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (mode != 1) throw new InvalidDataException("Original manipulation probes require source mode1.");
        GodotPlayerCombatRuntime bound = RequireCombat();
        GodotActorWorldHit? hit = worldQuery.Trace(start, end);
        return hit is null ? null : new(bound.Session.ResolveWorldContact(hit), hit.Position,
            PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal,
                hit.Collider is ImportedCollisionBody body ? body.SourceRotation : PsxCoordinates.OriginalRotation(hit.Collider.GlobalBasis)));
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
    }
}
