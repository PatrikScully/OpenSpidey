using System.Text.Json;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Mathematics;

internal static class ScorpionTailPathTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ScorpionTailPathTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.scorpion-tail-path.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.GetProperty("curves").EnumerateArray())
        {
            FixedVector3[] controls = Vectors(row.GetProperty("controls"));
            var samples = new FixedVector3[row.GetProperty("count").GetInt32()];
            ScorpionTailPath.SampleCubic(controls, samples);
            assert(samples.SequenceEqual(Vectors(row.GetProperty("expected"))),
                $"Whole original489660 tail curve{index++} preserves copied Q12 endpoints, truncated step4096/(count-1), four integer cubic coefficients and wrapping GPF products.");
        }
        index = 0;
        foreach (JsonElement row in document.RootElement.GetProperty("updates").EnumerateArray())
        {
            FixedVector3[] hooks = Vectors(row.GetProperty("hooks")), controls = Vectors(row.GetProperty("controls")),
                nodes = Vectors(row.GetProperty("nodes")), history = Vectors(row.GetProperty("history"));
            var events = new List<long>();
            ScorpionTailPath.Update(controls, nodes, history, part =>
            {
                events.Add(part);
                events.AddRange(Numbers(controls));
                events.AddRange(Numbers(nodes));
                events.AddRange(Numbers(history));
                return hooks[part - 17];
            });
            JsonElement expected = row.GetProperty("expected");
            assert(controls.SequenceEqual(Vectors(expected.GetProperty("controls"))) && nodes.SequenceEqual(Vectors(expected.GetProperty("nodes"))) &&
                history.SequenceEqual(Vectors(expected.GetProperty("history"))),
                $"Whole original488370 tail update{index} preserves seven genuine hook reads, Q12-to-whole truncation, both overlapping twelve-point curves and all128 sparse history vectors.");
            assert(events.SequenceEqual(expected.GetProperty("events").EnumerateArray().Select(value => value.GetInt64())),
                $"Whole original488370 tail update{index++} exposes the exact control/node/history state before every source hook callback, including first-curve completion before part21.");
            assert(history[23] == nodes[0] && history[0] == nodes[0],
                "The actual24-vector history copy reads its final source through the node23/history0 alias after iteration0 has already published the new first tail node.");
        }
    }

    private static FixedVector3[] Vectors(JsonElement values) => values.EnumerateArray()
        .Select(value => new FixedVector3(value[0].GetInt32(), value[1].GetInt32(), value[2].GetInt32())).ToArray();
    private static IEnumerable<long> Numbers(IEnumerable<FixedVector3> values) => values.SelectMany(value => new long[] { value.X, value.Y, value.Z });
}
