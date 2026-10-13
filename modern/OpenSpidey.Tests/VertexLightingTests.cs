using System.Text.Json;
using System.Numerics;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Rendering;

internal static class VertexLightingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(VertexLightingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.vertex-lighting.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        var light = new SourceModelLight(new(-2047, -2896, -2047, 0, 0, 4096, 0, 0, -4096, 0, 0, 0),
            new(1520, 2000, 1840, 1440, 1920, 1760, 1440, 1920, 1760, 0, 0, 0), 1760, 1600, 1600);
        SourceVertexLightingState state = new();
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            FixedJointTransform basis = SourceVertexLighting.LightBasis(light, Matrix(input, 0), Matrix(input, 9));
            int[] matrix = [basis.M00, basis.M01, basis.M02, basis.M10, basis.M11, basis.M12, basis.M20, basis.M21, basis.M22];
            PsxVertex[] vertices = [new(Vector3.Zero, (ushort)input[27]), new(Vector3.Zero, (ushort)input[28]), new(new(0, input[29], 0), 2)];
            PsxNormal[] rawNormals = Enumerable.Range(0, 3).Select(vertex => new PsxNormal(
                new(input[18 + vertex * 3], input[19 + vertex * 3], input[20 + vertex * 3]), 0)).Append(new(Vector3.Zero, 0)).ToArray();
            PsxModel model = new(8, ushort.MaxValue, vertices, new PsxFace[] { new(0, new byte[] { 0, 1, 2, 2 }, 0, null, []) })
                { Normals = rawNormals };
            Vector3[] normals = SourceVertexLighting.ReadPlainNormals(model);
            state.BeginActor();
            SourceRgb24[] colours = SourceVertexLighting.CalculatePart(model, normals, light, basis, state);
            SourceLightColour[] rawColours = [SourceVertexLighting.Calculate(light, basis, normals[0]),
                SourceVertexLighting.Calculate(light, basis, normals[1]), state.ReadAttachment(input[29])];
            assert(matrix.SequenceEqual(expected[0].EnumerateArray().Select(value => value.GetInt32())),
                $"Actual PC vertex-lighting matrix case {index}: light/body/part Q12 products preserve wrapping sum and narrow stores.");
            for (int vertex = 0; vertex < 3; vertex++)
            {
                uint[] normalBits = [Bits(normals[vertex].X), Bits(normals[vertex].Y), Bits(normals[vertex].Z)];
                uint[] expectedNormals = expected[1][vertex].EnumerateArray().Select(value => value.GetUInt32()).ToArray();
                uint[] colourBits = [Bits(rawColours[vertex].Red), Bits(rawColours[vertex].Green), Bits(rawColours[vertex].Blue)];
                uint[] expectedColours = expected[2][vertex].EnumerateArray().Select(value => value.GetUInt32()).ToArray();
                SourceRgb24 packed = new(Pack(expectedColours[0]), Pack(expectedColours[1]), Pack(expectedColours[2]));
                assert(normalBits.SequenceEqual(expectedNormals) && colourBits.SequenceEqual(expectedColours) && colours[vertex] == packed,
                    $"Actual PC vertex-lighting case {index}, vertex {vertex}: normal=[{string.Join(',', normalBits)}] expected=[{string.Join(',', expectedNormals)}], " +
                    $"colour=[{string.Join(',', colourBits)}] expected=[{string.Join(',', expectedColours)}], packed={colours[vertex]} expected={packed}.");
            }
            assert(state.AttachmentCursor == expected[3].GetInt32() && SourceVertexLighting.IsEnabled(model, 0x482) && !SourceVertexLighting.IsEnabled(model, 2),
                "Actual source flags1 export and flags2 reuse the shared persistent colour table; model8 requires item80 for lighting.");
            index++;
        }
        static byte Pack(uint bits) => (byte)Math.Clamp((int)((double)BitConverter.UInt32BitsToSingle(bits) * 255), 0, 255);
    }
    private static uint Bits(float value) => BitConverter.SingleToUInt32Bits(value);
    private static FixedJointTransform Matrix(int[] values, int start) => new((short)values[start], (short)values[start + 1], (short)values[start + 2],
        (short)values[start + 3], (short)values[start + 4], (short)values[start + 5], (short)values[start + 6], (short)values[start + 7], (short)values[start + 8], 0, 0, 0);
}
