using System.Numerics;
using System.Text.Json;
using OpenSpidey.Assets.Models;

internal static class PsxRenderPolicyTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PsxRenderPolicyTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.psx-render-policy.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int caseIndex = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            JsonElement[] originalFaces = input.GetProperty("faces").EnumerateArray().ToArray();
            JsonElement[] preparedFaces = expected.GetProperty("faces").EnumerateArray().ToArray();
            PsxFace[] faces = originalFaces.Select(face => new PsxFace(
                face.GetProperty("flags").GetUInt32(),
                face.GetProperty("indices").EnumerateArray().Select(value => value.GetByte()).ToArray(),
                face.GetProperty("color").GetUInt32(), null, [], face.GetProperty("collision").GetUInt32())).ToArray();
            var model = new PsxModel(input.GetProperty("flags").GetUInt16(), 0,
                Enumerable.Range(0, 64).Select(index => new PsxVertex(new Vector3(index, index * 2, -index), 0)).ToArray(), faces);
            for (int faceIndex = 0; faceIndex < faces.Length; faceIndex++)
            {
                PsxFace face = faces[faceIndex];
                uint prepared = preparedFaces[faceIndex].GetProperty("flags").GetUInt32();
                assert(PsxRenderPolicy.PrepareFaceFlags(face.Flags) == prepared,
                    $"PSX case {caseIndex} face {faceIndex}: exact original parser flag preparation.");
                assert(PsxRenderPolicy.IsFaceVisible(face.Flags) == ((prepared & 0xC0) != 0),
                    $"PSX case {caseIndex} face {faceIndex}: original processed C0 visibility test.");
                // The original parser duplicates triangle corner 2 into packet corner 3. Rendering
                // uses only three corners for triangles; the on-disk collision/face record stays intact.
                byte[] preparedIndices = preparedFaces[faceIndex].GetProperty("indices").EnumerateArray()
                    .Select(value => value.GetByte()).ToArray();
                assert(face.VertexIndices.Take(face.IsTriangle ? 3 : 4)
                        .SequenceEqual(preparedIndices.Take(face.IsTriangle ? 3 : 4)),
                    $"PSX case {caseIndex} face {faceIndex}: surviving triangle/quad corners retain source vertex indices.");
            }
            bool expectedVisible = (expected.GetProperty("modelFlags").GetUInt16() & 0x20) == 0;
            assert(PsxRenderPolicy.IsModelVisible(model) == expectedVisible,
                $"PSX case {caseIndex}: original aggregate model visibility, including empty models and authored hidden flag.");
            int[] survivingIndices = faces.Select((face, index) => (face, index))
                .Where(entry => PsxRenderPolicy.IsFaceVisible(entry.face.Flags)).Select(entry => entry.index).ToArray();
            assert(survivingIndices.SequenceEqual(expected.GetProperty("visibleIndices").EnumerateArray()
                    .Select(value => value.GetInt32())),
                $"PSX case {caseIndex}: face filtering preserves original face order/indices.");
            for (int faceIndex = 0; faceIndex < faces.Length; faceIndex++)
            {
                PsxFace face = faces[faceIndex]; JsonElement original = originalFaces[faceIndex];
                assert(ReferenceEquals(model.Faces[faceIndex], face)
                    && face.Flags == original.GetProperty("flags").GetUInt32()
                    && face.Color == original.GetProperty("color").GetUInt32()
                    && face.CollisionWord == original.GetProperty("collision").GetUInt32()
                    && face.VertexIndices.SequenceEqual(original.GetProperty("indices").EnumerateArray()
                        .Select(value => value.GetByte())),
                    $"PSX case {caseIndex} face {faceIndex}: render policy leaves original collision metadata and source face packets intact.");
            }
            caseIndex++;
        }
        var wall = new PsxFace(0x00100880, [0, 1, 2, 3], 0xFF00FF00, null, [], 0);
        assert(PsxRenderPolicy.PrepareFaceFlags(wall.Flags) == 0x00100800
            && !PsxRenderPolicy.IsFaceVisible(wall.Flags)
            && !PsxRenderPolicy.IsModelVisible(new(0, 0, [], [wall])),
            "Original L1A1 G green collision walls remain nonrenderable after exact XOR80 preparation.");
    }
}
