using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class ThugNativeServicesImportChecks
{
    // This scenario exercises initial patrol and one ordinary melee contact.
    // Requests outside that scenario fail, so missing interactions cannot be silently passed.
    internal sealed class Interactions : IGodotThugInteractionHost
    {
        public int DomeCount => 0;
        public bool IgnoreSphereRadius => false;
        public int PlayerWebComboValue => 0;
        private static NotSupportedException Outside(string operation) => new($"Native patrol/melee check unexpectedly requested {operation}.");
        public void SpawnGrenade(GodotThugActor actor, ThugGrenadeRequest request) => throw Outside("grenade throw");
        public void NotifyKill(int actorType) => throw Outside("kill quip");
        public bool GrabUpdate(GodotThugActor actor, ref FixedVector3 position, ref short yaw) => throw Outside("player grab");
        public IThugTrapWeb? RecoverWeb(GodotThugActor actor, bool tugWeb) => throw Outside("web handle");
        public void SetPlayerWebCombo(int duration, int amount) => throw Outside("web combo");
        public void ApplyHitToOtherBody(IThugInjuryBody body, ActorHit hit) => throw Outside("other actor damage");
        public ActorCollisionBody DescribeEnvironmentalContact(GodotActorWorldHit hit) => throw Outside("environmental body metadata");
        public ActorProcedureState? RecoverOtherActor(object handle) => throw Outside("other actor handle");
        public ActorBodyMotion? RecoverOtherBody(ActorProcedureState actor) => throw Outside("other body handle");
        public object? RecoverNativeActor(ActorProcedureState actor) => throw Outside("other native actor handle");
        public void SendMessageToOtherActor(GodotThugActor sender, object targetHandle, int code) => throw Outside("other actor message");
        public void SelectHostageCamera(FixedVector3 actorPosition, FixedVector3 hostagePosition) => throw Outside("hostage camera");
        public void SendSignal(GodotThugActor actor) => throw Outside("actor signal");
        public bool ExecuteScriptCommand(GodotThugActor actor, ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
            throw Outside($"script opcode{opcode:X4} at node{actor.Definition.NodeId}");
    }

    public static async Task VerifyAsync(Node3D owner, GodotActorRegistry registry, PkrArchive archive,
        PsxLibraryRepository libraries, Func<ImportedAnimatedModel> playerModelFactory,
        IReadOnlyList<ActorPartBox> boxes, FixedVector3 position, short yaw, Func<uint> clock,
        ReadOnlyMemory<byte> originalExecutable)
    {
        var random = new GameRandom(0x12A6CC58);
        var player = GodotPlayer.Create(position, yaw, playerModelFactory(), new(true, new(false, 10, true)), random: random.Next);
        player.Combat.ConfigureCollisionBoxes(boxes);
        player.SampleInput = () => default;
        player.SampleButtons = () => default;
        owner.AddChild(player);
        player.SetPhysicsProcess(false);
        Camera3D? previous = owner.GetViewport().GetCamera3D();
        var camera = new PlayerFollowCamera { Name = "ThugCheckPlayerCamera", Target = player };
        owner.AddChild(camera);
        var sounds = owner.GetNode<GodotSoundChannels>("SoundChannels");
        var speech = owner.GetNode<GodotSpeechPlayer>("Speech");
        var flash = new GodotScreenFlash { Name = "ThugCheckScreenFlash" };
        owner.AddChild(flash);
        var visuals = GodotThugVisualEffects.Create(archive, libraries, sounds.Controller,
            () => new(camera.SourcePosition >> 12), random.Next, clock);
        owner.AddChild(visuals);
        var sourceActors = new List<GodotThugActor>();
        var collisionDirectory = new GodotPlayerMeleeDirectory();
        using var actorDirectory = new GodotActorSessionDirectory(registry, collisionDirectory, new ActorSessionCheckPlatforms());
        var services = new GodotThugNativeServices(visuals, sounds.Controller, () => new(camera.SourcePosition >> 12),
            speech, player, () => GodotActorCollisionDirectory.SourceBaddyBodies(collisionDirectory), new Interactions(), actorDirectory);
        var scheduler = new ThugCheckBitScheduler { Visuals = visuals, Clock = clock };
        owner.AddChild(scheduler);
        try
        {
            player.Combat.SourceTimer = clock;
            player.Combat.CameraFlag = () => camera.TerrainObstructed;
            player.Combat.Sound = (sound, volume, _) => sounds.Controller.Play(sound, unchecked((short)volume));
            player.Combat.PositionSound = (sound, point, _) => sounds.Controller.PlayAt(sound, point, new(camera.SourcePosition >> 12));
            player.Combat.SoundStopped = handle => sounds.Controller.Stop(unchecked((uint)handle));
            player.Combat.XaStopped = () => { if (player.Combat.SpeechAttachment.CanStop(clock(), speech.Controller.Current)) speech.Controller.Stop(); };
            player.Combat.CameraBehind = camera.PutBehind;
            player.Combat.CameraField12C = camera.SetFocusHook;
            player.Combat.FlashReset = flash.Reset;
            bool failModelFactory = false;
            var expectedFailure = new InvalidOperationException("Deliberate original thug model constructor unwind.");
            var configuration = new GodotThugConfiguration
            {
                ModelFactory = name => failModelFactory ? throw expectedFailure : visuals.Resources.CreateModel(name),
                Player = player, Combat = new(),
                SourceBaddyOrder = () => sourceActors.AsEnumerable().Reverse().ToArray(), Effects = services,
                SpitParticles = visuals.SpitParticles, SpriteRings = visuals.SpriteRings,
                AttackClock = () => unchecked((int)clock()), Timer = clock, Random = random.Next,
                SourceConstructionOrdinal = registry.BaddyConstruction.RequireCurrentOrdinal,
                IsSourceCulled = _ => false // The isolated actor scenario keeps the source culling bit clear.
            };
            int beforeCount = registry.BaddyConstruction.State.Count;
            int beforeLeases = registry.BaddyConstruction.OwnedCount;
            double beforeOrphans = Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            failModelFactory = true;
            try
            {
                registry.BaddyConstruction.Create(312, () => GodotThugActor.Create(registry.Triggers.Nodes[9], registry, configuration));
                throw new InvalidDataException("Original thug construction ignored the deliberate source model failure.");
            }
            catch (InvalidOperationException exception) when (ReferenceEquals(exception, expectedFailure)) { }
            finally { failModelFactory = false; }
            if (registry.BaddyConstruction.State.Count != beforeCount || registry.BaddyConstruction.OwnedCount != beforeLeases ||
                (OS.IsDebugBuild() && Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount) != beforeOrphans))
                throw new InvalidDataException("Failed actual thug construction retained a source count lease or orphan native actor.");
            await ThugActorImportChecks.VerifyAsync(owner, registry, configuration, boxes, actor =>
            {
                sourceActors.Add(actor);
                if (!collisionDirectory.Contains(actor)) collisionDirectory.RegisterBody(actor);
                ThugAppearanceImportChecks.Verify(actor, visuals.AppearanceFor(actor));
            });
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            var cinematicResources = new GodotBlackCatResources(libraries, originalExecutable);
            var cinematicBits = new SourceParticleScheduler();
            uint cinematicClock = clock();
            var blackCat = new GodotBlackCatConfiguration
            {
                ModelFactory = cinematicResources.CreateModel, PoseData = cinematicResources.ReadPoseData,
                LightingState = new(), Clock = () => cinematicClock,
                ShadowTexture = libraries.Load("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
                FastForwardRequested = () => registry.Environment.FastForwardDeath,
                // The bounded pose proof samples every actor without renderer culling.
                IsSourceCulled = _ => false,
                SourceShadowCreated = bit => cinematicBits.Attach(SourceParticleList.Quad, bit),
                SourceShadowRemoved = identity => cinematicBits.Detach(identity)
            };
            await BlackCatActorImportChecks.VerifyAsync(owner, registry, blackCat, configuration,
                () => cinematicClock = unchecked(cinematicClock + 2));
            if (cinematicBits.Count != 0) throw new InvalidDataException("Actual Black Cat destruction retained protected source Quad owners.");
        }
        finally
        {
            scheduler.Free();
            visuals.Free();
            flash.Free();
            camera.Free();
            player.Free();
            previous?.MakeCurrent();
        }
    }
}

internal partial class ThugCheckBitScheduler : Node
{
    private int ticks;
    private readonly SourceParticleScheduler particles = new();
    public GodotThugVisualEffects Visuals { get; init; } = null!;
    public Func<uint> Clock { get; init; } = null!;
    public override void _Ready()
    {
        ProcessPhysicsPriority = 20;
        Visuals.SourceBitCreated += (family, bit) => particles.Attach(family, bit);
        Visuals.SourceBitRemoved += identity => particles.Detach(identity);
    }
    public override void _PhysicsProcess(double delta)
    {
        if (++ticks % 2 != 0) return;
        Visuals.AdvanceBulletModels();
        particles.Move(Clock());
        particles.RemoveDead();
    }
}
