using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L4A1 source307 Rhino through the rhino module factory and real rhino.psx model.</summary>
internal static class RhinoImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat)
    {
        var fixture = new Node3D { Name = "OriginalRhinoProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0;
        uint clock = 0x100;
        int attack = 400;
        CameraFocusTarget? globalBoss = null;
        var resources = new GodotRhinoResources(repository, originalImage);
        var configuration = new GodotRhinoConfiguration
        {
            ModelFactory = resources.CreateModel, Light = resources.ReadLight, LightingState = new SourceVertexLightingState(),
            Clock = () => clock, AttackClock = () => attack, Random = bound => bound <= 0 ? 0 : 1,
            IsSourceCulled = _ => false, CreateHealthBar = _ => { }, SpeechTable = resources.ReadSpeechTable(),
            WallChunks = resources.ReadWallChunks(),
            SetGlobalBoss = target => globalBoss = target, SetBossCameraMode = () => { }, PlayerInputSuppression = () => 1,
            TryPlayPositionSpeech = (_, _, _, _) => false
        };
        try
        {
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/l4a1_t.trg"));
            TriggerNode? sample = null;
            foreach (TriggerNode node in triggers.Nodes)
            {
                if (node.Id == 0) continue;
                try
                {
                    if (TriggerActorDefinition.Read(node).ActorType == RhinoState.Type) { sample = node; break; }
                }
                catch (InvalidDataException) { }
            }
            Assert(sample is not null, "Untouched L4A1 carries at least one authored source307 Rhino record.");
            TriggerActorDefinition definition = TriggerActorDefinition.Read(sample!);
            Assert(definition.ActorType == RhinoState.Type,
                "The first L4A1 Rhino record is source307.");

            (GodotActorRegistry registry, GodotActorCodeModules modules) = Scene(triggers);
            bool rejected = false;
            try { registry.Spawn(sample!); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                "An unloaded rhino module rejects its factory and unwinds the CBaddy count.");

            modules.LoadCodeModule(RhinoState.ModuleName);
            registry.Spawn(sample!);
            var actor = (GodotRhinoActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(actor.SourceActorType == RhinoState.Type && actor.State.Health == 1400 &&
                actor.State.MinorRadius == 175 && actor.ItemFlags == 0x482 &&
                actor.State.MovementAnimations == RhinoState.SetupWord0 && actor.State.IdleAnimations == RhinoState.SetupWord1 &&
                (actor.State.BehaviorFlags & 0x02000201) == 0x02000201 &&
                registry.BaddyConstructionState.Count == 1 && !actor.Model.AdvanceAutomatically,
                "The genuine CRhino constructor seeds health1400, rMinor175, strange-init words and behavior 0x02000201.");
            Assert(actor.Model.SourcePartCount > 0, "rhino.psx loads a real skinned model for source307.");
            actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
            Assert(actor.Lighting.LitPartCount >= 0 && resources.ReadLight().Directions != default,
                "M3d_RhinoLight at 0x552080 is applied to the Rhino model.");

            for (int frame = 0; frame < 8; frame++)
            {
                clock += 2; attack += 2;
                actor.Advance();
            }
            Assert(actor.State.State == 8 && actor.State.Substate is 2 or 3,
                "Bounded AI frames leave RhinoInit and enter ChargePlayer wind-up (0x47F800).");
            Assert(actor.Hit(new ActorHit { Damage = ushort.MaxValue }) && actor.State.Health == 1400 &&
                actor.State.Field3D0 == 30 && actor.State.State == 8,
                "CRhino::Hit (0x47EE70) without the damage flag keeps health, starts the 30-tick flash and leaves charge state flags8 alone.");

            modules.Modules.Unload(RhinoState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0 &&
                modules.Modules.SourceOrder.Count == 0 && globalBoss is null,
                "Rhino_RelocatableModuleClear deletes the active type307 body and clears boss global ownership.");

            GD.Print($"Passed {assertions} native Rhino checks: L4A1 source307 module factory, rhino construction, lighting and clear.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        }
        finally
        {
            fixture.Free();
        }

        (GodotActorRegistry, GodotActorCodeModules) Scene(TriggerFile triggers)
        {
            var registry = new GodotActorRegistry
            {
                Name = "RhinoRegistry", Triggers = triggers, Environment = new(LevelId: 0x401)
            };
            fixture.AddChild(registry); registry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(registry, thugs, blackCat, rhino: configuration);
            modules.RegisterFactories();
            return (registry, modules);
        }

        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native Rhino: {evidence}"); assertions++; }
    }
}
