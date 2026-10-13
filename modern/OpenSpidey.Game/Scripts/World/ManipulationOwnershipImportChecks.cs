using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Unchanged L1A3 initial401 constructor records and actual OBJ resources, before a player exists.</summary>
internal static class ManipulationOwnershipImportChecks
{
    private sealed class RejectedModelOwnership : Exception { }
    private sealed class UnboundImpactHost : IManipulationImpactHost
    {
        private static InvalidOperationException Unbound() => new("The constructor-only fixture has no post-census impact services.");
        public IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder => throw Unbound();
        public FixedVector3 PlayerPosition => throw Unbound();
        public FixedVector3 CameraPositionWholeUnits => throw Unbound();
        public int GroundHeight(FixedVector3 position, int above, int below, int flags) => throw Unbound();
        public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) => throw Unbound();
        public void SendExplosionMessage(object target, FixedVector3 position) => throw Unbound();
        public void CreateBigExplosion(FixedVector3 position) => throw Unbound();
        public void HitPlayer(ActorHit hit) => throw Unbound();
        public void HitBaddy(object target, ActorHit hit) => throw Unbound();
        public void ReactSymbioteToExplosion(object target) => throw Unbound();
        public void TrySpawnDebris(Func<ManipulationDebrisRequest> factory) => throw Unbound();
        public void SpawnChunkSmoke(ManipulationSmokeRequest smoke) => throw Unbound();
        public void SendAttachedPulse(ushort node) => throw Unbound();
    }
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive)
    {
        var viewport = new SubViewport { Size = new(320, 240), OwnWorld3D = true };
        owner.AddChild(viewport);
        GodotCombatSceneOwnership? ownership = null, failedOwnership = null;
        try
        {
            var root = new Node3D { Name = "OriginalPrePlayerManipulationOwnership" }; viewport.AddChild(root);
            var repository = new PsxLibraryRepository(archive);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A3_G.psx"); root.AddChild(world);
            PsxAssetLibrary geometry = repository.RequireLoaded("L1A3_G"), objects = repository.RequireLoaded("L1A3_O");
            var textures = repository.RequireLoaded("L1A3_L").Textures.Concat(objects.Textures).ToArray();
            var environment = GodotSwitchEnvironment.Create(world, geometry,
                repository.RequireLoaded("L1A3_L").Textures.Concat(geometry.Textures).ToArray(), archive.Read("data/L1A3_G.psx"));
            root.AddChild(environment);
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A3_T.trg"));
            TriggerNode[] authored = triggers.Nodes.Where(node => node.TypeCode == 1 &&
                TriggerActorDefinition.Read(node) is { ActorType: 401, InitializeOnLoad: true }).ToArray();
            Assert(authored.Length == 14 && authored[0].Id == 192 && authored[^1].Id == 274,
                "The positive constructor proof must retain all14 unchanged initial401 L1A3 records.");
            var registry = new GodotActorRegistry { Triggers = triggers, ObjectLibrary = () => objects,
                ObjectTextures = () => textures, ClaimObjectModel = world.ClaimObjectModel,
                ProcessMode = Node.ProcessModeEnum.Disabled }; root.AddChild(registry);
            ownership = new(registry);
            GodotManipulationConfiguration configuration = Configuration(environment, ownership, null);
            registry.RegisterFactory(401, (node, factory) => GodotManipulationActor.Create(node, factory, configuration));
            foreach (TriggerNode node in authored) registry.Spawn(node);
            GodotManipulationActor[] actors = registry.SourceList(ActorUpdateGroup.Environment).SourceOrder.OfType<GodotManipulationActor>().ToArray();
            Assert(registry.Player is null && actors.Length == 14 && registry.LiveCount == 14 &&
                actors.Select(actor => actor.SourceNodeId).SequenceEqual(authored.Reverse().Select(node => node.Id)),
                "Every real initial401 constructor must attach to the canonical source head before CPlayer exists.");
            foreach (GodotManipulationActor actor in actors)
            {
                TriggerActorDefinition definition = TriggerActorDefinition.Read(triggers.Nodes[actor.SourceNodeId]);
                Assert(actor.Body.Position == new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12 &&
                    actor.Body.Angles == new ActorAngles(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z) &&
                    actor.State.ItemFlags == 0x10 && actor.State.Flags == 0 && actor.State.ChunkModels.Count == 4 &&
                    actor.SourceMesh.Mesh!.GetSurfaceCount() > 0 && actor.Colliders.Count > 0 && actor.SourceUpdates == 0 &&
                    ReferenceEquals(actor.SourceVisibilityBank, objects),
                    $"Actual401 node{actor.SourceNodeId} must retain its source OBJ/pose/flags/chunk list and drawable/collision ownership without running AI.");
            }
            Assert(actors.Where(actor => actor.SourceNodeId >= 272).All(actor => actor.State.ShadowItem is null) &&
                actors.Where(actor => actor.SourceNodeId < 272).All(actor => actor.State.ShadowItem?.Identity is GodotSwitchEnvironmentItem),
                "Actual272/273/274 absent ENV shadow3DED6E2A stays null while the other11 original shadows use their existing claimed CItem owners.");
            var resources = actors.ToDictionary(actor => actor, actor => actor.CaptureOwnedResources());
            Assert(resources.Values.All(value => value.Resources.OfType<ArrayMesh>().Count() == 1 &&
                value.Resources.OfType<Shape3D>().Any() && value.Resources.OfType<Material>().Any() &&
                value.Resources.OfType<Texture2D>().Any() && value.Resources.All(GodotObject.IsInstanceValid)) &&
                resources.Values.SelectMany(value => value.Resources).Select(value => value.GetInstanceId()).Distinct().Count() ==
                resources.Values.Sum(value => value.Resources.Length),
                "Each original401 instance must explicitly own its private OBJ mesh, collision shapes, materials and textures without borrowing another actor's GPU resources.");

            // The required scene ownership callback can fail after all genuine model resources have been built.
            // Preserve the exact authored bytes and OBJ records, and prove constructor rollback at that external boundary.
            GodotManipulationActor? failed = null;
            (PsxMaterialLibrary Materials, Resource[] Resources)? failedResources = null;
            var failingRegistry = new GodotActorRegistry { Triggers = triggers, ObjectLibrary = () => objects,
                ObjectTextures = () => textures, ProcessMode = Node.ProcessModeEnum.Disabled,
                ClaimObjectModel = (_, _) => { failedResources = failed!.CaptureOwnedResources(); throw new RejectedModelOwnership(); } };
            root.AddChild(failingRegistry); failedOwnership = new(failingRegistry);
            var failingConfiguration = Configuration(environment, failedOwnership, actor => failed = actor);
            failingRegistry.RegisterFactory(401, (node, factory) => GodotManipulationActor.Create(node, factory, failingConfiguration));
            double before = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            bool rejected = false;
            try { failingRegistry.Spawn(triggers.Nodes[192]); }
            catch (RejectedModelOwnership) { rejected = true; }
            Assert(rejected && failed is { ResourcesDisposed: true } && !GodotObject.IsInstanceValid(failed) &&
                failedResources is { } released && released.Materials.IsDisposed && released.Resources.All(resource => !GodotObject.IsInstanceValid(resource)) &&
                failingRegistry.LiveCount == 0 && failingRegistry.SourceList(ActorUpdateGroup.Environment).Count == 0 &&
                Performance.GetMonitor(Performance.Monitor.ObjectResourceCount) == before,
                "A failed required model-ownership callback must retire the genuine192 constructor body and every allocated OBJ mesh/material/texture/shape, with no residual source registration.");

            GodotManipulationActor first = actors.Single(actor => actor.SourceNodeId == 192);
            MeshInstance3D mesh = first.SourceMesh; Rid meshId = ((ArrayMesh)mesh.Mesh!).GetRid();
            var formerParent = new Node3D(); root.AddChild(formerParent);
            first.Reparent(formerParent); first.Reparent(registry); formerParent.Free();
            Assert(registry.Owns(first) && !first.ResourcesDisposed && !resources[first].Materials.IsDisposed &&
                resources[first].Resources.All(GodotObject.IsInstanceValid) && ((ArrayMesh)first.SourceMesh.Mesh!).GetRid() == meshId,
                "Original401 canonical identity and its actual GPU/physics resources must survive reparent and former-parent destruction.");
            registry.DestroyImmediately(first);
            Assert(first.ResourcesDisposed && resources[first].Materials.IsDisposed &&
                resources[first].Resources.All(resource => !GodotObject.IsInstanceValid(resource)) && registry.LiveCount == 13 &&
                actors.Where(actor => !ReferenceEquals(actor, first)).All(actor => !actor.ResourcesDisposed &&
                    !resources[actor].Materials.IsDisposed && resources[actor].Resources.All(GodotObject.IsInstanceValid)),
                "Actual401 Predelete must free only its private resources, leaving other instances of the same immutable OBJ model alive.");
            foreach (GodotManipulationActor actor in actors.Where(actor => !ReferenceEquals(actor, first))) registry.DestroyImmediately(actor);
            Assert(registry.LiveCount == 0 && registry.SourceList(ActorUpdateGroup.Environment).Count == 0 &&
                resources.All(pair => pair.Key.ResourcesDisposed && pair.Value.Materials.IsDisposed &&
                    pair.Value.Resources.All(resource => !GodotObject.IsInstanceValid(resource))) && environment.Items.All(item => !item.ResourcesDisposed),
                "Destroying all original401 bodies must release their actual GPU/physics resources and canonical registrations while independent ENV shadow owners remain alive.");
            GD.Print("Verified all14 genuine L1A3 initial401 constructors before CPlayer, actual OBJ/ENV/pose/source-list identities, three legitimate absent shadows, deterministic constructor failure, reparent and private mesh/material/texture/shape destruction.");
        }
        finally
        {
            if (GodotObject.IsInstanceValid(viewport)) viewport.Free();
            failedOwnership?.Dispose(); ownership?.Dispose();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private static GodotManipulationConfiguration Configuration(GodotSwitchEnvironment environment,
        GodotCombatSceneOwnership ownership, Action<GodotManipulationActor>? constructing) => new()
    {
        Environment = environment,
        AttachToEnvironmentalList = actor => { ownership.Actors.RegisterConstructingActor(actor); constructing?.Invoke(actor); },
        DetachFromEnvironmentalList = ownership.Actors.Remove,
        SourceBaddyOrder = () => throw new InvalidOperationException("Source401 AI is excluded before actual player binding."),
        IgnoreSphereRadius = () => throw new InvalidOperationException("Player collision policy is unbound before census completes."),
        LineToItem = (_, _, _) => throw new InvalidOperationException("Source401 AI is excluded before actual world-query binding."),
        HitBaddy = (_, _) => throw new InvalidOperationException("Source401 hits are excluded before actual combat binding."),
        ShakeCamera = (_, _) => throw new InvalidOperationException("The original camera has not been constructed."),
        ImpactEffects = new UnboundImpactHost()
    };
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
