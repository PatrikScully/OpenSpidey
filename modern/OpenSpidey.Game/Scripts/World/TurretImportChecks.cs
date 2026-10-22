using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Turret;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>L8A2 (0x802) source325 turret through the turret module factory only.</summary>
internal static class TurretImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat)
    {
        var fixture = new Node3D { Name = "OriginalTurretProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0;
        uint clock = 0x100;
        var resources = new GodotTurretResources(repository, originalImage);
        var configuration = new GodotTurretConfiguration
        {
            ModelFactory = resources.CreateModel, Light = resources.ReadLight, LightingState = new SourceVertexLightingState(),
            Clock = () => clock, IsSourceCulled = _ => false
        };
        try
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/l8a2_t.trg"));
            TriggerNode? sample = null;
            foreach (TriggerNode node in triggers.Nodes)
            {
                if (node.Id == 0) continue;
                try
                {
                    if (TriggerActorDefinition.Read(node).ActorType == TurretState.Type) { sample = node; break; }
                }
                catch (InvalidDataException) { }
            }
            Assert(sample is not null, "Untouched L8A2 carries at least one authored source325 turret record.");
            (GodotActorRegistry registry, GodotActorCodeModules modules) = Scene(triggers);
            bool rejected = false;
            try { registry.Spawn(sample!); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && registry.LiveCount == 0,
                "An unloaded turret module rejects its factory and unwinds the CBaddy count.");
            modules.LoadCodeModule(TurretState.ModuleName);
            registry.Spawn(sample!);
            var actor = (GodotTurretActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(actor.SourceActorType == TurretState.Type && actor.State.Health == 10 && actor.MinorRadius == 0x80 &&
                (actor.State.CombatFlags & 0x10200) != 0 && actor.Model.SourcePartCount > 0,
                "The genuine CTurret constructor on 0x802 seeds health, radius, combat flags and loads turret.psx.");
            modules.Modules.Unload(TurretState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0,
                "Turret_RelocatableModuleClear deletes the active type325 body.");
            registry.Free();
            GD.Print($"Passed {assertions} native turret checks: L8A2 module factory, construction and clear.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally { fixture.Free(); }

        (GodotActorRegistry, GodotActorCodeModules) Scene(TriggerFile triggers)
        {
            var registry = new GodotActorRegistry
            {
                Name = "TurretRegistry802", Triggers = triggers, Environment = new(LevelId: 0x802)
            };
            fixture.AddChild(registry); registry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(registry, thugs, blackCat, turret: configuration);
            modules.RegisterFactories();
            return (registry, modules);
        }

        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native turret: {evidence}"); assertions++; }
    }
}
