using System.Text.Json;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;

internal static class ScorpionTailGeometryTests
{
    private static readonly ActorHook[] hooks =
    [
        new(0, 368, 464, 2), new(0, 368, 704, 2), new(240, 368, 464, 2), new(0, 368, 224, 2), new(-239, 368, 464, 2),
        new(0, -1, 0, 0), new(120, -1, 0, 0), new(0, -1, -120, 0), new(-119, -1, 0, 0), new(0, -1, 120, 0)
    ];

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionTailGeometryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.scorpion-tail-render.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.GetProperty("renders").EnumerateArray())
        {
            var geometry = new ScorpionTailGeometry();
            Vectors(row.GetProperty("vertices")).CopyTo(geometry.Vertices, 0);
            Vectors(row.GetProperty("normals")).CopyTo(geometry.Normals, 0);
            FixedVector3[] points = Vectors(row.GetProperty("hooks"));
            var events = new List<long>();
            int cursor = 0;
            bool submitted = geometry.Rebuild(row.GetProperty("ready").GetInt32() != 0, Vectors(row.GetProperty("nodes")), hooks, hook =>
            {
                events.AddRange(new long[] { hook.X, hook.Y, hook.Z, hook.PartIndex, geometry.Position.X, geometry.Position.Y, geometry.Position.Z });
                events.AddRange(State(geometry));
                return points[cursor++];
            });
            JsonElement expected = row.GetProperty("expected");
            assert(geometry.Position == Vector(expected.GetProperty("position")) &&
                State(geometry).SequenceEqual(Numbers(expected.GetProperty("geometry"))),
                $"Whole original489810 tail renderer{index} preserves23-ring geometry, all92 signed16 normals/vertices, wrapped signed midpoint, packed bounds, quarter turns and distinct SHR/SAR stores.");
            assert(events.SequenceEqual(Numbers(expected.GetProperty("events"))) &&
                (submitted ? 1 : 0) == expected.GetProperty("submissions").GetInt32() && expected.GetProperty("renderFlag").GetInt32() == 0,
                $"Whole original489810 tail renderer{index++} preserves all ten hook callback identities and visible geometry stores, plus regionFF's complete no-store/no-submit guard.");
        }
        index = 0;
        foreach (JsonElement row in document.RootElement.GetProperty("allocations").EnumerateArray())
        {
            var texture = new DecodedTexture(0x164485E, row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32(), 0, []);
            IReadOnlyList<PsxFace> faces = ScorpionTailMeshLayout.CreateFaces(texture);
            JsonElement expected = row.GetProperty("expected");
            assert(expected.GetProperty("region").GetInt32() == row.GetProperty("occupied").GetInt32() && expected.GetProperty("allocations").GetInt32() == 2 &&
                Numbers(expected.GetProperty("header")).SequenceEqual(new long[] { 8, 92, 92, 88, 32767, 65535, 0x200000 }) &&
                Numbers(expected.GetProperty("item")).SequenceEqual(new long[] { 0x480, 0, 0, 0, 0, 0, 1 }) &&
                Numbers(expected.GetProperty("palette")).All(colour => colour == ScorpionTailMeshLayout.PaletteColour),
                "The source primary-tail initialization establishes its actual private first-free S slot, two allocations,92-normal header, neutral palette and original light/item flags.");
            long[][] actual = faces.Select(face => new long[] { face.Flags, 28, face.VertexIndices[0], face.VertexIndices[1], face.VertexIndices[2], face.VertexIndices[3], face.Color }
                .Concat(face.TextureCoordinates.Select(value => (long)value)).ToArray()).ToArray();
            assert(actual.Length == 88 && actual.SelectMany(value => value).SequenceEqual(expected.GetProperty("faces").EnumerateArray().SelectMany(Numbers)),
                $"Whole original489050 layout{index++} preserves all88 face orders, wrapped fourth-side indices,0x887/28-byte descriptors and quarter atlas byte coordinates, including truncated nondivisible spans.");
        }
    }

    private static IEnumerable<long> State(ScorpionTailGeometry geometry) => new long[] { geometry.BoundsX, geometry.BoundsY, geometry.BoundsZ }
        .Concat(geometry.Vertices.Concat(geometry.Normals).SelectMany(value => new long[] { value.X, value.Y, value.Z }));
    private static FixedVector3 Vector(JsonElement value) => new(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32());
    private static FixedVector3[] Vectors(JsonElement values) => values.EnumerateArray().Select(Vector).ToArray();
    private static IEnumerable<long> Numbers(JsonElement values) => values.EnumerateArray().Select(value => value.GetInt64());
}
