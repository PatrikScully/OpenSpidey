using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Audio;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Actual L1A4 static vault duplicate, restart191 commands and untouched moving platform45 script.</summary>
internal static class EnvironmentVisibilityImportChecks
{
    public static async Task VerifyAsync(Node3D owner, PkrArchive archive, PsxLibraryRepository repository)
    {
        var viewport = new SubViewport { Name = "OriginalL1A4VaultVisibilityProof", OwnWorld3D = true,
            Size = new(640, 480), RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L1A4_G.psx"); root.AddChild(world);
            ImportedLevelSource source = world.Sources.Single(source => source.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase));
            PsxAssetLibrary objects = repository.RequireLoaded("L1A4_O");
            var textures = repository.RequireLoaded("L1A4_L").Textures.Concat(objects.Textures).ToArray();
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L1A4_T.trg"));
            var visibility = new GodotEnvironmentVisibility(() => world, repository);
            GodotEnvironmentVisibilityItem item = visibility.SourceItems[155];
            ImportedSceneMesh staticDoor = world.Geometry.GetChildren().SelectMany(group => group.GetChildren())
                .OfType<ImportedSceneMesh>().Single(mesh => mesh.SourceInstanceIndex == 155 && mesh.GetParent().Name.ToString().Equals("L1A4_G", StringComparison.OrdinalIgnoreCase));
            ImportedCollisionBody[] staticBodies = world.Collision.GetChildren().SelectMany(group => group.GetChildren())
                .OfType<ImportedCollisionBody>().Where(body => body.SourceAsset == source.AssetPath && body.InstanceIndex == 155).ToArray();
            var layers = staticBodies.Select(body => body.CollisionLayer).ToArray();
            Assert(source.Scene.Instances.Count == 254 && item.InstanceIndex == 155 && ReferenceEquals(item.Source, source) &&
                item.ModelChecksum == 0x2A023428 && source.Scene.Models[155].Flags == 8 && item.ItemFlags == 0 &&
                staticDoor.Visible && staticDoor.Mesh!.GetSurfaceCount() > 0 && staticBodies.Length != 0 && layers.All(layer => layer != 0),
                "The real G155 VaultDoor01 has its original visible mesh, source flag0, model flag8 and native collision.");
            Transform3D originalTransform = staticDoor.Transform;
            VisibilityBoxImportChecks.Verify(visibility, item, staticDoor, staticBodies);
            var handler = new EnvironmentVisibilityCommandHandler(visibility);
            TriggerNode restart = triggers.Nodes[1];
            TriggerInstruction[] commands = TriggerCommandReader.Read(restart).Where(command => command.Opcode == handler.Opcode).ToArray();
            Assert(commands.Length == 12, "The actual L1A4 death restart retains all twelve authored named visibility commands.");
            foreach (TriggerInstruction command in commands) handler.Execute(new(restart, 0), command);
            Assert(item.ItemFlags == 1 && !staticDoor.Visible && staticDoor.Transform == originalTransform &&
                staticBodies.All(body => body.SourceFlags == 1 && body.CollisionLayer == 0) &&
                source.Scene.Models[155].Flags == 8 && source.Scene.Models[155].Checksum == 0x2A023428,
                "Authored191 hides only the static item flag0; its source model and native mesh identity persist.");
            visibility.SetVisibility(0x2A023428, true);
            Assert(item.ItemFlags == 0 && staticDoor.Visible && staticBodies.Select(body => body.CollisionLayer).SequenceEqual(layers),
                "Showing the same actual ENV item restores its original collision layers and mesh without replacing identities.");
            visibility.SetVisibility(0x2A023428, false);

            var sounds = new GodotSoundChannels { Archive = archive, LevelGroup = SoundCatalog.Find("L1A4") }; root.AddChild(sounds);
            sounds.Controller.PlayerBank = SoundDefinitionBank.Read(archive.Read("data/spidey.sfx"));
            sounds.Controller.LevelBank = SoundDefinitionBank.Read(archive.Read("data/L1A4.sfx"), levelSpecific: true);
            var registry = new GodotActorRegistry { Triggers = triggers, ObjectLibrary = () => objects, ObjectTextures = () => textures,
                ClaimObjectModel = world.ClaimObjectModel, Environment = new(LevelId: 0x104),
                Sounds = sounds.Controller, SoundListener = () => new(default), SuspensionDistance = uint.MaxValue };
            root.AddChild(registry); registry.SetPhysicsProcess(false);
            GodotPlatformActor? door = null;
            registry.Spawned += actor => door = actor as GodotPlatformActor;
            registry.Spawn(triggers.Nodes[45]);
            GodotPlatformActor platform = door ?? throw new InvalidDataException("Actual vault node45 did not construct CPlatform402.");
            FixedVector3 closed = new FixedVector3(-3163, -671, 1956) << 12;
            FixedVector3 opened = new FixedVector3(-3163, -671, 2691) << 12;
            Assert(platform.Definition.ActorType == 402 && platform.Definition.Links.SequenceEqual(new ushort[] { 48 }) &&
                platform.Motion.Position == closed && TriggerPlacement.Read(triggers.Nodes[48]).Position == new TriggerPosition(-3163, -671, 2691),
                "Independent authored vault placements retain closed node45 and its linked open position48.");
            platform.Advance();
            MeshInstance3D movingMesh = platform.GetNode<MeshInstance3D>("SourceModel");
            Assert(platform.ModelChecksum == 0xC2582125 && platform.Visible && movingMesh.Mesh!.GetSurfaceCount() > 0 &&
                platform.Motion.Position == closed && platform.ScriptState.Active && !staticDoor.Visible && item.ItemFlags == 1,
                "The dynamic VaultDoor model remains visible while the distinct static VaultDoor01 duplicate stays hidden.");
            registry.SignalActiveActors(45);
            for (int update = 0; update < 350; update++) platform.Advance();
            await Physics(); await Physics();
            Assert(platform.Motion.Position == opened && platform.Visible && platform.ScriptState.Active &&
                ReferenceEquals(platform.GetNode<MeshInstance3D>("SourceModel"), movingMesh) && !staticDoor.Visible && item.ItemFlags == 1 &&
                sounds.StartedCount == 2,
                "Untouched original signal/sound90/wait8/sound52/MoveTo script animates the actual door to node48 without deleting its mesh.");
            ImportedCollisionBody[] movingBodies = platform.GetNode<Node3D>("WorldCollision").GetChildren().OfType<ImportedCollisionBody>().ToArray();
            Assert(movingBodies.Length != 0 && movingBodies.All(body => body.ModelChecksum == 0xC2582125 &&
                body.SupportId == platform.GetInstanceId() && body.GlobalPosition.DistanceTo(platform.GlobalPosition) < 0.001f &&
                body.SourceTriangles.Count != 0 && body.GetChildren().OfType<CollisionShape3D>().All(shape => !shape.Disabled)),
                "Real native vault collision follows the opened dynamic platform and preserves actual source triangles/body ownership.");
            registry.SignalActiveActors(45);
            for (int update = 0; update < 350; update++) platform.Advance();
            Assert(platform.Motion.Position == closed && platform.Visible && platform.ScriptState.Active &&
                sounds.StartedCount == 4 && !staticDoor.Visible,
                "The second genuine signal returns the same persistent moving door to its own node45 and loops for another signal.");
            world.UnloadSource(source.AssetPath);
            bool staleRejected = false;
            try { visibility.SetItemVisibility(source, 155, true); }
            catch (InvalidOperationException) { staleRejected = true; }
            Assert(staleRejected && visibility.SourceItems.Count == 0 && !item.RegionUsable,
                "Visibility cannot mutate a removed G-region generation or accidentally target the surviving OBJ prototypes.");
            await Physics(); await Physics();
            GD.Print("Verified actual L1A4 vault: all12 restart191 commands, static G155 hidden/restored, independent platform45 source signal/sound/wait/open/close and persistent native mesh/collision.");
        }
        finally { viewport.Free(); }
        async Task Physics() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        static void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException($"Native ENV visibility: {evidence}"); }
    }
}
