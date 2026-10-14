using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Torch;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Rendering;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Genuine L1A2a source328 record through the torch module factory, real torch.psx model and CTorch data.</summary>
internal static class TorchImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository, ReadOnlyMemory<byte> originalImage,
        GodotThugConfiguration thugs, GodotBlackCatConfiguration blackCat, GodotPlayer player)
    {
        var fixture = new Node3D { Name = "OriginalTorchProof", TopLevel = true };
        owner.AddChild(fixture);
        int assertions = 0, wibbles = 0;
        uint clock = 0x100;
        bool fastForward = false;
        var shadows = new List<SourceParticleRegistration>();
        var removedShadows = new List<object>();
        var resources = new GodotTorchResources(repository, originalImage);
        var configuration = new GodotTorchConfiguration
        {
            ModelFactory = resources.CreateModel, SourceData = resources.ReadSourceData,
            ShadowTexture = repository.RequireLoaded("shell_dc").Textures.Single(texture => texture.Checksum == 0x9F6CA828),
            LightingState = new SourceVertexLightingState(), Clock = () => clock, FastForwardRequested = () => fastForward,
            AdvanceTextureWibble = _ => wibbles++, IsSourceCulled = _ => false,
            SourceShadowCreated = shadows.Add, SourceShadowRemoved = removedShadows.Add
        };
        TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A2a_T.trg"));
        TriggerNode node = triggers.Nodes[135];
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        try
        {
            Assert(definition.ActorType == TorchState.Type && definition.ClearsBodyFlag2 && definition.InitializeOnLoad &&
                !definition.StartsSuspended && definition.Links.Count == 0 && definition.BehaviorData.Length > 18,
                "Untouched L1A2a node135 is the authored init-on-load marker4 source328 actor with its cinematic stream.");
            (GodotActorRegistry registry, GodotActorCodeModules modules, List<ushort> pulses) = Scene(0x102);
            bool rejected = false;
            try { registry.Spawn(node); }
            catch (InvalidOperationException) { rejected = true; }
            Assert(rejected && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0,
                "An unloaded torch module rejects its factory and unwinds the CBaddy count.");
            modules.LoadCodeModule(TorchState.ModuleName);
            registry.Spawn(node);
            var actor = (GodotTorchActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(actor.SourceActorType == TorchState.Type && actor.State.BodyFlags == 0x214 && actor.ItemFlags == 0x482 &&
                actor.Model.ClipId == 4 && !actor.Model.AdvanceAutomatically && actor.State.Stage == 1 && actor.State.Substate == 0 &&
                actor.State.PreviousTicks == 2 && actor.State.OlderTicks == 2 && actor.State.SurfaceClearance == 100 &&
                actor.MinorRadius == 0 && actor.CinematicScript.Cursor == 0 && !actor.CinematicScript.Running &&
                registry.BaddyConstructionState.Count == 1 && !actor.Model.HasSourceJoints && !actor.State.IsDead,
                "The genuine CTorch constructor runs clip4, sets item flags 0x480, seeds the tick history and keeps the cinematic stream idle.");
            Assert(!actor.Hit(new ActorHit { Damage = ushort.MaxValue }) && actor.State.BodyFlags == 0x214,
                "The CTorch slot3 Hit leaf returns0 without mutating the body.");
            actor.SynchronizeSourceLighting(); actor.Lighting.VerifyUploads();
            Assert(actor.Lighting.LitPartCount == 0 && actor.Model.SourcePartCount == 28 && actor.SourceData.PoseLinks.Links.Count == 16 &&
                actor.SourceData.Light.Directions != default,
                "torch.psx carries face-only normals, so the source vertex-light rule keeps all 28 baked parts while M3d_TorchLight and the 0x557F20 sixteen-link packet are read.");
            for (int frame = 0; frame < 60; frame++)
            {
                clock += 2; actor.Advance(); actor.SynchronizeSourceLighting();
                if (frame % 20 == 0) { actor.Lighting.VerifyUploads(); await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame); }
            }
            bool landed = actor.State.Stage != 1 || actor.State.Substate != 0;
            Assert(actor.SourceUpdates == 60 && wibbles == 60 && actor.State.StepScale == 1365 * 6 && actor.State.PreviousTicks == 2 &&
                actor.World.GroundQueries > 0 && actor.Model.HasSourceJoints && (landed || actor.State.GravityAcceleration == 0),
                "Sixty source frames scale physics by three two-tick frames, call the model wibble once each and query the real world below.");
            Assert(actor.Shadow is { IsAlive: true } shadow && shadows.Count == 1 && shadow.SourceCorners.Count == 4 &&
                shadow.SourceCorners.All(corner => corner.Y == actor.State.GroundY << 12),
                "CTorch::DoMGSShadow creates one protected footprint whose corners sit at the source ground register.");
            actor.ReceiveMessage(10, null);
            clock += 2; actor.Advance();
            Assert(actor.Messages.Head is null && !actor.State.IsDead, "CTorch::AI deletes its whole CMessage list without reacting.");
            modules.Modules.Unload(TorchState.ModuleName);
            Assert(!GodotObject.IsInstanceValid(actor) && registry.LiveCount == 0 && registry.BaddyConstructionState.Count == 0 &&
                removedShadows.Count == 1 && modules.Modules.SourceOrder.Count == 0,
                "Torch_RelocatableModuleClear deletes the active type328 body, its footprint and CBaddy count.");
            fastForward = true;
            modules.LoadCodeModule(TorchState.ModuleName);
            registry.Spawn(node);
            var aborted = (GodotTorchActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            Assert(aborted.State.IsDead && (aborted.ItemFlags & 1) != 0 && aborted.Lifecycle.Phase == ActorLifePhase.Dead && pulses.Count == 0,
                "Outside level 0x803 the constructor applies the abort-byte Die(0), with no authored links to pulse.");
            modules.UnloadAllCodeModules();
            registry.Free();
            (registry, modules, _) = Scene(TorchState.SurvivesFastForwardLevel);
            modules.LoadCodeModule(TorchState.ModuleName);
            registry.Spawn(node);
            var survivor = (GodotTorchActor)registry.SourceActors(ActorUpdateGroup.Enemy).Single();
            clock += 2; survivor.Advance();
            Assert(!survivor.State.IsDead && survivor.SourceUpdates == 1,
                "Level 0x803 keeps a fast-forwarded torch alive through construction and AI.");
            fastForward = false;
            modules.UnloadAllCodeModules();
            registry.Free();
            GD.Print($"Passed {assertions} native torch checks: L1A2a source328 constructor, AI/physics frames, pose packet, MGS shadow and module lifetime.");
        }
        finally
        {
            fixture.Free();
        }

        (GodotActorRegistry, GodotActorCodeModules, List<ushort>) Scene(int levelId)
        {
            var pulses = new List<ushort>();
            var registry = new GodotActorRegistry
            {
                Name = $"TorchRegistry{levelId:X}", Triggers = triggers, Environment = new(LevelId: levelId),
                ReceivePulse = pulses.Add, Player = player
            };
            fixture.AddChild(registry); registry.SetPhysicsProcess(false);
            var modules = new GodotActorCodeModules(registry, thugs, blackCat, torch: configuration);
            modules.RegisterFactories();
            return (registry, modules, pulses);
        }
        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native torch: {evidence}"); assertions++; }
    }
}
