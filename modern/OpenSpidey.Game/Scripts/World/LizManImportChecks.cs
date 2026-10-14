using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Genuine sewer source317 LizMan through the lizman module factory and real lizman/lizman2 models.</summary>
internal static class LizManImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat)
    {
        var fixture = new Node3D { Name = "OriginalLizManProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0;
        uint clock = 0x100;
        int attack = 40, total = 1000;
        var resources = new GodotLizManResources(repository, originalImage);
        var configuration = new GodotLizManConfiguration
        {
            ModelFactory = resources.CreateModel, Light = resources.ReadLight, LightingState = new SourceVertexLightingState(),
            Clock = () => clock, AttackClock = () => attack, TotalTime = () => total, Random = bound => bound <= 0 ? 0 : 1,
            IsSourceCulled = _ => false
        };
        try
        {
            // L5A3 (level 0x503) is the first sewer combat room that loads module "lizman" and type317.
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/l5a3_t.trg"));
            TriggerNode? sample = null;
            foreach (TriggerNode node in triggers.Nodes)
            {
                if (node.Id == 0) continue;
                try
                {
                    if (TriggerActorDefinition.Read(node).ActorType == LizManState.Type) { sample = node; break; }
                }
                catch (InvalidDataException) { }
            }
            Assert(sample is not null, "Untouched L5A3 carries at least one authored source317 LizMan record.");
            TriggerActorDefinition definition = TriggerActorDefinition.Read(sample!);
            Assert(definition.ActorType == LizManState.Type && definition.ClearsBodyFlag2 && definition.InitializeOnLoad,
                "The first L5A3 LizMan is an init-on-load marker4 source317 body.");

            (GodotActorRegistry registry, GodotActorCodeModules modules) = Scene(0x503, triggers);
            bool rejected = false;
            try { registry.Spawn(sample!); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                "An unloaded lizman module rejects its factory and unwinds the CBaddy count.");

            modules.LoadCodeModule(LizManState.ModuleName);
            registry.Spawn(sample!);
            var actor = (GodotLizManActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(actor.SourceActorType == LizManState.Type && actor.State.ModelName == "lizman2" &&
                actor.State.BodyFlags == 0x214 && actor.ItemFlags == 0x482 && actor.State.Field338 == 0x1000 &&
                actor.State.Field3AC == 0x21 && actor.State.Field398 == 0x3C000 && actor.State.Field212 == 0x1E &&
                actor.State.Field374 == unchecked(total - 0x131) && actor.MinorRadius == 0x80 &&
                registry.BaddyConstructionState.Count == 1 && !actor.Model.AdvanceAutomatically,
                "The genuine CLizMan constructor on 0x503 selects lizman2, seeds field_338/3AC/398/374 and item flags 0x480.");
            Assert(actor.Model.SourcePartCount > 0, "lizman2.psx loads a real skinned model for source317.");
            actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
            Assert(actor.Lighting.LitPartCount >= 0 && resources.ReadLight().Directions != default,
                "M3d_DefaultLight at 0x54FFE8 is applied to the LizMan model.");

            bool hitRejected = false;
            try { actor.Hit(new ActorHit { Damage = ushort.MaxValue }); }
            catch (NotSupportedException ex) when (ex.Message.Contains("0x44BA00", StringComparison.Ordinal)) { hitRejected = true; }
            Assert(hitRejected, "Unported CLizMan::Hit keeps an explicit NotSupportedException naming 0x44BA00.");

            for (int frame = 0; frame < 8; frame++)
            {
                clock += 2; attack += 2; actor.Advance(); actor.SynchronizeSourceLighting();
            }
            Assert(actor.SourceUpdates == 8 && !actor.State.IsDead && actor.State.State is 0 or 1 or 2,
                "Bounded AI frames advance construction/init without dying; Jump remains gated by NotSupportedException.");

            modules.Modules.Unload(LizManState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0 &&
                modules.Modules.SourceOrder.Count == 0,
                "LizMan_RelocatableModuleClear deletes the active type317 body and its CBaddy count.");

            (registry, modules) = Scene(0x505, triggers);
            modules.LoadCodeModule(LizManState.ModuleName);
            registry.Spawn(sample!);
            var sewerBoss = (GodotLizManActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(sewerBoss.State.ModelName == "lizman" && sewerBoss.State.Field398 == 0x32000 && sewerBoss.State.Field212 == 0xF &&
                (sewerBoss.State.BehaviorFlags & 0x8000) != 0 && (sewerBoss.State.CombatFlags & 0x2000000) != 0,
                "Level 0x505 applies the alternate CLizMan constructor branch (lizman model, field_398=0x32000).");
            modules.UnloadAllCodeModules();
            registry.Free();

            GD.Print($"Passed {assertions} native LizMan checks: L5A3 source317 module factory, lizman2 construction, lighting and clear.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally
        {
            fixture.Free();
        }

        (GodotActorRegistry, GodotActorCodeModules) Scene(int levelId, TriggerFile triggers)
        {
            var registry = new GodotActorRegistry
            {
                Name = $"LizManRegistry{levelId:X}", Triggers = triggers, Environment = new(LevelId: levelId)
            };
            fixture.AddChild(registry); registry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(registry, thugs, blackCat, lizMan: configuration);
            modules.RegisterFactories();
            return (registry, modules);
        }

        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native LizMan: {evidence}"); assertions++; }
    }
}
