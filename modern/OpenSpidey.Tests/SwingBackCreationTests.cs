using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class SwingBackCreationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(SwingBackCreationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.swing-back-creation.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            FixedVector3 Vector(int offset) => new(input[offset], input[offset + 1], input[offset + 2]);
            int cursor = 0; var events = new List<long>();
            int Random(int bound) { int result = (cursor++ * 7 + 1) % bound; events.AddRange([1, bound, result]); return result; }
            var held = new KnottedWebStrand(Vector(0), Vector(3), Random); held.SetEndpoints(Vector(0), Vector(3));
            FixedVector3 offset = Vector(6) - held.Ends[^1]; held.SetEndpoints(Vector(0), Vector(6)); events.Clear();
            using ReleasedSwingWeb? released = ReleasedSwingWeb.Create(held, offset, input[10], input[9] != 0, new(500, 300, -700), Random);
            var actual = new List<long> { released is null ? 0 : 1 };
            void Append(FixedVector3 value) => actual.AddRange([value.X, value.Y, value.Z]);
            Append(offset); Append(held.Ends[^1]);
            if (released is not null)
            {
                KnottedWebStrand strand = released.Strand;
                Append(held.Start); Append(ReleasedSwingWeb.CreationEndpoint(held, offset));
                actual.AddRange([strand.SegmentCount, strand.FlickerKnots, strand.WobbleKnots, strand.FireVariant,
                    strand.StartColour >> 24, strand.InnerStartColour >> 24]);
                Append(strand.Start); Append(strand.Side);
                for (int i = 0; i < strand.SegmentCount; i++)
                { Append(strand.Ends[i]); Append(strand.Knots[i].RestPosition); actual.Add(strand.Colours[i] >> 24); }
                foreach (uint colour in strand.InnerColours) actual.Add(colour >> 24);
            }
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Count).Select(value => value.GetInt64())),
                $"Whole original PC SwingBack {index}: same G_LOW_MEMORY gate, lastEndpoint PLUS renderOffset, source constructor and paired code2/wobble flags.");
            assert(events.SequenceEqual(expected[actual.Count].EnumerateArray().Select(value => value.GetInt64())),
                $"Whole original PC SwingBack {index++}: low-memory suppresses all allocation/random and ordinary construction keeps original random stream position.");
            held.Dispose();
        }
    }
}
