using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.SpClone;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L7A4 source327 record through the spclone module factory, the real spidey model and CSpClone data.</summary>
internal static class SpCloneImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat, GodotPlayer player)
    {
        var fixture = new Node3D { Name = "OriginalSpCloneProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0;
        uint clock = 0x100;
        bool fastForward = false;
        var shadows = new List<SourceParticleRegistration>();
        var removedShadows = new List<object>();
        var resources = new GodotSpCloneResources(repository, originalImage);
        var configuration = new GodotSpCloneConfiguration
        {
            ModelFactory = resources.CreateModel, Light = resources.ReadLight, PoseLinks = resources.ReadPoseLinks,
            ShadowTexture = repository.RequireLoaded("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
            LightingState = new SourceVertexLightingState(), Clock = () => clock, FastForwardRequested = () => fastForward,
            IsSourceCulled = _ => false, SourceShadowCreated = shadows.Add, SourceShadowRemoved = removedShadows.Add
        };
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/L7A4_T.trg"));
        TriggerNode[] clones = triggers.Nodes.Where(candidate => candidate.TypeCode is 1 or 7 &&
            TriggerActorDefinition.Read(candidate).ActorType == SpCloneState.Type).ToArray();
        TriggerNode node = clones.MaxBy(clone => TriggerActorDefinition.Read(clone).BehaviorData.Length)!;
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        try
        {
            Assert(clones.Length > 1 && clones.All(clone => TriggerActorDefinition.Read(clone).BehaviorData.Length >= 4) &&
                definition.BehaviorData.Length > 18,
                $"Untouched L7A4 carries {clones.Length} source327 actors, each with an analogue-input stream.");
            (GodotActorRegistry registry, GodotActorCodeModules modules, List<ushort> pulses) = Scene();
            bool rejected = false;
            try { registry.Spawn(node); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                "An unloaded spclone module rejects its factory and unwinds the CBaddy count.");
            modules.LoadCodeModule(SpCloneState.ModuleName);
            registry.Spawn(node);
            var actor = (GodotSpCloneActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            uint bodyFlags = (definition.ClearsBodyFlag2 ? 0x214u : 0x216u) | 8;
            Assert(actor.SourceActorType == SpCloneState.Type && actor.State.BodyFlags == bodyFlags && actor.ItemFlags == 0x482 &&
                actor.Model.ClipId == 0xCA && actor.State.AiState == 1 && actor.State.AiSubstate == 0 &&
                actor.State.PreviousTicks == 2 && actor.State.OlderTicks == 2 && actor.State.Field21E == 100 && actor.State.ShadowScale == 48 &&
                actor.CinematicScript.Cursor == 0 && !actor.CinematicScript.Running && !actor.Model.HasSourceJoints && !actor.State.IsDead,
                "The genuine CSpClone constructor turns its shadow on, runs clip0xCA, sets item flags0x480 and seeds the tick history.");
            Assert(!actor.Hit(new ActorHit { Damage = ushort.MaxValue }) && actor.State.BodyFlags == bodyFlags,
                "The CSpClone slot3 Hit leaf 0x4B0D60 returns0 without mutating the body.");
            actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
            for (int frame = 0; frame < 60; frame++)
            {
                clock += 2; actor.Advance(); actor.SynchronizeSourceLighting();
                if (frame % 20 == 0) { actor.Lighting.VerifyUploads(); await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame); }
            }
            Assert(actor.SourceUpdates == 60 && actor.State.StepScale == 1365 * 6 && actor.State.PreviousTicks == 2 &&
                actor.World.GroundQueries > 0 && actor.Model.HasSourceJoints,
                "Sixty source frames scale physics by three two-tick frames, query the real world and apply the 0x555888 twelve-link pose.");
            Assert(actor.Shadow is { IsAlive: true } shadow && shadows.Count == 1 && shadow.SourceCorners.Count == 4 &&
                shadow.SourceCorners.All(corner => corner.Y == actor.State.GroundY << 12),
                "CSpClone::DoMGSShadow creates one protected footprint on the source landing plane.");
            actor.State.AiState = 2; actor.State.AiSubstate = 0;
            for (int frame = 0; frame < 30 && !actor.State.IsDead; frame++) { clock += 2; actor.Advance(); }
            Assert(actor.CinematicScript.Cursor > 0 && (actor.CinematicScript.Running || actor.State.IsDead),
                "The authored L7A4 analogue-input stream decodes through the clone profile.");
            actor.ReceiveMessage(10, null);
            clock += 2; actor.Advance();
            Assert(actor.Messages.Head is null, "CSpClone::AI cleans up its whole CMessage list without reacting.");
            modules.Modules.Unload(SpCloneState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0 &&
                removedShadows.Count == 1 && modules.Modules.SourceOrder.Count == 0,
                "SpClone_RelocatableModuleClear deletes the active type327 body, its footprint and CBaddy count.");
            fastForward = true;
            modules.LoadCodeModule(SpCloneState.ModuleName);
            registry.Spawn(node);
            var aborted = (GodotSpCloneActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(aborted.State.IsDead && (aborted.ItemFlags & 1) != 0 && aborted.Lifecycle.Phase == ActorLifePhase.Dead &&
                pulses.SequenceEqual(definition.Links),
                "The constructor applies the abort-byte Die(0) and pulses every authored link once.");
            fastForward = false;
            modules.UnloadAllCodeModules();
            registry.Free();
            GD.Print($"Passed {assertions} native spclone checks: L7A4 source327 node{node.Id} of {clones.Length} constructor, physics/pose frames, MGS shadow, authored bytecode and module lifetime.");
        }
        finally
        {
            fixture.Free();
        }

        (GodotActorRegistry, GodotActorCodeModules, List<ushort>) Scene()
        {
            var pulses = new List<ushort>();
            var registry = new GodotActorRegistry
            {
                Name = "SpCloneRegistry", Triggers = triggers, Environment = new(LevelId: 0x704),
                ReceivePulse = pulses.Add, Player = player
            };
            fixture.AddChild(registry); registry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(registry, thugs, blackCat, spClone: configuration);
            modules.RegisterFactories();
            return (registry, modules, pulses);
        }
        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native spclone: {evidence}"); assertions++; }
    }
}
