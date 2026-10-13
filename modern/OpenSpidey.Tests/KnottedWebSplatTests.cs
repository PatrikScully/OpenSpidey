using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class KnottedWebSplatTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(KnottedWebSplatTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.knotted-web-splats.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int roll = (input[6] * 7 + 1) % 4096;
            var state = new WebImpactSplat(new(input[0], input[1], input[2]), new(input[3], input[4], input[5]), roll);
            if (input[7] != 0) state.SetTint(255, 123, 0);
            else state.SetTint(128, 128, 128);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (frame != 0) state.Step();
                var actual = new List<long> { state.Scale, state.Age, state.ColourTint, state.Finished ? 1 : 0 };
                foreach (FixedVector3 corner in state.Corners) actual.AddRange([corner.X, corner.Y, corner.Z]);
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    $"Knotted splat source/PC case {index} frame {frame}: exact source OrientUsing roll, normal offset, growth and per-channel delayed colour fade.");
                frame++;
            }
            index++;
        }
    }
}
