using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L8A1 source324/412 through the simby module factories and real symbiote models.</summary>
internal static class SimbyImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat, GodotPlayer player)
    {
        var fixture = new Node3D { Name = "OriginalSimbyProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0, wibblers = 0;
        uint clock = 0x100;
        int attackClock = 120;
        var boss = GodotBossGlobalOwner.Create();
        fixture.AddChild(boss);
        var world = new SimbyWorldState();
        var resources = new GodotSimbyResources(repository, originalImage);
        var vertexWibbler = new GodotSimbyVertexWibbler(repository);
        fixture.AddChild(vertexWibbler);
        var droplets = GodotSimbyDroplets.Create(archive.Read("data/symbits.psx"));
        fixture.AddChild(droplets);
        var particles = new SourceParticleScheduler();
        droplets.SourceBitCreated += bit => particles.Attach(SourceParticleList.Flat, bit);
        int fixtureGroundY = 0;
        var configuration = new GodotSimbyConfiguration
        {
            ModelFactory = resources.CreateSimbyModel, PunchObModelFactory = resources.CreatePunchObModel,
            PosePacket = resources.ReadPosePacket, LightingState = new SourceVertexLightingState(),
            Clock = () => clock, AttackClock = () => attackClock, Random = bound => bound == 0 ? 0 : bound / 3,
            World = world, BossGlobal = boss, IsSourceCulled = _ => false, IsPunchObCulled = _ => false,
            EnsureVertexWibbler = () => { vertexWibbler.Ensure(0x801, bound => bound == 0 ? 0 : bound / 3); wibblers++; },
            Droplets = droplets, SymBaseMesh = resources.CreateSymBaseMesh, GroundY = _ => fixtureGroundY, IgnoreSphereRadius = () => false
        };
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/l8a1_t.trg"));
        try
        {
            TriggerNode? simbyNode = triggers.Nodes.FirstOrDefault(node =>
                node.TypeCode is 1 or 7 && TriggerActorDefinition.Read(node).ActorType == SimbyState.Type);
            TriggerNode? punchNode = triggers.Nodes.FirstOrDefault(node =>
                node.TypeCode is 1 or 7 && TriggerActorDefinition.Read(node).ActorType == PunchObState.Type);
            Assert(simbyNode is not null && punchNode is not null,
                "Untouched L8A1 keeps init-on-load source324 and source412 records for the simby module.");
            (GodotActorRegistry registry, GodotActorCodeModules modules) = Scene(0x801);
            bool rejected = false;
            try { registry.Spawn(simbyNode!); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                "An unloaded simby module rejects its factory and unwinds the CBaddy count.");
            modules.LoadCodeModule(SimbyState.ModuleName);
            registry.Spawn(simbyNode!);
            var simby = (GodotSimbyActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(simby.SourceActorType == SimbyState.Type && simby.State.Health == 800 && simby.State.MinorRadius == 140 &&
                simby.State.SurfaceClearance == 100 && (simby.State.CombatFlags & 0x201) == 0x201 &&
                registry.BaddyConstructionState.Count == 1 && wibblers == 1 && world.Count == 1 &&
                vertexWibbler.Created && vertexWibbler.Count == 2 &&
                !simby.Model.AdvanceAutomatically && simby.Model.SourcePartCount > 0,
                "The genuine CSimby constructor seeds health/radius, combat flags, pose joints and the one-time vertex wibbler.");
            simby.Advance();
            Assert(simby.SourceUpdates == 1 &&
                ((simby.State.Stage == 0 && simby.State.Substate == 1) || (simby.State.Stage == 8 && simby.State.Substate == 0)),
                "CSimby::AI Initialize either seats the body (substate1) or enters DropDown when the fixture has no ground.");
            registry.Spawn(punchNode!);
            var punch = registry.SourceActors(ActorUpdateGroup.Enemy).OfType<GodotPunchObActor>().Single();
            Assert(punch.SourceActorType == PunchObState.Type && punch.Model.ClipId == 0 && registry.BaddyConstructionState.Count == 2,
                "Module slot4 builds CPunchOb with clip0 and a second CBaddy lease.");
            clock += 2; punch.Advance();
            Assert(punch.SourceUpdates == 1 && punch.SourceScale == 4096,
                "CPunchOb::AI advances one frame without entering the trap-web scale stage.");
            TriggerFile l7Triggers = TriggerFile.Read(archive.Read("data/l7a2_t.trg"));
            TriggerNode? dropletNode = l7Triggers.Nodes.FirstOrDefault(node =>
                node.TypeCode is 1 or 7 && TriggerActorDefinition.Read(node).ActorType == SimbyState.DropletTriggerType);
            Assert(dropletNode is not null, "L7A2 keeps at least one authored type411 CSimbyDroplet factory record.");
            var dropletRegistry = new GodotActorRegistry
            {
                Name = "SimbyDropletRegistry702", Triggers = l7Triggers, Environment = new(LevelId: 0x702),
                ReceivePulse = _ => { }, Player = player
            };
            fixture.AddChild(dropletRegistry);
            dropletRegistry.SetPhysicsProcess(false);
            var dropletModules = new GodotActorCodeModules(dropletRegistry, thugs, blackCat, simby: configuration);
            dropletModules.RegisterFactories();
            dropletModules.LoadCodeModule(SimbyState.ModuleName);
            var droplet = (GodotSimbyDropletActor)dropletModules.Modules.CallUserFunction(SimbyState.ModuleName, 1, new(dropletNode!, dropletRegistry));
            dropletRegistry.Adopt(droplet);
            Assert(droplets.Count == 1 && particles.Count == 1 && droplet.SourceActorType == SimbyState.DropletTriggerType,
                "Module slot1 builds CSimbyDroplet on FlatBitList with gAnimTable24 frame1.");
            for (int tick = 0; tick < 120; tick++)
            {
                particles.Move(clock);
                dropletRegistry.SourceList(ActorUpdateGroup.Background).Update();
                clock += 2;
            }
            particles.RemoveDead();
            Assert(!droplet.State.Alive && droplets.Count == 0 && dropletRegistry.LiveCount == 0,
                "CSimbyDroplet Move for 120 ticks reaches ground or age60, pulses links and dies.");
            dropletModules.UnloadAllCodeModules();
            dropletRegistry.Free();
            modules.Modules.Unload(SimbyState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(simby) && GodotObject.IsInstanceValid(punch) && registry.BaddyConstructionState.Count == 1 &&
                world.Count == 0 && modules.Modules.SourceOrder.Count == 0,
                "Simby_RelocatableModuleClear deletes active type324 only and clears the shared count.");
            modules.UnloadAllCodeModules();
            registry.Free();
            VerifyBits();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            GD.Print($"Passed {assertions} native simby checks: L8A1 source324/412 construction, CSimby Initialize frame, PunchOb AI frame, L7A2 CSimbyDroplet flat bit, module clear and SplattyDeath Quad/BulletList bits.");
        }
        finally
        {
            fixture.Free();
        }

        void VerifyBits()
        {
            var simbyBits = GodotSimbyBits.Create(archive.Read("data/bits.psx"), archive.Read("data/symbits.psx"));
            fixture.AddChild(simbyBits);
            var bullets = new SourceActorList();
            var bitWorld = new GodotActorWorldQuery(fixture, triggers);
            var quads = new SourceParticleScheduler();
            int glows = 0;
            bool playerPresent = false;
            simbyBits.Services = new()
            {
                Player = () => playerPresent ? player : null, Random = bound => bound == 0 ? 0 : bound / 3,
                CameraPosition = () => (player.Controller.Position >> 12) + new FixedVector3(0, -300, -600),
                GroundWorld = () => bitWorld, IgnoreSphereRadius = () => false, PlaySound = (_, _) => { },
                CreateGlowFlash = parameters => { glows++; return new GlowFlashState(parameters, bound => bound == 0 ? 0 : bound / 3); },
                Bullets = () => bullets, BlobMesh = () => GodotSimbyBits.BuildBlobMesh(repository)
            };
            simbyBits.SourceBitCreated += bit => quads.Attach(SourceParticleList.Quad, bit);
            simbyBits.SpawnShot(player.Controller.Position + new FixedVector3(0, -100 << 12, 0), bitWorld);
            quads.RemoveDead();
            Assert(simbyBits.Count == 0 && quads.Count == 0,
                "CSimbyShot without dword_6A9038 dies in its constructor and leaves the Quad list unmoved.");
            FixedVector3 above = player.Controller.Position + new FixedVector3(0, -400 << 12, 0);
            simbyBits.SplattyDeath(above, part => above + new FixedVector3(part << 12, 0, 0));
            Assert(glows == 1 && bullets.Count == 13 && simbyBits.BlobCount == 13 && simbyBits.Count == 30 && quads.Count == 30,
                "Simby_SplattyDeath emits one glow, thirteen symblob BulletList bodies and thirty CSimbyDrop Quad bits.");
            for (int tick = 0; tick < 160; tick++)
            {
                quads.Move(clock);
                quads.RemoveDead();
                bullets.Update();
                simbyBits.Rebuild();
                clock += 2;
            }
            Assert(simbyBits.BlobCount == 0 && bullets.Count == 0 && simbyBits.Count == 0 && quads.Count == 0,
                "Blobs die after twenty-one AI frames; drops land or age out into splats that fade to black on the real L8A1 ground.");
            simbyBits.Free();
        }

        (GodotActorRegistry, GodotActorCodeModules) Scene(int levelId)
        {
            var registry = new GodotActorRegistry
            {
                Name = $"SimbyRegistry{levelId:X}", Triggers = triggers, Environment = new(LevelId: levelId),
                ReceivePulse = _ => { }, Player = player
            };
            fixture.AddChild(registry); registry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(registry, thugs, blackCat, simby: configuration);
            modules.RegisterFactories();
            return (registry, modules);
        }

        void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            assertions++;
        }
    }
}
