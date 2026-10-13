using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ReleasedSwingWebTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ReleasedSwingWebTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.released-swing-web.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            FixedVector3 Vector(int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
            int cursor = 0; var events = new List<long>();
            int Random(int bound) { int result = (cursor++ * 7 + 1) % bound; events.AddRange([1, bound, result]); return result; }
            void Fire(FixedVector3 position, int scale, Func<int, int> random)
            { events.AddRange([2, 5, position.X, position.Y, position.Z, scale]); random(7); }
            using var released = new ReleasedSwingWeb(Vector(0), Vector(3), input[9], new(500, 300, -700), Random);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").GetProperty("states").EnumerateArray())
            {
                if (frame != 0) released.Move((uint)(500 + (frame - 1) * 2), Vector(6), new(500, 300, -700), Random, Fire);
                KnottedWebStrand strand = released.Strand;
                var actual = new List<long> { released.Retracting, released.Age, released.Alive ? 0 : 1, strand.SegmentCount };
                void Append(FixedVector3 value) => actual.AddRange([value.X, value.Y, value.Z]);
                Append(strand.Start); Append(strand.Side); actual.AddRange([strand.StartColour, strand.InnerStartColour]);
                for (int i = 0; i < strand.SegmentCount; i++)
                {
                    Append(strand.Ends[i]); actual.Add(strand.Colours[i]); Append(strand.Knots[i].RestPosition);
                    Append(strand.Knots[i].Position); actual.Add(strand.Knots[i].CodeRgb);
                }
                for (int i = 0; i < strand.InnerEnds.Count; i++) { Append(strand.InnerEnds[i]); actual.Add(strand.InnerColours[i]); }
                long[] wanted = expected.EnumerateArray().Select(value => value.GetInt64()).ToArray();
                int mismatch = Enumerable.Range(0, actual.Count).FirstOrDefault(i => actual[i] != wanted[i], -1);
                assert(actual.SequenceEqual(wanted),
                    $"Actual PC released swing {index} frame {frame}: unsigned retraction, source gravity/fade, paired semitransparent lines and age/death; difference {mismatch}: {(mismatch < 0 ? 0 : actual[mismatch])} versus {(mismatch < 0 ? 0 : wanted[mismatch])}.");
                frame++;
            }
            assert(events.SequenceEqual(test.GetProperty("expected").GetProperty("events").EnumerateArray().Select(value => value.GetInt64())),
                $"Actual PC released swing {index++}: original wobble and fire relocation/random order.");
        }
    }
}
