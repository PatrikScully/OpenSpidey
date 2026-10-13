using Godot;
using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Game.Actors;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;

namespace OpenSpidey.Game.Import;

/// <summary>Checks enemy geometry as well as pose storage; a correct skeleton alone cannot catch unwelded limbs.</summary>
internal static class EnemyModelImportChecks
{
    public static void Run(PkrArchive archive, Node3D host)
    {
        (string Name, int Parts, int Clips)[] expected = [("thug", 16, 44), ("henchman", 15, 41), ("henchngt", 15, 41)];
        foreach (var asset in expected)
        {
            string path = $"data/{asset.Name}.psx";
            byte[] bytes = archive.Read(path);
            PsxScene scene = PsxSceneReader.Read(bytes);
            IAnimationPoseSource animations = PsxMatrixAnimationReader.Read(bytes)
                ?? throw new InvalidDataException($"{path}: original enemy matrix animations are absent.");
            if (animations.Parents.Count != asset.Parts || animations.ClipCount != asset.Clips ||
                scene.Models.Count != asset.Parts * 2 || scene.Models[0].NextLod != asset.Parts)
                throw new InvalidDataException($"{path}: enemy rig, clips or source high/low LOD selection changed.");

            // RenderSuperItem selects part + models[0].NextLOD at distance. Near models use part directly.
            // Its two pose swaps apply exclusively to MechList; neither enemy has the player's part order.
            ImportedAnimatedModel model = ImportedAnimatedModel.Build(scene, PsxTextureReader.Read(bytes), animations,
                playerPartOrder: false);
            try
            {
                model.AdvanceAutomatically = false;
                host.AddChild(model);
                var bindings = ReadSourceBindings(scene, asset.Parts, out int stitches);
                List<Corner> corners = VerifyMeshArrays(model, scene, bindings);
                int samples = VerifyPosedCorners(model, animations, corners);
                model.VerifyAllClips();
                model.VerifyPlayback();
                VerifySourceJointGold(model, asset.Name);
                GD.Print($"Verified enemy {asset.Name}: {asset.Parts} parts, {asset.Clips} clips, " +
                    $"{corners.Count} rendered corners, {stitches} source welds, {samples} pose samples; original part order and near LOD.");
            }
            finally { model.Free(); }
        }
    }

    internal static (int Corners,int Welds,int Samples) VerifyOriginalGeometry(ImportedAnimatedModel model,PsxScene scene,IAnimationPoseSource animations)
    {
        var sourceBindings=ReadSourceBindings(scene,animations.Parents.Count,out int welds);
        List<Corner> corners=VerifyMeshArrays(model,scene,sourceBindings);
        int samples=VerifyPosedCorners(model,animations,corners);
        model.VerifyAllClips();model.VerifyPlayback();
        return(corners.Count,welds,samples);
    }

    private static void VerifySourceJointGold(ImportedAnimatedModel model, string asset)
    {
        string fixture = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "OpenSpidey.Tests", "Fixtures", "actor-joint-poses.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(fixture));
        JsonElement test = document.RootElement.GetProperty("cases").EnumerateArray().Single(value =>
            value.TryGetProperty("native", out JsonElement native) && native.GetBoolean() && value.GetProperty("asset").GetString() == asset);
        JsonElement packet = document.RootElement.GetProperty("packets").EnumerateArray().Single(value =>
            value.GetProperty("name").GetString() == test.GetProperty("packet").GetString());
        FixedJointTransform[] key = test.GetProperty("key").EnumerateArray().Select(Pose).ToArray();
        FixedJointTransform[] gold = test.GetProperty("expected").EnumerateArray().Select(Pose).ToArray();
        model.RunClip(test.GetProperty("clip").GetInt32(), test.GetProperty("frame").GetInt32());
        for (int part = 0; part < key.Length; part++)
            if (model.GetSourceAnimationPartPose(part) != key[part]) throw new InvalidDataException($"{asset}, part {part}: native gold uses another original frame.");
        ActorPoseLinkPacket original = ActorPoseLinkPacket.Read(Convert.FromHexString(packet.GetProperty("bytes").GetString()!), key.Length);
        ActorPoseLinkPacket production = OriginalThugModelPackets.PoseLinks(asset == "thug" ? 304 : 312);
        if (!production.Links.SequenceEqual(original.Links)) throw new InvalidDataException("Native source links differ from original PC packet.");
        model.InitializeSourceJoints(production);
        int joint = 0;
        foreach (JsonElement value in test.GetProperty("joints").EnumerateArray())
        {
            short[] fields = Values(value);
            model.SetSourceJoint(joint++, new(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5]));
        }
        model.ApplySourceJointPose(true);
        model.VerifySourcePose(gold);
        model.VerifyCurrentPose();
        model.SetAnimationFinished(1);
        if (!model.Playback.IsFinished) throw new InvalidDataException("Source guard animation finish override was ignored.");
        model.SetAnimationFinished(0);
        model.ApplySourceJointPose(false);
        model.VerifySourcePose(key);
        GD.Print($"Verified {asset} corrected source joints: original link order, joint1 torso correction, raw collision matrices and native skin match PC BuildPose gold.");

        static short[] Values(JsonElement value) => value.EnumerateArray().Select(component => component.GetInt16()).ToArray();
        static FixedJointTransform Pose(JsonElement value)
        {
            short[] fields = Values(value);
            return new(fields[0], fields[1], fields[2], fields[3], fields[4], fields[5], fields[6], fields[7], fields[8], fields[9], fields[10], fields[11]);
        }
    }

    private static IReadOnlyList<BoundCharacterVertex>[] ReadSourceBindings(PsxScene scene, int parts, out int stitches)
    {
        // Independent reconstruction of the attach-point cursor in DCModel_RenderModel: declarations append,
        // references select an earlier declared point and retain that point's original part transform.
        var attachments = new List<BoundCharacterVertex>();
        var result = new IReadOnlyList<BoundCharacterVertex>[parts];
        var connected = new bool[parts, parts];
        stitches = 0;
        for (int part = 0; part < parts; part++)
        {
            var vertices = new List<BoundCharacterVertex>();
            foreach (PsxVertex vertex in scene.Models[part].Vertices)
            {
                BoundCharacterVertex binding;
                if ((vertex.Flags & 2) != 0)
                {
                    int attachment = unchecked((short)((short)vertex.Position.Y * 8)) >> 3;
                    attachment = Math.Min(attachment, 255);
                    if ((uint)attachment >= attachments.Count)
                        throw new InvalidDataException($"Enemy part {part}: invalid source stitch index {attachment}.");
                    binding = attachments[attachment];
                    connected[part, binding.PartIndex] = connected[binding.PartIndex, part] = true;
                    stitches++;
                }
                else
                {
                    binding = new(part, vertex.Position);
                    if ((vertex.Flags & 1) != 0) attachments.Add(binding);
                }
                vertices.Add(binding);
            }
            result[part] = vertices;
        }
        if (stitches == 0) throw new InvalidDataException("Enemy mesh contains no source welds.");
        // A disconnected rigid accessory is legitimate. Every part containing stitch references must,
        // however, connect to another source part; self-only attachment cannot join the body.
        for (int part = 0; part < parts; part++)
            if (scene.Models[part].Vertices.Any(vertex => (vertex.Flags & 2) != 0) &&
                !Enumerable.Range(0, parts).Any(other => other != part && connected[part, other]))
                throw new InvalidDataException($"Enemy part {part}: stitch references do not join another part.");
        return result;
    }

    private static List<Corner> VerifyMeshArrays(ImportedAnimatedModel model, PsxScene scene,
        IReadOnlyList<BoundCharacterVertex>[] bindings)
    {
        Node3D container = model.GetNode<Node3D>("MatrixParts");
        var corners = new List<Corner>();
        int meshes = 0;
        for (int part = 0; part < bindings.Length; part++)
        {
            PsxModel source = scene.Models[part];
            if ((source.Flags & 0x20) != 0 || source.Faces.Count == 0) continue;
            var instance = container.GetNode<MeshInstance3D>($"Mesh{part:D2}");
            if (instance.Mesh is not ArrayMesh mesh || instance.Transform != Transform3D.Identity)
                throw new InvalidDataException($"Enemy part {part}: source model is missing or transformed twice.");
            var groups = source.Faces.GroupBy(face => face.TextureChecksum).ToArray();
            if (mesh.GetSurfaceCount() != groups.Length)
                throw new InvalidDataException($"Enemy part {part}: source material groups were omitted.");
            for (int surface = 0; surface < groups.Length; surface++)
            {
                using Godot.Collections.Array arrays = mesh.SurfaceGetArrays(surface);
                Vector3[] positions = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                int[] bones = arrays[(int)Mesh.ArrayType.Bones].AsInt32Array();
                float[] weights = arrays[(int)Mesh.ArrayType.Weights].AsFloat32Array();
                BoundCharacterVertex[] expected = groups[surface].SelectMany(face =>
                    (face.IsTriangle ? new[] { 0, 1, 2 } : new[] { 0, 1, 2, 1, 3, 2 })
                    .Select(corner => bindings[part][face.VertexIndices[corner]])).ToArray();
                if (positions.Length != expected.Length || bones.Length != positions.Length * 4 || weights.Length != bones.Length)
                    throw new InvalidDataException($"Enemy part {part}: rendered corner or bone influence counts differ from source.");
                for (int vertex = 0; vertex < positions.Length; vertex++)
                {
                    BoundCharacterVertex binding = expected[vertex];
                    if (positions[vertex].DistanceTo(ConvertPosition(binding.Position)) > 0.000001f ||
                        bones[vertex * 4] != binding.PartIndex || Math.Abs(weights[vertex * 4] - 1) > 0.000001f ||
                        Enumerable.Range(1, 3).Any(index => weights[vertex * 4 + index] != 0))
                        throw new InvalidDataException($"Enemy part {part}, corner {vertex}: rendered vertex lost its exact source weld.");
                    corners.Add(new(binding, positions[vertex], bones[vertex * 4]));
                }
            }
            meshes++;
        }
        if (container.GetChildren().OfType<MeshInstance3D>().Count() != meshes || corners.Count == 0)
            throw new InvalidDataException("Enemy renderer selected extra LOD models or omitted all source geometry.");
        return corners;
    }

    private static int VerifyPosedCorners(ImportedAnimatedModel model, IAnimationPoseSource animations, List<Corner> corners)
    {
        var expected = new FixedJointTransform[animations.Parents.Count];
        var rendered = new Transform3D[expected.Length];
        int samples = 0;
        for (int id = 0; id < animations.ClipCount; id++)
        {
            IAnimationPoseClip clip = animations.DecodeClip(id);
            model.SelectClip(id);
            for (int frame = 0; frame < clip.FrameCount; frame++)
            {
                // Exercise both sparse-key loop wrapping and one-shot terminal extrapolation.
                foreach (bool loop in new[] { false, true })
                {
                    model.RunClip(id, frame, frame);
                    if (loop) model.CycleClip(id);
                    clip.SampleFrame(frame, loop, expected);
                    for (int part = 0; part < expected.Length; part++)
                    {
                        if (model.GetPartPose(part) != expected[part])
                            throw new InvalidDataException($"Enemy clip {id}, frame {frame}: part {part} was reordered or its pose distorted.");
                        rendered[part] = model.GetNode<Node3D>($"MatrixParts/ExactPart{part:D2}").Transform;
                    }
                    foreach (Corner corner in corners)
                    {
                        Vector3 sourcePoint = ConvertPosition(expected[corner.Source.PartIndex].TransformPoint(corner.Source.Position));
                        Vector3 renderedPoint = rendered[corner.Bone] * corner.Position;
                        float error = sourcePoint.DistanceTo(renderedPoint);
                        if (!float.IsFinite(error) || error > 0.00001f)
                            throw new InvalidDataException($"Enemy clip {id}, frame {frame}, bone {corner.Bone}: rendered corner is distorted by {error} meters.");
                    }
                    samples++;
                }
            }
        }
        return samples;
    }

    // Keep this expectation separate from PsxCoordinates, so a character-scale/sign regression is detectable.
    private static Vector3 ConvertPosition(System.Numerics.Vector3 source) => new(source.X / 1600, -source.Y / 1600, -source.Z / 1600);
    private readonly record struct Corner(BoundCharacterVertex Source, Vector3 Position, int Bone);
}
