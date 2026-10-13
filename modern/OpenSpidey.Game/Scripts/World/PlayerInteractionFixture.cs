using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Private original L1A3 world and actual source player/actor services, reusable by native contact proofs.</summary>
public sealed class PlayerInteractionFixture : IDisposable
{
    public SubViewport Viewport { get; }
    public Node3D Root { get; }
    public GodotPlayer Player { get; }
    public GodotPlayerMelee Melee => Player.Melee!;
    public GodotActorRegistry Registry { get; }
    public ImportedLevelWorld World { get; }
    public GodotActorWorldQuery WorldQuery { get; }
    public GodotSwitchEnvironment Environment { get; }
    public GodotPlayerMeleeDirectory Directory { get; } = new();
    public GodotPlayerMeleeSession Session { get; }
    public GodotPlayerMeleeNativeServices NativeServices { get; }
    public GodotPlayerCombatImpacts Impacts { get; }
    public GodotBouncingRocks Rocks { get; }
    public PlayerFollowCamera Camera { get; }
    public List<ushort> Pulses { get; } = [];
    public uint Clock { get; set; } = 1000;
    public int CameraBehindCalls { get; private set; }
    private readonly List<GodotManipulationActor> manipulationList = [];
    private bool disposed;

    public static PlayerInteractionFixture Create(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory, GodotThugResources thugResources, PsxAssetLibrary items, SoundChannelController? sourceSounds = null, Func<SoundListener>? sourceListener = null) =>
        new(owner, archive, repository, original, playerFactory, thugResources, items, sourceSounds, sourceListener);

    private PlayerInteractionFixture(Node3D owner, PkrArchive archive, PsxLibraryRepository repository,
        OriginalPlayerData original, Func<GodotPlayer> playerFactory, GodotThugResources thugResources, PsxAssetLibrary items, SoundChannelController? sourceSounds, Func<SoundListener>? sourceListener)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(playerFactory);
        Viewport = new() { Name = "OriginalL1A3PlayerInteractionWorld", OwnWorld3D = true, Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(Viewport); Root = new() { Name = "OriginalInteractionFixture" }; Viewport.AddChild(Root);
        try
        {
            World = ImportedLevelWorld.Load(repository, "data/L1A3_G.psx"); Root.AddChild(World);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
            Player = playerFactory();
            if (Player.Inventory is null || Player.Combat.Sound is null || Player.Combat.PositionSound is null)
                throw new InvalidOperationException("Private interaction player requires real inventory and native sound playback bindings.");
            Root.AddChild(Player); Player.SetPhysicsProcess(false); Player.Model.AdvanceAutomatically = false;
            Camera = new() { Target = Player, Current = true }; Root.AddChild(Camera); Camera.SetPhysicsProcess(false); Camera.MakeCurrent();
            Camera.GlobalPosition = Player.GlobalPosition + new Vector3(0, 3, 8); Camera.LookAt(Player.GlobalPosition);
            Player.Combat.CameraBehind = mode => { CameraBehindCalls++; Camera.PutBehind(mode); };
            Player.Combat.SourceTimer = () => Clock;
            PsxAssetLibrary geometry = repository.Load("data/L1A3_G.psx"), objects = repository.Load("data/L1A3_O.psx");
            var textures = repository.Load("data/L1A3_L.psx").Textures.Concat(geometry.Textures).Concat(objects.Textures).ToArray();
            Environment = GodotSwitchEnvironment.Create(World, geometry, textures, archive.Read("data/L1A3_G.psx")); Root.AddChild(Environment);
            Registry = new() { Triggers = triggers, Player = Player, Difficulty = 2, SuspensionDistance = uint.MaxValue,
                ObjectLibrary = () => objects, ObjectTextures = () => textures, ClaimObjectModel = World.ClaimObjectModel,
                Sounds = sourceSounds, SoundListener = sourceListener ?? (() => throw new InvalidOperationException("Fixture actor sound listener is not connected.")),
                Environment = new(LevelId: 0x103), ReceivePulse = Pulses.Add, RandomSource = new GameRandom(0x4B60FD).Next };
            Root.AddChild(Registry); Registry.SetPhysicsProcess(false); Directory.Bind(Registry);
            WorldQuery = new(Root, triggers); Session = new(Registry, Player, () => World); Session.RegisterSwitchEnvironment(Environment);
            byte[] bits = archive.Read("data/bits.psx"); Rocks = GodotBouncingRocks.Create(bits); Root.AddChild(Rocks);
            Impacts = GodotPlayerCombatImpacts.Create(bits, () => Clock, Registry.RandomSource,
                position => GodotBouncingRocks.GetGroundY(WorldQuery, position), Rocks.Emit); Root.AddChild(Impacts);
            NativeServices = new(Player, Directory, WorldQuery, thugResources, items, Impacts, original, Session, () => Clock,
                TargetingRotation, () => Camera.SourcePosition >> 12, ViewRotation);
            Player.ConfigureMelee(new() { FightData = PlayerFightData.Decode(original.DistanceDefinitions.Span, original.ComboDefinitions.Span),
                Hooks = original.Hooks, PoseLinks = original.PoseLinks, Services = NativeServices,
                SenseState = new(Player.Combat.State, new(new()), new()), TargetingRotation = TargetingRotation, Random = Registry.RandomSource,
                CreateInteractions = melee => new(melee, Session, Directory, new() { SwitchToStandMode = Player.Combat.SwitchToStandMode,
                    CheckJump = () => Player.Controller.MeleeCheckJump(), PutCameraBehind = Player.Combat.PutCameraBehind }) });
            var manipulation = new GodotManipulationConfiguration { Environment = Environment,
                AttachToEnvironmentalList = actor => manipulationList.Insert(0, actor), DetachFromEnvironmentalList = actor => manipulationList.Remove(actor),
                SourceBaddyOrder = SourceBaddies, IgnoreSphereRadius = () => Session.IgnoreSphereRadius,
                LineToItem = (from, to, mode) => {
                    if (mode != 1) throw new NotSupportedException("Original manipulation query uses source mode1.");
                    GodotActorWorldHit? hit = WorldQuery.Trace(from, to);
                    return hit is null ? null : new(Session.ResolveWorldContact(hit), hit.Position, PsxCoordinates.RotateOriginalNormal(hit.Triangle.FixedNormal, hit.Collider is ImportedCollisionBody body ? body.SourceRotation : PsxCoordinates.OriginalRotation(hit.Collider.GlobalBasis)));
                }, HitBaddy = HitBaddy, ShakeCamera = (_, _) => throw Outside("collision camera shake"), ImpactEffects = new OutsideCollisionEffects(this) };
            Registry.RegisterFactory(401, (node, registry) => GodotManipulationActor.Create(node, registry, manipulation));
            var switches = new GodotSwitchConfiguration { Environment = Environment, AttachToControlList = Directory.RegisterControl,
                DetachFromControlList = Directory.Remove, HasPlayer = () => true,
                ReadPlayerCounter = () => throw Outside("reward switch counter read"), WritePlayerCounter = _ => throw Outside("reward switch counter write") };
            Registry.RegisterFactory(407, (node, registry) => GodotSwitchActor.Create(node, registry, switches));
        }
        catch
        {
            NativeServices?.Dispose();
            Viewport.Free();
            Session?.Dispose();
            Directory.Dispose();
            throw;
        }
    }
    public GodotManipulationActor SpawnManipulation(ushort node) => Spawn<GodotManipulationActor>(node);
    public GodotSwitchActor SpawnSwitch(ushort node) => Spawn<GodotSwitchActor>(node);
    private T Spawn<T>(ushort node) where T : GodotScriptActor
    {
        T? captured = null;
        void Capture(GodotScriptActor actor) { if (actor.Definition.NodeId == node) captured = actor as T; }
        Registry.Spawned += Capture;
        try { Registry.Spawn(Registry.Triggers.Nodes[node]); }
        finally { Registry.Spawned -= Capture; }
        return captured ?? throw new InvalidDataException($"Original node{node} did not create expected source actor{typeof(T).Name}.");
    }
    public FixedJointTransform TargetingRotation() => FixedRotation.TransposeRotation(Player.Controller.Orientation.Basis);
    public FixedJointTransform ViewRotation() => Camera.SourceViewRotation;
    public IReadOnlyList<ManipulationTargetBody> SourceBaddies() => Directory.SourceBodies.Select(body =>
        new ManipulationTargetBody(new(body.Identity, body.Position, body.Radius, body.BodyFlags), body.Type)).ToArray();
    private void HitBaddy(object identity, ActorHit hit)
    {
        if (!Directory.Contains(identity)) throw new InvalidDataException("Manipulation damage target is outside this genuine fixture directory.");
        if (identity is GodotThugActor thug) thug.Hit(hit);
        else if (identity is GodotHostageActor hostage) hostage.Hit(hit);
        else throw new InvalidDataException("Original manipulation damage requires a supported genuine baddy actor.");
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        NativeServices.Dispose();
        Viewport.Free();
        Session.Dispose();
        Directory.Dispose();
    }
    private static InvalidOperationException Outside(string operation) => new($"Interaction ownership fixture does not execute projectile collision/Chunk; unexpected activated operation: {operation}.");
    private sealed class OutsideCollisionEffects(PlayerInteractionFixture fixture) : IManipulationImpactHost
    {
        public IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder => fixture.SourceBaddies();
        public FixedVector3 PlayerPosition => fixture.Player.Controller.Position;
        public FixedVector3 CameraPositionWholeUnits => PsxCoordinates.OriginalWorldPosition(fixture.Camera.GlobalPosition) >> 12;
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => fixture.WorldQuery.GroundHeight(position, above, below);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) => throw Outside("Chunk sound");
        public void SendExplosionMessage(object target, FixedVector3 position) => throw Outside("explosion message");
        public void CreateBigExplosion(FixedVector3 position) => throw Outside("explosion visual");
        public void HitPlayer(ActorHit hit) => throw Outside("explosion player damage");
        public void HitBaddy(object target, ActorHit hit) => throw Outside("explosion baddy damage");
        public void ReactSymbioteToExplosion(object target) => throw Outside("symbiote reaction");
        public void TrySpawnDebris(Func<ManipulationDebrisRequest> factory) => throw Outside("debris allocation");
        public void SpawnChunkSmoke(ManipulationSmokeRequest smoke) => throw Outside("chunk smoke");
        public void SendAttachedPulse(ushort node) => throw Outside("Chunk pulse");
    }
}
