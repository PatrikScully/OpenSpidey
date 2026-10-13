using System.Reflection;
using System.Text.Json;
using OpenSpidey.Assets.Animation;

internal static class MatrixAnimationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.matrix-animation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement vector in document.RootElement.EnumerateArray())
        {
            int frames = vector.GetProperty("frames").GetInt32();
            int interval = vector.GetProperty("interval").GetInt32();
            int parts = vector.GetProperty("parts").GetInt32();
            short[] keys = vector.GetProperty("keys").EnumerateArray().Select(value => value.GetInt16()).ToArray();
            byte[] source = Build(frames, interval, parts, keys);
            MatrixAnimationLibrary library = PsxMatrixAnimationReader.Read(source)!;
            IAnimationPoseClip clip = library.DecodeClip(0);
            var pose = new FixedJointTransform[parts];
            JsonElement expected = vector.GetProperty("expected");
            Array.Fill(source, (byte)0);
            for (int frame = 0; frame < frames; frame++)
                for (int mode = 0; mode < 2; mode++)
                {
                    clip.SampleFrame(frame, loop: mode == 1, pose);
                    short[] actual = pose.SelectMany(part => new[] { part.M00, part.M01, part.M02,
                        part.M10, part.M11, part.M12, part.M20, part.M21, part.M22, part.X, part.Y, part.Z }).ToArray();
                    short[] reference = expected[frame * 2 + mode].EnumerateArray().Select(value => value.GetInt16()).ToArray();
                    assert(actual.SequenceEqual(reference),
                        $"Matrix interpolation matches C++: frames={frames}, interval={interval}, frame={frame}, loop={mode}.");
                }
        }
        byte[] truncated = Build(2, 1, 1, new short[24]);
        bool rejected = false;
        try { PsxMatrixAnimationReader.Read(truncated.AsSpan(0, truncated.Length - 6)); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Truncated matrix packet is rejected before sampling.");
        byte[] badOffset = Build(2, 1, 1, new short[24]);
        BitConverter.GetBytes(4u).CopyTo(badOffset, 24);
        rejected = false;
        try { PsxMatrixAnimationReader.Read(badOffset); }
        catch (InvalidDataException) { rejected = true; }
        assert(rejected, "Matrix stream cannot overlap its clip table.");
    }

    private static byte[] Build(int frames, int interval, int parts, short[] keys)
    {
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(0x20006u); writer.Write(12u); writer.Write((uint)parts);
            writer.Write(42u); writer.Write((uint)(12 + keys.Length * 2));
            writer.Write(1u); writer.Write(12u); writer.Write((uint)(frames | ((interval - 1) << 16)));
            foreach (short key in keys) writer.Write(key);
            writer.Write(uint.MaxValue);
        }
        return output.ToArray();
    }
}
