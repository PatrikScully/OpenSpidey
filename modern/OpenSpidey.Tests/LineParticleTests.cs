using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class LineParticleTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(LineParticleTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.line-particle.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            uint clock = unchecked((uint)input[8]);
            var particle = new LineParticleMotion(new(input[0], input[1], input[2]), new(input[3], input[4], input[5]),
                (ushort)input[7], input[6], clock);
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                clock = unchecked(clock + (uint)input[9]);
                particle.Advance(clock);
                long[] actual = [particle.Head.X, particle.Head.Y, particle.Head.Z,
                    particle.Tail.X, particle.Tail.Y, particle.Tail.Z, particle.Velocity.X, particle.Velocity.Y, particle.Velocity.Z,
                    particle.RemainingTicks, particle.LastClock, particle.Alive ? 1 : 0];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    $"Line-particle trail, motion and lifetime match original clock delta {input[9]}.");
            }
        }
    }
}
