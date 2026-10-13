using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class FireEmberTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(FireEmberTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.fire-embers.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            int cursor = input[4]; var events = new List<int>();
            int Random(int bound)
            { int value = (cursor++ * 7 + 1) % bound; events.AddRange([bound, value]); return value; }
            var state = new FireEmber(new(input[0], input[1], input[2]), input[3], Random);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                if (frame != 0) { events.Clear(); state.AdvanceBits(Random); }
                long[] actual = [state.Position.X, state.Position.Y, state.Position.Z,
                    state.Origin.X, state.Origin.Y, state.Origin.Z, state.Velocity.X, state.Velocity.Y, state.Velocity.Z,
                    state.Scale, state.Amplitude, state.OrbitPhase, state.AmplitudePhase, state.FadeDelay,
                    state.Red, state.Green, state.Blue, state.CodeRgb, state.Alive ? 0 : 1];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(19).Select(value => value.GetInt64())),
                    $"CEmber source/PC case {index} frame {frame}: exact signed spiral, scale-derived rise/delay,RGB15 decay and source tint.");
                assert(events.SequenceEqual(expected[19].EnumerateArray().Select(value => value.GetInt32())),
                    $"CEmber source/PC case {index} frame {frame}: exact six ctor RNG calls and final dying-update flicker.");
                frame++;
            }
            assert(state.Angle == 0 && state.PostScale == 0x10001000, "CEmber keeps actual CFlatBit angle/postscale defaults.");
            index++;
        }
    }
}
