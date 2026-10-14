using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cops;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Presentation;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

public interface IGodotHostageSceneCamera
{
    int SelectViewingNode(FixedVector3 shooter, FixedVector3 hostage);
}

/// <summary>Real scene owners available during source autoexec and initial actor construction, before CPlayer.</summary>
public class GodotEnemyPreparationContext
{
    public required PkrArchive Archive { get; init; }
    public required PsxLibraryRepository Repository { get; init; }
    public required ReadOnlyMemory<byte> OriginalExecutable { get; init; }
    public required GodotActorRegistry Registry { get; init; }
    public required GodotSoundChannels Sounds { get; init; }
    public required GodotSpeechPlayer Speech { get; init; }
    public GodotCombatSceneOwnership? SceneOwnership { get; init; }
    public required Func<ImportedLevelWorld> CurrentWorld { get; init; }
    public required PsxAssetRegions Regions { get; init; }
    public required GodotEnvironmentVisibility EnvironmentVisibility { get; init; }
    public required SourceVertexLightingState LightingState { get; init; }
    public required Func<uint> Clock { get; init; }
    public required Func<int> AttackClock { get; init; }
    public required Func<int> TotalTime { get; init; }
    public required Action<uint> TriggerCommandPoint { get; init; }
    public IGodotHostageSceneCamera? HostageCamera { get; init; }
    public bool UnloadCodeModulesOnDestroy { get; init; } = true;
}

/// <summary>Compatibility composition for scenes whose actual player has already been constructed.</summary>
public sealed class GodotEnemyRuntimeContext : GodotEnemyPreparationContext
{
    public required GodotPlayerCombatRuntime Combat { get; init; }
    public required GodotPlayerWebRuntime Web { get; init; }
    public required PlayerFollowCamera Camera { get; init; }
}

/// <summary>Actual post-census player owners; binding never constructs a substitute player.</summary>
public sealed class GodotEnemyPlayerBinding
{
    public required GodotPlayer Player { get; init; }
    public required GodotPlayerCombatRuntime Combat { get; init; }
    public required GodotPlayerWebRuntime Web { get; init; }
    public required PlayerFollowCamera Camera { get; init; }
    public IGodotHostageSceneCamera? HostageCamera { get; init; }
}

/// <summary>Production original312/304/319 model, AI, web, grenade, sound and source-list composition. Authored189 owns module loading.</summary>
public partial class GodotEnemyRuntime : Node3D
{
    private GodotEnemyPreparationContext context = null!;
    private GodotEnemyPlayerBinding? playerBinding;
    private Exception? bindingFailure;
    private GodotGrenadeEnvironment? environment;
    private GodotGrenadeSceneServices? grenades;
    private GodotGrenadeWorldListBinding? bulletBinding;
    private readonly HashSet<object> ownedBits = new(ReferenceEqualityComparer.Instance);
    private bool disposed;
    public GodotThugVisualEffects Visuals { get; private set; } = null!;
    public GodotExplosionFragments Fragments { get; private set; } = null!;
    public GodotGrenadeEnvironment Environment => environment ?? throw new InvalidOperationException("Source grenade environment requires post-census player binding.");
    public GodotGrenadeSceneServices Grenades => grenades ?? throw new InvalidOperationException("Source grenades require post-census player binding.");
    public GodotActorSessionServices Session { get; private set; } = null!;
    public GodotThugNativeServices NativeServices { get; private set; } = null!;
    public GodotThugConfiguration Thugs { get; private set; } = null!;
    public GodotBlackCatConfiguration BlackCat { get; private set; } = null!;
    public GodotHostageConfiguration Hostages { get; private set; } = null!;
    public GodotIdleNpcConfiguration IdleNpcs { get; private set; } = null!;
    public GodotTorchConfiguration Torch { get; private set; } = null!;
    public GodotLizManConfiguration LizMan { get; private set; } = null!;
    public GodotSimbyConfiguration Simby { get; private set; } = null!;
    public GodotRhinoConfiguration Rhino { get; private set; } = null!;
    public GodotVenomConfiguration Venom { get; private set; } = null!;
    public GodotCopConfiguration Cop { get; private set; } = null!;
    public GodotChopperConfiguration Chopper { get; private set; } = null!;
    public CopCombatCoordinator CopCombat { get; private set; } = null!;
    public GodotSimbyVertexWibbler SimbyVertexWibbler { get; private set; } = null!;
    public GodotBossGlobalOwner BossGlobal { get; private set; } = null!;
    public GodotActorCodeModules Modules { get; private set; } = null!;
    public GodotActorVisibility Visibility { get; private set; } = null!;
    public ThugCombatCoordinator Encounter { get; } = new();
    public GodotCombatSceneOwnership SceneOwnership { get; private set; } = null!;
    public bool IsPlayerBound => playerBinding is not null && bindingFailure is null;

    public static GodotEnemyRuntime Create(GodotEnemyRuntimeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        GodotEnemyRuntime runtime = Prepare(context, context.SceneOwnership ?? context.Combat.SceneOwnership);
        try
        {
            runtime.BindPlayer(new()
            {
                Player = context.Registry.Player ?? throw new InvalidOperationException("Original enemy AI requires the scene's actual player."),
                Combat = context.Combat, Web = context.Web, Camera = context.Camera, HostageCamera = context.HostageCamera
            });
            return runtime;
        }
        catch { runtime.Free(); throw; }
    }

    public static GodotEnemyRuntime Prepare(GodotEnemyPreparationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Prepare(context, context.SceneOwnership ?? throw new ArgumentException("Pre-player enemy construction requires the canonical scene body/bit owners.", nameof(context)));
    }

    private static GodotEnemyRuntime Prepare(GodotEnemyPreparationContext context, GodotCombatSceneOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(context);
        GodotActorRegistry registry = context.Registry;
        ownership.RequireRegistry(registry);
        var runtime = new GodotEnemyRuntime { Name = "OriginalEnemyModules", context = context, SceneOwnership = ownership };
        try
        {
            runtime.Visibility = new(() => runtime.RequirePlayerBinding().Camera.GetViewport().GetCamera3D() ??
                throw new InvalidOperationException("No native camera owns the current level view."));
            runtime.Visuals = GodotThugVisualEffects.Create(context.Archive, context.Repository, context.Sounds.Controller,
                runtime.Listener, registry.RandomSource, context.Clock);
            runtime.AddChild(runtime.Visuals);
            runtime.Visuals.SourceBitCreated += runtime.AttachBit;
            runtime.Visuals.SourceBitRemoved += runtime.DetachBit;
            runtime.Fragments = GodotExplosionFragments.Create(context.Archive.Read("data/bits.psx"));
            runtime.AddChild(runtime.Fragments);
            runtime.Fragments.SourceBitCreated += runtime.AttachFlatBit;
            runtime.Fragments.SourceBitRemoved += runtime.DetachBit;
            var commands = new GodotThugScriptCommands(registry);
            runtime.Session = new(ownership.Actors, new()
            {
                DomeCount = () => runtime.RequirePlayerBinding().Web.Domes!.DomeCount,
                IgnoreSphereRadius = () => runtime.RequirePlayerBinding().Combat.Session.IgnoreSphereRadius,
                PlayerWebComboValue = () => runtime.RequirePlayerBinding().Player.Combat.State.Field528,
                SpawnGrenade = (actor, request) => runtime.Grenades.Spawn(actor, request),
                NotifyKill = type => runtime.RequirePlayerBinding().Combat.KillQuips.NotifyKill(unchecked((ushort)type)),
                GrabUpdate = runtime.GrabUpdate,
                RecoverWeb = (actor, tug) => runtime.RequirePlayerBinding().Web.Contacts.RecoverContact(actor, tug),
                SetPlayerWebCombo = (duration, amount) =>
                {
                    GodotPlayer player = runtime.RequirePlayerBinding().Player;
                    player.Combat.State.Field534 = duration;
                    player.Combat.State.Field52C = amount;
                },
                SelectHostageCamera = (shooter, hostage) => (runtime.RequirePlayerBinding().HostageCamera ?? context.HostageCamera ??
                    throw new NotSupportedException("Hostage shooting scenes require the original optimum-viewing-node camera host.")).SelectViewingNode(shooter, hostage),
                ExecuteScriptCommand = (actor, opcode, operands, environment) => actor is GodotThugActor thug &&
                    commands.TryExecute(thug, opcode, operands, environment)
            });
            runtime.NativeServices = new(runtime.Visuals, context.Sounds.Controller, runtime.Listener,
                context.Speech, () => registry.Player, () => ownership.Actors.SourceBaddyBodies, runtime.Session, ownership.Actors);
            runtime.Thugs = new()
            {
                ModelFactory = runtime.Visuals.Resources.CreateModel, PlayerProvider = () => registry.Player, Combat = runtime.Encounter,
                SourceBaddyOrder = runtime.SourceThugs, Effects = runtime.NativeServices,
                SpitParticles = runtime.Visuals.SpitParticles, SpriteRings = runtime.Visuals.SpriteRings,
                AttackClock = context.AttackClock, Timer = context.Clock, Random = registry.RandomSource,
                SourceConstructionOrdinal = registry.BaddyConstruction.RequireCurrentOrdinal,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags)
            };
            var cinematic = new GodotBlackCatResources(context.Repository, context.OriginalExecutable);
            runtime.BlackCat = new()
            {
                ModelFactory = cinematic.CreateModel, PoseData = cinematic.ReadPoseData,
                ShadowTexture = context.Repository.RequireLoaded("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
                LightingState = context.LightingState, Clock = context.Clock, FastForwardRequested = () => registry.Environment.FastForwardDeath,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags),
                SourceShadowCreated = runtime.AttachQuadBit, SourceShadowRemoved = runtime.DetachBit
            };
            var hostages = new GodotHostageResources(context.Archive, context.Repository);
            runtime.Hostages = new()
            {
                ModelFactory = hostages.CreateModel, CollisionBoxes = hostages.CollisionBoxes,
                PlayerProvider = () => registry.Player,
                ShadowTexture = context.Repository.RequireLoaded("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
                SourceBaddyOrder = () => ownership.Actors.SourceBaddyBodies,
                Speech = context.Speech, Session = runtime.Session, Clock = context.Clock,
                AttackClock = context.AttackClock, Random = registry.RandomSource,
                SourceConstructionOrdinal = registry.BaddyConstruction.RequireCurrentOrdinal,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags)
            };
            var idleNpcs = new GodotIdleNpcResources(context.Repository, context.OriginalExecutable);
            runtime.IdleNpcs = new()
            {
                ModelFactory = idleNpcs.CreateModel, Light = idleNpcs.ReadLight, LightingState = context.LightingState,
                Clock = context.Clock, FastForwardRequested = () => registry.Environment.FastForwardDeath,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags)
            };
            var torch = new GodotTorchResources(context.Repository, context.OriginalExecutable);
            runtime.Torch = new()
            {
                ModelFactory = torch.CreateModel, SourceData = torch.ReadSourceData,
                ShadowTexture = context.Repository.RequireLoaded("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
                LightingState = context.LightingState, Clock = context.Clock, FastForwardRequested = () => registry.Environment.FastForwardDeath,
                // PreProcessWibblyTextures UV animation belongs to the unported texture-wibble renderer; ENV kind6 packets share the gap.
                AdvanceTextureWibble = _ => { },
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags),
                SourceShadowCreated = runtime.AttachQuadBit, SourceShadowRemoved = runtime.DetachBit
            };
            var lizMan = new GodotLizManResources(context.Repository, context.OriginalExecutable);
            runtime.LizMan = new()
            {
                ModelFactory = lizMan.CreateModel, Light = lizMan.ReadLight, LightingState = context.LightingState,
                Clock = context.Clock, AttackClock = context.AttackClock, TotalTime = context.TotalTime,
                Random = registry.RandomSource,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags)
            };
            runtime.BossGlobal = GodotBossGlobalOwner.Create();
            runtime.AddChild(runtime.BossGlobal);
            var simby = new GodotSimbyResources(context.Repository, context.OriginalExecutable);
            runtime.SimbyVertexWibbler = new(context.Repository);
            runtime.AddChild(runtime.SimbyVertexWibbler);
            runtime.Simby = new()
            {
                ModelFactory = simby.CreateSimbyModel, PunchObModelFactory = simby.CreatePunchObModel,
                PosePacket = simby.ReadPosePacket, LightingState = context.LightingState,
                Clock = context.Clock, AttackClock = context.AttackClock, Random = registry.RandomSource,
                World = new(), BossGlobal = runtime.BossGlobal,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags),
                IsPunchObCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags),
                EnsureVertexWibbler = () => runtime.SimbyVertexWibbler.Ensure(registry.Environment.LevelId, registry.RandomSource)
            };
            var rhino = new GodotRhinoResources(context.Repository, context.OriginalExecutable);
            var rhinoHealthBars = new BossHealthBars<RhinoState, object>(body => body.Health);
            runtime.Rhino = new()
            {
                ModelFactory = rhino.CreateModel, Light = rhino.ReadLight, LightingState = context.LightingState,
                Clock = context.Clock, AttackClock = context.AttackClock, Random = registry.RandomSource,
                SpeechTable = rhino.ReadSpeechTable(), WallChunks = rhino.ReadWallChunks(), SourceClock = context.Clock,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags),
                CreateHealthBar = state => CreateRhinoHealthBar(context, rhinoHealthBars, state)
            };
            var venom = new GodotVenomResources(context.Repository, context.OriginalExecutable);
            runtime.Venom = new()
            {
                ModelFactory = venom.CreateModel, Light = venom.ReadLight, LightingState = context.LightingState,
                DifficultyLevel = () => registry.Difficulty,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags)
            };
            var copResources = new GodotCopResources(context.Archive, context.Repository);
            runtime.CopCombat = new(runtime.Encounter);
            runtime.Cop = new()
            {
                ModelFactory = copResources.CreateModel, PlayerProvider = () => registry.Player, CopCombat = runtime.CopCombat,
                SharedCombat = runtime.Encounter, AttackClock = context.AttackClock, Timer = context.Clock, Random = registry.RandomSource,
                SourceConstructionOrdinal = registry.BaddyConstruction.RequireCurrentOrdinal,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags),
                Session = runtime.Session,
                PlayPositionSound = (sound, position, pitch) => runtime.NativeServices.PlayPositionSound(sound, position, pitch)
            };
            var chopperResources = new GodotChopperResources(context.Archive, context.Repository);
            runtime.Chopper = new()
            {
                ModelFactory = chopperResources.CreateModel, PlayerProvider = () => registry.Player, Timer = context.Clock,
                SourceConstructionOrdinal = registry.BaddyConstruction.RequireCurrentOrdinal,
                IsSourceCulled = actor => runtime.Visibility.IsCulled(actor, actor.Model, actor.ItemFlags)
            };
            runtime.Modules = new(registry, runtime.Thugs, runtime.BlackCat, runtime.Hostages, runtime.IdleNpcs, runtime.Torch,
                runtime.LizMan, runtime.Simby, runtime.Rhino, runtime.Venom, runtime.Cop, runtime.Chopper);
            runtime.Modules.RegisterFactories();
            return runtime;
        }
        catch { runtime.Free(); throw; }
    }

    /// <summary>Call after the original initial actor census and actual CPlayer/CCamera construction, before AI.</summary>
    public void BindPlayer(GodotEnemyPlayerBinding binding)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(binding);
        if (playerBinding is not null || bindingFailure is not null)
            throw new InvalidOperationException("Original enemy player binding is once-only.", bindingFailure);
        if (!ReferenceEquals(context.Registry.Player, binding.Player) ||
            !ReferenceEquals(binding.Combat.SceneOwnership, SceneOwnership) ||
            binding.Player.Melee is null || binding.Web.Domes is null)
            throw new ArgumentException("Enemy AI requires the actual registry player and canonical combat, Grab and web-dome owners.", nameof(binding));
        SceneOwnership.RequireRegistry(context.Registry);
        try
        {
            playerBinding = binding;
            var crates = new GodotCrateEffects(binding.Combat.ItemShatter, Fragments, Visuals.Glows,
                EnvironmentTextures, context.TriggerCommandPoint, context.Registry.RandomSource);
            environment = new(context.CurrentWorld, context.Repository, path => context.Archive.Read(path),
                binding.Combat.Session, crates, context.EnvironmentVisibility);
            grenades = new(new()
            {
                ProjectileRoot = this, World = binding.Combat.World, Actors = SceneOwnership.Actors,
                Baddies = SceneOwnership.Directory, Player = binding.Player, Repository = context.Repository,
                Regions = context.Regions, ObjectLibrary = context.Registry.ObjectLibrary,
                Timer = () => unchecked((int)context.Clock()), TotalTime = context.TotalTime, Visuals = Visuals,
                Sounds = context.Sounds.Controller, Listener = Listener, Environment = Environment
            });
            bulletBinding = new(context.Registry, Grenades, Visuals.Explosions);
        }
        catch (Exception exception) { bindingFailure = exception; throw; }
    }

    private GodotEnemyPlayerBinding RequirePlayerBinding()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (bindingFailure is not null) throw new InvalidOperationException("Original enemy player binding failed.", bindingFailure);
        return playerBinding ?? throw new InvalidOperationException("This source enemy action requires CPlayer/CCamera after the initial actor census.");
    }

    private bool GrabUpdate(ref FixedVector3 position, ref short yaw) =>
        RequirePlayerBinding().Player.Melee!.ZipDive.GrabUpdate(ref position, ref yaw);

    public IReadOnlyList<GodotThugActor> SourceThugs() => context.Registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder
        .OfType<GodotThugActor>().Where(context.Registry.Owns).ToArray();

    /// <summary>Called once by the session's source render pass, after it establishes actual culling and actor-list order.</summary>
    public void SynchronizeSourceLighting()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        foreach (GodotScriptActor actor in context.Registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder.OfType<GodotScriptActor>())
        {
            if (!context.Registry.Owns(actor)) continue;
            switch (actor)
            {
                case GodotBlackCatActor cat: cat.SynchronizeSourceLighting(); break;
                case GodotIdleNpcActor npc: npc.SynchronizeSourceLighting(); break;
                case GodotTorchActor torch: torch.SynchronizeSourceLighting(); break;
                case GodotLizManActor lizMan: lizMan.SynchronizeSourceLighting(); break;
                case GodotSimbyActor simby: simby.SynchronizeSourceLighting(); break;
                case GodotRhinoActor rhino: rhino.SynchronizeSourceLighting(); break;
                case GodotVenomActor venom: venom.SynchronizeSourceLighting(); break;
            }
        }
    }

    private SoundListener Listener() => new(RequirePlayerBinding().Camera.SourcePosition >> 12);
    private static void CreateRhinoHealthBar(GodotEnemyPreparationContext context, BossHealthBars<RhinoState, object> bars, RhinoState state)
    {
        foreach (PsxAssetLibrary bank in context.Repository.Loaded.Values)
        {
            string path = bank.AssetPath.Replace('\\', '/');
            int slash = path.LastIndexOf('/');
            string file = slash < 0 ? path : path[(slash + 1)..];
            if (!file.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase)) continue;
            string stem = file[..^4];
            byte[] manifest;
            try { manifest = context.Archive.Read($"data/{stem.ToLowerInvariant()}.tex"); }
            catch (Exception) { continue; }
            try
            {
                bars.Create(state, RhinoState.Type, name =>
                {
                    uint checksum = NamedTextureTable.FindFirstSourceName(manifest, name);
                    return bank.Textures.First(texture => texture.Checksum == checksum);
                });
                return;
            }
            catch (KeyNotFoundException) { }
        }
        throw new NotSupportedException(
            "Panel_CreateHealthBar type307 requires a loaded mission G bank with rhino/boss/rhino_wounded (0x463610).");
    }
    private IReadOnlyList<OpenSpidey.Assets.Textures.DecodedTexture> EnvironmentTextures(GodotGrenadeEnvironmentItem item)
    {
        if (!context.Repository.Loaded.TryGetValue(item.Bank.AssetPath, out PsxAssetLibrary? bank) || !ReferenceEquals(bank, item.Bank))
            throw new InvalidOperationException("Crate shatter references an unloaded source bank generation.");
        string shared = bank.AssetPath[..^6] + "_L.psx";
        return context.Repository.Loaded.TryGetValue(shared, out PsxAssetLibrary? textures)
            ? textures.Textures.Concat(bank.Textures).ToArray() : bank.Textures;
    }
    private void AttachFlatBit(SourceParticleRegistration bit) => AttachBit(SourceParticleList.Flat, bit);
    private void AttachQuadBit(SourceParticleRegistration bit) => AttachBit(SourceParticleList.Quad, bit);
    private void AttachBit(SourceParticleList list, SourceParticleRegistration bit)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SceneOwnership.Particles.Attach(list, bit);
        ownedBits.Add(bit.Identity);
    }
    private void DetachBit(object identity)
    {
        if (ownedBits.Remove(identity)) SceneOwnership.Particles.Detach(identity);
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        if (context.UnloadCodeModulesOnDestroy && Modules is not null && GodotObject.IsInstanceValid(context.Registry))
            foreach (var module in Modules.Modules.SourceOrder) Modules.Modules.Unload(module.Name);
        grenades?.Dispose();
        bulletBinding?.Dispose();
        if (Visuals is not null)
        {
            Visuals.SourceBitCreated -= AttachBit; Visuals.SourceBitRemoved -= DetachBit;
        }
        if (Fragments is not null)
        {
            Fragments.SourceBitCreated -= AttachFlatBit; Fragments.SourceBitRemoved -= DetachBit;
        }
        foreach (object identity in ownedBits) SceneOwnership.Particles.Detach(identity);
        ownedBits.Clear();
    }
}
