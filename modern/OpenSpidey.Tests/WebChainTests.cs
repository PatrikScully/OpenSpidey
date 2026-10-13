using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class WebChainTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(WebChainTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.web-chain.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.ValueKind == JsonValueKind.True ? 1 :
                value.ValueKind == JsonValueKind.False ? 0 : value.GetInt32()).ToArray();
            var origin = new FixedVector3(input[7], input[8], input[9]);
            var step = new FixedVector3(input[10], input[11], input[12]);
            var anchorDelta = new FixedVector3(input[13], input[14], input[15]);
            var chain = new WebChain(Enumerable.Range(0, input[0]).Select(index => origin + step * index).ToArray(),
                input[1], input[2], input[3], input[4]);
            if (input[5] != 0) chain.SetGroundY(input[6]);
            int frame = 0;
            foreach (JsonElement state in test.GetProperty("expected").EnumerateArray())
            {
                if (frame != 0) chain.Move(origin + anchorDelta * (frame % 7));
                long[] actual = [chain.Points.Count, chain.RestLength, chain.RestLengthSquared, chain.Gravity,
                    chain.GroundY, chain.GroundEnabled ? 1 : 0, chain.Friction, chain.Recoil];
                assert(actual.SequenceEqual(state.EnumerateArray().Take(8).Select(value => value.GetInt64())),
                    "Original CChain parameters and explicit floor enablement match PC construction and SetGroundY.");
                long[] points = chain.Points.SelectMany(point => new long[] { point.Position.X, point.Position.Y, point.Position.Z,
                    point.Velocity.X, point.Velocity.Y, point.Velocity.Z, point.PreviousPosition.X, point.PreviousPosition.Y,
                    point.PreviousPosition.Z, point.Anchored ? 1 : 0 }).ToArray();
                assert(points.SequenceEqual(state[8].EnumerateArray().SelectMany(row => row.EnumerateArray().Select(value => value.GetInt64()))),
                    "Actual PC chain integration preserves accumulated gravity, ground clamp, weighted recoil and conditional constraint cursor advancement.");
                frame++;
            }
        }
    }
}
