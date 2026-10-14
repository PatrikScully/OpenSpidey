using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Private original L2A2 world over actual autoexec151320 and linked162/285 ENV visibility, independent of unported boss AI.</summary>
internal static class MissionRenderCompatibilityImportChecks
{
    public static async Task VerifyAsync(Node owner, PkrArchive archive)
    {
        var viewport = new SubViewport { Name = "OriginalL2A2RenderingCommands", Size = new(1280, 720), OwnWorld3D = true };
        owner.AddChild(viewport);
        int checks = 0;
        try
        {
            var root = new Node3D(); viewport.AddChild(root);
            var repository = new PsxLibraryRepository(archive);
            ImportedLevelWorld world = ImportedLevelWorld.Load(repository, "data/L2A2_G.psx"); root.AddChild(world);
            Node geometryGroup = world.Geometry.GetChildren().Single(group => group.Name.ToString().Equals("L2A2_G", StringComparison.OrdinalIgnoreCase));
            TriggerFile triggers = TriggerFile.Read(archive.Read("data/L2A2_T.trg"));
            TriggerInstruction original151 = TriggerCommandReader.Read(triggers.Nodes[0]).Single(instruction => instruction.Opcode == 151);
            var visibility = new GodotEnvironmentVisibility(() => world, repository);
            GodotEnvironmentVisibilityItem[] items = visibility.SourceItems.ToArray();
            ImportedSceneMesh[] meshes = world.Geometry.GetChildren().SelectMany(group => group.GetChildren()).OfType<ImportedSceneMesh>().ToArray();
            ImportedCollisionBody[] bodies = world.Collision.GetChildren().SelectMany(group => group.GetChildren()).OfType<ImportedCollisionBody>().ToArray();
            var meshState = meshes.ToDictionary(mesh => mesh, mesh => (mesh.Mesh, mesh.Transform));
            var bodyState = bodies.ToDictionary(body => body, body => (body.Transform, body.CollisionLayer));
            ushort[] modelFlags = items[0].Source.Scene.Models.Select(model => model.Flags).ToArray();
            Assert(items.Length > 0 && meshes.Length > 0 && bodies.Length > 0 &&
                BinaryPrimitives.ReadUInt16LittleEndian(original151.Arguments.Span) == 320,
                "The private proof retains real L2A2 G/O native geometry/collision and its exact authored autoexec151320 operand.");
            new DualBufferSizeCommandHandler().Execute(new(triggers.Nodes[0], 0), original151);
            Assert(viewport.Size == new Vector2I(1280, 720) && meshes.All(mesh => ReferenceEquals(mesh.Mesh, meshState[mesh].Mesh) &&
                mesh.Transform == meshState[mesh].Transform) && bodies.All(body => body.Transform == bodyState[body].Transform &&
                body.CollisionLayer == bodyState[body].CollisionLayer),
                "Genuine PC151320 leaves the widescreen viewport, all actual mesh identities, transforms and collision buffers untouched.");
            var handler = new LinkedEnvironmentVisibilityCommandHandler(triggers, visibility);
            foreach (ushort nodeId in new ushort[] { 162, 285 })
            {
                TriggerNode node = triggers.Nodes[nodeId];
                TriggerInstruction original13 = TriggerCommandReader.Read(node).Single(instruction => instruction.Opcode == 13);
                ushort[] links = MissionRuntime.ReadLinks(node).ToArray();
                HashSet<uint> checksums = links.Select(id => TriggerEnvironmentReference.ReadChecksum(triggers.Nodes[id])).ToHashSet();
                HashSet<GodotEnvironmentVisibilityItem> affected = checksums.Select(checksum => items.FirstOrDefault(item => item.ModelChecksum == checksum))
                    .OfType<GodotEnvironmentVisibilityItem>().ToHashSet();
                ushort[] initialFlags = items.Select(item => item.ItemFlags).ToArray();
                Assert(BinaryPrimitives.ReadUInt16LittleEndian(original13.Arguments.Span) == 0 && links.Length == (nodeId == 162 ? 1 : 47) && affected.Count > 0,
                    $"Untouched L2A2 node{nodeId} retains actual13 hide operand and its complete original ENV link list.");
                handler.Execute(new(node, 0), original13);
                Assert(items.Select((item, index) => item.ItemFlags == (affected.Contains(item) ? (ushort)(initialFlags[index] | 1) : initialFlags[index])).All(value => value),
                    $"Actual L2A2 node{nodeId}/13 hides only the first matching ENV instances and retains every other source flag/item.");
                foreach (GodotEnvironmentVisibilityItem item in affected)
                {
                    ImportedSceneMesh[] itemMeshes = meshes.Where(mesh => ReferenceEquals(mesh.GetParent(), geometryGroup) &&
                        mesh.SourceInstanceIndex == item.InstanceIndex).ToArray();
                    ImportedCollisionBody[] itemBodies = bodies.Where(body => string.Equals(body.SourceAsset, item.Source.AssetPath, StringComparison.OrdinalIgnoreCase) &&
                        body.InstanceIndex == item.InstanceIndex).ToArray();
                    Assert(itemMeshes.All(mesh => !mesh.Visible && ReferenceEquals(mesh.Mesh, meshState[mesh].Mesh) && mesh.Transform == meshState[mesh].Transform) &&
                        itemBodies.All(body => (body.SourceFlags & 1) != 0 && body.CollisionLayer == 0 && body.Transform == bodyState[body].Transform),
                        $"Original ENV checksum{item.ModelChecksum:X8} hides its actual source mesh/collision without deleting or moving native resources.");
                }
                byte[] show = new byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(show, 1);
                // Independent positive flag-path probe; the authored startup13 instruction above remains untouched.
                handler.Execute(new(node, 0), new(13, original13.SourceOffset, show));
                Assert(items.Select((item, index) => item.ItemFlags == (affected.Contains(item) ? (ushort)(initialFlags[index] & ~1) : initialFlags[index])).All(value => value),
                    "The positive original13 path clears only ENVflag0 through the same genuine item identities.");
            }
            Assert(items[0].Source.Scene.Models.Select(model => model.Flags).SequenceEqual(modelFlags) &&
                meshes.All(mesh => ReferenceEquals(mesh.Mesh, meshState[mesh].Mesh) && mesh.Transform == meshState[mesh].Transform) &&
                viewport.Size == new Vector2I(1280, 720),
                "Original visibility commands retain source model metadata and private GPU geometry;151 never forces the console320 operand onto Godot's viewport.");
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print($"Passed {checks} private L2A2 rendering-command checks: authored151320 keeps widescreen/native buffers and original162/285 linked13 updates actual ENV mesh/collision. Boss construction, AI and ordinary campaign traversal were not exercised.");
        }
        finally { viewport.Free(); }

        void Assert(bool condition, string evidence)
        { if (!condition) throw new InvalidDataException("Native L2A2 rendering commands: " + evidence); checks++; }
    }
}
