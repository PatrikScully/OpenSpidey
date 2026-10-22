using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

/// <summary>Claim ownership boundaries use unchanged L3A1 ENV items and its actual154 chunk program, including originally hidden ruin109.</summary>
internal static class ClaimedEnvironmentChunkImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, GodotPlayer player)
    {
        var viewport = new SubViewport { Size = new(320, 240), OwnWorld3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        GodotPlayerMeleeSession? session = null;
        try
        {
            var root = new Node3D { Name = "OriginalClaimedEnvironmentChunkBoundary" }; viewport.AddChild(root);
            var repository = new PsxLibraryRepository(archive);
            byte[] raw = archive.Read("data/L3A1_G.psx");
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L3A1_G.psx"); root.AddChild(world);
            PsxAssetLibrary bank = repository.RequireLoaded("L3A1_G");
            ImportedLevelSource source = world.Sources.Single(source => ReferenceEquals(source.Scene, bank.Scene));
            var textures = repository.RequireLoaded("L3A1_L").Textures.Concat(bank.Textures).ToArray();
            int triggerIndex = Enumerable.Range(0, bank.Scene.Models.Count).First(index =>
                bank.Scene.Models[index].Faces.Any(face => OriginalCollisionFilter.IsTriggerZone(face.CollisionWord ?? 0)));
            double resourcesBeforeFailure = Performance.GetMonitor(Performance.Monitor.ObjectResourceCount);
            bool rejected = false;
            try
            {
                GodotSwitchEnvironmentItem unsupported = GodotSwitchEnvironmentItem.Create(bank, textures, triggerIndex, triggerIndex, default);
                unsupported.Free();
            }
            catch (NotSupportedException) { rejected = true; }
            Assert(rejected && Performance.GetMonitor(Performance.Monitor.ObjectResourceCount) == resourcesBeforeFailure,
                "Rejecting an actual source trigger-face item must release its partially built claimed meshes, materials, arrays and collision shapes before transferring ENV ownership.");
            var environment = GodotSwitchEnvironment.Create(world, bank, textures, raw); root.AddChild(environment);
            var visibility = new GodotEnvironmentVisibility(() => world, repository, (live, index) =>
                ReferenceEquals(live, source) ? environment.Items.SingleOrDefault(item => item.SourceInstanceIndex == index) : null);
            environment.ItemClaimed += visibility.TransferFlagsToClaimedItem;
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L3A1_T.trg"));
            var registry = new GodotActorRegistry { Triggers = triggers, Player = player, ProcessMode = Node.ProcessModeEnum.Disabled }; root.AddChild(registry);
            session = new(registry, player, () => world); session.RegisterSwitchEnvironment(environment);
            var mission = new MissionRuntime(triggers, registry, []); registry.ReceivePulse = mission.ReceivePulse;
            uint clock = 100;
            registry.EnvironmentChunks = new GodotEnvironmentChunks(registry, visibility, path => archive.Read(path),
                checksum => mission.TriggerCommandPoint(checksum), () => clock);
            // First exercise source switch edits before shared preparation. No407 script or authored model is fabricated.
            var building = (GodotSwitchEnvironmentItem)environment.Resolve(0xBC64AF62)!.Identity;
            environment.MarkSwitchFaces(building.State);
            visibility.PrepareChunkSource(raw);
            GodotEnvironmentVisibilityItem sourceBuilding = visibility.SourceItems[62];
            Assert(building.SourceInstanceIndex == 62 && ReferenceEquals(building.SharedSourceModel, sourceBuilding.ChunkModel) &&
                building.SharedSourceModel!.Faces.All(face => (face.CollisionWord & 0x02000000) != 0),
                "Actual original building62 claim and chunk source must alias one prepared model while retaining prior Switch_SetSwitchFaceFlags edits.");
            // These authored ruins start with no draw bits. Their existing native item owner must survive until source42B5 reveals the real faces.
            var stationary = (GodotSwitchEnvironmentItem)environment.Resolve(0x5FA09D29)!.Identity;
            var moving = (GodotSwitchEnvironmentItem)environment.Resolve(0x76AC3057)!.Identity;
            GodotEnvironmentVisibilityItem sourceStatic = visibility.SourceItems[108], sourceMoving = visibility.SourceItems[109];
            Assert(stationary.SourceInstanceIndex == 108 && moving.SourceInstanceIndex == 109 &&
                stationary.Mesh.Mesh!.GetSurfaceCount() == 0 && moving.Mesh.Mesh!.GetSurfaceCount() == 0 &&
                ReferenceEquals(stationary.SharedSourceModel, sourceStatic.ChunkModel) && ReferenceEquals(moving.SharedSourceModel, sourceMoving.ChunkModel),
                "Original hidden ruins108/109 must retain one genuine claimed identity and empty native geometry before chunk reveal.");
            environment.MarkSwitchFaces(stationary.State); environment.MarkSwitchFaces(moving.State);
            MeshInstance3D staticMesh = stationary.Mesh, flyingMesh = moving.Mesh;
            var camera = new Camera3D { Current = true, Near = .01f, Far = 10000 }; root.AddChild(camera); camera.MakeCurrent();
            camera.Position = PsxCoordinates.FixedWorldPosition(building.State.Position) + new Vector3(0, 10, 30);
            camera.LookAt(PsxCoordinates.FixedWorldPosition(building.State.Position));
            registry.Spawn(triggers.Nodes[154]);
            GodotEnvironmentChunkControl control = registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotEnvironmentChunkControl>().Single();
            Assert((building.State.ItemFlags & 0x21) == 0x21 && !building.Mesh.Visible &&
                ReferenceEquals(staticMesh, stationary.Mesh) && ReferenceEquals(flyingMesh, moving.Mesh) &&
                stationary.Mesh.Visible && moving.Mesh.Visible && staticMesh.Mesh!.GetSurfaceCount() > 0 && flyingMesh.Mesh!.GetSurfaceCount() > 0,
                "Authored15442B5 must hide the existing claimed building and reveal real static/flying faces through their same native mesh nodes.");
            Assert(stationary.SharedSourceModel!.Faces.All(face => (face.CollisionWord & 0x02000000) != 0 && (face.CollisionWord & 0x10000) == 0) &&
                moving.SharedSourceModel!.Faces.All(face => (face.CollisionWord & 0x02010000) == 0x02010000) &&
                stationary.Colliders.Any(body => body.CollisionLayer != 0) && moving.Colliders.All(body => body.CollisionLayer == 0),
                "Static claimed ruins gain actual source collision while flying ruins retain original no-collision20/10000 policy and prior switch-face02000000 bits.");
            Assert(!ImportedMeshes(world, sourceStatic.InstanceIndex).Any() && !ImportedMeshes(world, sourceMoving.InstanceIndex).Any(),
                "Claimed reveal must not build duplicate geometry under the imported bank root.");
            EnvironmentChunkEntry entry = control.State.Entries.Single(entry => ReferenceEquals(entry.Item, sourceMoving));
            FixedVector3 oldPosition = moving.State.Position; ActorAngles oldAngles = moving.State.Angles;
            FixedVector3 velocity = entry.Velocity; ActorAngles spin = entry.AngularVelocity;
            control.AdvanceSourceClock(clock);
            Assert(moving.State.Position == oldPosition + velocity && moving.State.Angles == new ActorAngles(unchecked((short)(oldAngles.X + spin.X)),
                unchecked((short)(oldAngles.Y + spin.Y)), unchecked((short)(oldAngles.Z + spin.Z))) &&
                sourceMoving.Position == moving.State.Position && sourceMoving.Angles == moving.State.Angles &&
                moving.Mesh.Transform.Origin.IsEqualApprox(PsxCoordinates.FixedWorldPosition(moving.State.Position)),
                "Actual CChunkControl must move the canonical claimed CItem fixed position/short angles and its existing native geometry once per AI.");
            stationary.HideModel(); Assert(!stationary.Mesh.Visible && (sourceStatic.Model.Flags & 0x20) != 0,
                "Subsequent claimed HideModel must mutate the same shared chunk model state.");
            sourceStatic.Model.Flags &= 0xFFDF; visibility.SynchronizeSourceItem(sourceStatic);
            Assert(stationary.Mesh.Visible && ReferenceEquals(staticMesh, stationary.Mesh), "Shared source model reveal must restore the same claimed native owner.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
            foreach (GodotEnvironmentChunkControl native in registry.SourceList(ActorUpdateGroup.Control).SourceOrder.OfType<GodotEnvironmentChunkControl>().ToArray())
                registry.DestroyImmediately(native);
            // Claimed resources outlive ordinary reparenting and the former container, then retire only at nativePredelete.
            ArrayMesh owned = (ArrayMesh)moving.Mesh.Mesh!;
            var movingResources = moving.CaptureOwnedResources();
            var staticResources = stationary.CaptureOwnedResources();
            Assert(moving.CachedSurfaceArrayCount == owned.GetSurfaceCount() && movingResources.Resources.OfType<ArrayMesh>().Count() == 2 &&
                !movingResources.Resources.OfType<Shape3D>().Any() && staticResources.Resources.OfType<Shape3D>().Any() &&
                movingResources.Resources.OfType<Material>().Any() &&
                movingResources.Resources.OfType<Texture2D>().Any() && movingResources.Resources.All(GodotObject.IsInstanceValid),
                "Each claimed item must own both GPU meshes, every cached surface array, original materials/textures and the actual eligible static collision shapes; flying no-collision10000 faces build no shapes.");
            var temporary = new Node3D(); root.AddChild(temporary);
            moving.Reparent(temporary); moving.Reparent(environment); temporary.Free();
            Assert(!moving.ResourcesDisposed && !movingResources.Materials.IsDisposed && movingResources.Resources.All(GodotObject.IsInstanceValid) &&
                GodotObject.IsInstanceValid(moving) && ReferenceEquals(moving.Mesh.Mesh, owned) && owned.GetSurfaceCount() > 0,
                "Claimed native meshes, materials and surfaces must survive reparent and former-parent destruction.");
            moving.Free();
            Assert(moving.ResourcesDisposed && !GodotObject.IsInstanceValid(moving) && moving.CachedSurfaceArrayCount == 0 &&
                movingResources.Materials.IsDisposed && movingResources.Resources.All(resource => !GodotObject.IsInstanceValid(resource)),
                "Actual claimed itemPredelete must invalidate both private meshes, original materials/textures and collision shapes, and dispose all cached surface arrays.");
            Assert(!stationary.ResourcesDisposed && !staticResources.Materials.IsDisposed && staticResources.Resources.All(GodotObject.IsInstanceValid) &&
                staticMesh.Mesh!.GetSurfaceCount() > 0,
                "Destroying the flying claimed owner must leave the independently owned static item's meshes/materials/textures/collision shapes alive.");
            stationary.Free();
            Assert(stationary.ResourcesDisposed && stationary.CachedSurfaceArrayCount == 0 && staticResources.Materials.IsDisposed &&
                staticResources.Resources.All(resource => !GodotObject.IsInstanceValid(resource)),
                "Actual static claimed itemPredelete must also release its real collision shapes and every private mesh/material/texture/surface array.");
            GD.Print("Verified genuine claimed ENV chunk ownership: actual L3A1 building62 and hidden ruins108/109, shared source/DC faces, authored15442B5, exact flying pose, source collision policy, no duplicate geometry and reparent/Predelete lifetime.");
        }
        finally
        {
            session?.Dispose();
            if (GodotObject.IsInstanceValid(viewport)) viewport.Free();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
    private static IEnumerable<ImportedSceneMesh> ImportedMeshes(ImportedLevelWorld world, int index) =>
        world.Geometry.GetChildren().OfType<Node3D>().Where(group => group.Name.ToString().Equals("L3A1_G", StringComparison.OrdinalIgnoreCase))
            .SelectMany(group => group.GetChildren().OfType<ImportedSceneMesh>()).Where(mesh => mesh.SourceInstanceIndex == index && !mesh.IsQueuedForDeletion());
    private static void Assert(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
