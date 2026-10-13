using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Player;

/// <summary>Composes live player contacts, interactions and the final source particle update phase.</summary>
public partial class GodotPlayerCombatRuntime : Node3D
{
    private GodotActorRegistry registry = null!;
    private GodotPlayerMeleeNativeServices services = null!;
    private Func<uint> clock = null!;
    private int physicsTicks;
    private GodotCombatSceneOwnership sceneOwnership = null!;
    private bool ownsSceneOwnership;
    private bool disposed;

    public GodotCombatSceneOwnership SceneOwnership => sceneOwnership;
    public GodotPlayerMeleeDirectory Directory => sceneOwnership.Directory;
    public GodotPlayerMeleeSession Session { get; private set; } = null!;
    public GodotPlayerKillQuips KillQuips { get; private set; } = null!;
    public GodotPlayerCombatImpacts Impacts { get; private set; } = null!;
    public GodotBouncingRocks Rocks { get; private set; } = null!;
    public GodotItemShatter ItemShatter { get; private set; } = null!;
    public SourceParticleScheduler Particles => sceneOwnership.Particles;
    public GodotControllerFeedback ControllerFeedback { get; private set; } = null!;
    public GodotActorSessionDirectory Actors => sceneOwnership.Actors;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public GodotGlowFlash Glows { get; private set; } = null!;
    public bool AutoAimReticlePresent => services.HasReticle;
    public void DeleteAutoAimReticle() => services.DeleteReticle();
    internal void CreateAutoAimReticle() => services.CreateReticle();
    public event Action<Exception>? Faulted;

    public static GodotPlayerCombatRuntime Create(PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, GodotPlayer player, GodotActorRegistry registry,
        Func<ImportedLevelWorld> currentWorld, PlayerFollowCamera camera, GodotSpeechPlayer speech,
        Func<uint> sourceClock, GodotCombatSceneOwnership? sceneOwnership = null)
    {
        ArgumentNullException.ThrowIfNull(sourceClock);
        GodotCombatSceneOwnership ownership = sceneOwnership ?? new(registry);
        ownership.RequireRegistry(registry);
        var runtime = new GodotPlayerCombatRuntime
        {
            Name = "PlayerCombatRuntime",
            registry = registry,
            clock = sourceClock,
            sceneOwnership = ownership,
            ownsSceneOwnership = sceneOwnership is null,
            // Actors run first; the original miscellaneous particle phase follows their updates.
            ProcessPhysicsPriority = 100
        };
        try
        {
            runtime.ControllerFeedback = new() { Name = "ControllerFeedback", Clock = sourceClock };
            runtime.AddChild(runtime.ControllerFeedback);
            runtime.Glows = new() { Name = "SharedGlowEffects" };
            runtime.AddChild(runtime.Glows);
            runtime.Glows.SourceBitCreated += bit => runtime.Particles.Attach(SourceParticleList.Glow, bit);
            runtime.Glows.SourceBitRemoved += identity => runtime.Particles.Detach(identity);
            player.Combat.Vibration = () => runtime.ControllerFeedback.Available;
            player.Combat.PadActuator = runtime.ControllerFeedback.Activate;
            runtime.Session = new(registry, player, currentWorld);
            runtime.KillQuips = new(player, speech, sourceClock,
                () => runtime.Directory.SourceBodies.Select(body => new PlayerKillQuipEnemy(body.BodyFlags,
                    GodotActorCollisionDirectory.Snapshot(body.Native).Health, body.BehaviorFlags)).ToArray(), registry.RandomSource);
            GodotActorWorldQuery query = ownership.World;
            runtime.World = query;
            runtime.ItemShatter = GodotItemShatter.Create((start, end) =>
                query.Trace(start, end, includeEnvironmentalActors: true)?.Position.Y ?? int.MaxValue,
                registry.RandomSource);
            runtime.AddChild(runtime.ItemShatter);
            registry.ActorRemoved += runtime.ForgetShatterOwner;
            byte[] bits = archive.Read("data/bits.psx");
            runtime.Rocks = GodotBouncingRocks.Create(bits);
            runtime.AddChild(runtime.Rocks);
            runtime.Impacts = GodotPlayerCombatImpacts.Create(bits, sourceClock, registry.RandomSource,
                position => GodotBouncingRocks.GetGroundY(query, position), runtime.Rocks.Emit);
            runtime.AddChild(runtime.Impacts);
            runtime.Impacts.Rings.SourceBitCreated += bit => runtime.Particles.Attach(SourceParticleList.Flat,bit);
            runtime.Impacts.Lines.SourceBitCreated += bit => runtime.Particles.Attach(SourceParticleList.GLine,bit);
            runtime.Rocks.SourceBitCreated += bit => runtime.Particles.Attach(SourceParticleList.Flat,bit);
            runtime.ItemShatter.ChunkEmitted += bit => runtime.Particles.Attach(SourceParticleList.Chunk,bit);
            runtime.ItemShatter.GlassEmitted += bit => runtime.Particles.Attach(SourceParticleList.Glass,bit);
            FixedJointTransform TargetingRotation() => FixedRotation.TransposeRotation(player.Controller.Orientation.Basis);
            runtime.services = new(player, runtime.Directory, query, new(archive, repository),
                repository.Load("items"), runtime.Impacts, original, runtime.Session, sourceClock,
                TargetingRotation, () => camera.SourcePosition >> 12, () => camera.SourceViewRotation);
            player.ConfigureMelee(new()
            {
                FightData = PlayerFightData.Decode(original.DistanceDefinitions.Span, original.ComboDefinitions.Span),
                Hooks = original.Hooks,
                PoseLinks = original.PoseLinks,
                Services = runtime.services,
                SenseState = new(player.Combat.State, runtime.KillQuips.Rules, new()),
                TargetingRotation = TargetingRotation,
                Random = registry.RandomSource,
                CreateInteractions = melee => new(melee, runtime.Session, runtime.Directory, new()
                {
                    SwitchToStandMode = player.Combat.SwitchToStandMode,
                    CheckJump = () => player.Controller.MeleeCheckJump(),
                    PutCameraBehind = player.Combat.PutCameraBehind
                })
            });
            return runtime;
        }
        catch
        {
            runtime.Free();
            throw;
        }
    }

    public void ShatterPlatform(GodotPlatformActor actor, bool checkSeen, bool markProcessed) =>
        ItemShatter.Shatter(actor, actor.SourceModelLibrary, actor.ModelIndex, actor.Motion.Position,
            actor.Motion.Angles, checkSeen, markProcessed, registry.ObjectTextures());

    private void ForgetShatterOwner(GodotScriptActor actor) => ItemShatter.Forget(actor);

    public override void _PhysicsProcess(double delta)
    {
        if (++physicsTicks % 2 != 0) return;
        try
        {
            Particles.Move(clock());
            Particles.RemoveDead();
        }
        catch (Exception exception)
        {
            SetPhysicsProcess(false);
            if (Faulted is null) GD.PushError(exception.ToString());
            else Faulted(exception);
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        registry.ActorRemoved -= ForgetShatterOwner;
        services?.Dispose();
        Session?.Dispose();
        if (ownsSceneOwnership) sceneOwnership.Dispose();
    }
}
