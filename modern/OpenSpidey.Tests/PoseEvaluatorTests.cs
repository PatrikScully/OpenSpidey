using System.Numerics;
using System.Reflection;
using System.Text.Json;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;

internal static class PoseEvaluatorTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.animation-poses.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement vector in document.RootElement.EnumerateArray())
        {
            short[] joints = vector.GetProperty("joints").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            short[] expected = vector.GetProperty("transforms").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            CompressedAnimationLibrary library = Build(joints);
            DecodedAnimationClip clip = library.Clips[0].Decode();
            var transforms = new FixedJointTransform[3];
            CompressedPoseEvaluator.Evaluate(library, clip, 0, transforms);
            short[] actual = transforms.SelectMany(pose => new[] { pose.M00, pose.M01, pose.M02,
                pose.M10, pose.M11, pose.M12, pose.M20, pose.M21, pose.M22, pose.X, pose.Y, pose.Z }).ToArray();
            assert(actual.SequenceEqual(expected), "Hierarchy pose matches recovered C++ rotation and fixed-point composition.");
        }

        PsxModel parent = Model(new(new Vector3(10, 20, 30), 1), new(new Vector3(40, 50, 60), 1));
        PsxModel child = Model(new(new Vector3(1234, 1, 999), 2), new(new Vector3(1111, 0, 888), 2));
        var scene = new PsxScene(0x20006, [], new[] { parent, child }, []);
        IReadOnlyList<IReadOnlyList<BoundCharacterVertex>> bindings = StitchedVertexBindings.Read(scene, 2);
        assert(bindings[1][0] == bindings[0][1] && bindings[1][1] == bindings[0][0],
            "Stitch indices come from file Y, not dummy X, and retain the attachment's position and bone.");
        assert(bindings[0][0].Position == new Vector3(10, 20, 30), "Attachment vertices remain part of their original geometry.");
        bool rejected = false;
        try { StitchedVertexBindings.Read(scene with { Models = new[] { child, parent } }, 2); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "A stitch cannot reference an attachment that has not been emitted.");
    }

    private static PsxModel Model(params PsxVertex[] vertices) => new(0, ushort.MaxValue, vertices, []);

    private static CompressedAnimationLibrary Build(short[] joints)
    {
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x20006u); writer.Write(12u); writer.Write(3u);
            writer.Write(0x52454948u); writer.Write(8u);
            writer.Write((ushort)2); writer.Write((ushort)0); writer.Write((ushort)2); writer.Write((ushort)0);
            writer.Write(44u); writer.Write((uint)(12 + joints.Length * 3));
            writer.Write(1u); writer.Write(12u); writer.Write(1u);
            foreach (short sample in joints) { writer.Write((byte)14); writer.Write(sample); }
            writer.Write(uint.MaxValue);
        }
        return PsxCompressedAnimationReader.Read(output.ToArray())!;
    }
}
