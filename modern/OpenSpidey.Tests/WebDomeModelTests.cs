using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class WebDomeModelTests
{
    public static void Run(Action<bool, string> assert)
    {
        using (Stream stream = typeof(WebDomeModelTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-dome-pieces.json")!)
        using (JsonDocument document = JsonDocument.Parse(stream))
        {
            int index = 0;
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                var piece = new WebDomePiece(new(input[0], input[1], input[2]), (ushort)input[3], input[4], input[5]);
                int frame = 0;
                foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
                {
                    if (frame != 0) { if (frame == input[6]) piece.TintStep = input[7]; piece.AdvanceActor(); }
                    long[] actual = [piece.Position.X, piece.Position.Y, piece.Position.Z, piece.ModelIndex,
                        piece.Delay, piece.TintStep, piece.ItemFlags, piece.BodyFlags, piece.Tint];
                    assert(actual.SequenceEqual(expected.EnumerateArray().Take(9).Select(value => value.GetInt64())),
                        $"CDomePiece actualPC case {index} frame {frame}: exact constructor model/delay/flags and sourceRGB4/-10 clamping.");
                    frame++;
                }
                assert(piece.ModelName == (input[5] != 0 ? "firedome" : "webdome3"), "Source CDomePiece selects the actual ordinary/fire bank.");
                index++;
            }
        }
        using (Stream stream = typeof(WebDomeModelTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-dome-rings.json")!)
        using (JsonDocument document = JsonDocument.Parse(stream))
        {
            int index = 0;
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                FixedVector3[] vertices = Enumerable.Range(0, 4).Select(vertex =>
                    new FixedVector3(input[4 + vertex * 3], input[5 + vertex * 3], input[6 + vertex * 3])).ToArray();
                var ring = new WebDomeRing(new(input[0], input[1], input[2]), input[3], vertices);
                int frame = 0;
                foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
                {
                    if (frame != 0 && (ring.BodyFlags & 0x40) == 0) ring.AdvanceActor();
                    long[] actual = [ring.Position.X, ring.Position.Y, ring.Position.Z, ring.FireVariant,
                        ring.ItemFlags, ring.BodyFlags, ring.Tint];
                    assert(actual.SequenceEqual(expected.EnumerateArray().Take(7).Select(value => value.GetInt64())),
                        $"CDomeRing actualPC case {index} frame {frame}: actual ordinary/fire flags, sourceY90 offset andRGB3 death.");
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        FixedVector3 value = ring.Vertices[vertex], direction = ring.Directions[vertex];
                        long[] coordinates = [value.X, value.Y, value.Z, direction.X, direction.Z];
                        assert(coordinates.SequenceEqual(expected[7][vertex].EnumerateArray().Select(value => value.GetInt64())),
                            $"CDomeRing actualPC case {index} frame {frame} vertex {vertex}: exact source squareRoot/100-step and signed short deformation, pairedDC float position.");
                    }
                    frame++;
                }
                assert(vertices.SequenceEqual(Enumerable.Range(0, 4).Select(vertex =>
                    new FixedVector3(input[4 + vertex * 3], input[5 + vertex * 3], input[6 + vertex * 3]))),
                    "Modern ring owns its deformed vertices; the repository's original source mesh remains available for exact singleton replacement.");
                index++;
            }
        }
    }
}
