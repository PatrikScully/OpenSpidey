using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;
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
        var configuration = new GodotSimbyConfiguration
        {
            ModelFactory = resources.CreateSimbyModel, PunchObModelFactory = resources.CreatePunchObModel,
            PosePacket = resources.ReadPosePacket, LightingState = new SourceVertexLightingState(),
            Clock = () => clock, AttackClock = () => attackClock, Random = bound => bound == 0 ? 0 : bound / 3,
            World = world, BossGlobal = boss, IsSourceCulled = _ => false, IsPunchObCulled = _ => false,
            EnsureVertexWibbler = () => { vertexWibbler.Ensure(0x801, bound => bound == 0 ? 0 : bound / 3); wibblers++; }
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
            bool dropletMissing = false;
            try { modules.Modules.CallUserFunction(SimbyState.ModuleName, 1, new(simbyNode!, registry)); }
            catch (NotSupportedException) { dropletMissing = true; }
            Assert(dropletMissing, "Slot1 CSimbyDroplet stays NotSupported until FlatBit adapter exists.");
            modules.Modules.Unload(SimbyState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(simby) && GodotObject.IsInstanceValid(punch) && registry.BaddyConstructionState.Count == 1 &&
                world.Count == 0 && modules.Modules.SourceOrder.Count == 0,
                "Simby_RelocatableModuleClear deletes active type324 only and clears the shared count.");
            modules.UnloadAllCodeModules();
            registry.Free();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            GD.Print($"Passed {assertions} native simby checks: L8A1 source324/412 construction, CSimby Initialize frame, PunchOb AI frame, droplet gap and module clear.");
        }
        finally
        {
            fixture.Free();
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
