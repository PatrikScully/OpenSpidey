using System.Text.Json;
using OpenSpidey.Core.Effects;

internal static class FireSparkTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(FireSparkTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.fire-sparks.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            int cursor = input[7]; var events = new List<int>();
            int Random(int bound)
            { int value = (cursor++ * 7 + 1) % bound; events.AddRange([bound, value]); return value; }
            var state = new FireSpark(new(new(input[0], input[1], input[2]), new(input[3], input[4], input[5]), input[6]), Random);
            assert(events.SequenceEqual(expected[0].EnumerateArray().Select(value => value.GetInt32())),
                $"Fire spark source/PC case {index}: exact point/tile branch constructor RNG.");
            int frame = 0;
            foreach (JsonElement wanted in expected[1].EnumerateArray())
            {
                if (frame != 0) state.AdvanceBits();
                long[] actual = [state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X,
                    state.Velocity.Y, state.Velocity.Z, state.GroundY, state.Tag, state.Code, state.WidthHeight,
                    state.ColourDecay, state.Red, state.Green, state.Blue, state.Alive ? 0 : 1];
                assert(actual.SequenceEqual(wanted.EnumerateArray().Select(value => value.GetInt64())),
                    $"Fire spark source/PC case {index} frame {frame}: source wrapped position, strict ground clamp, horizontal halfspeed, gravity29584 and RGB death.");
                frame++;
            }
            assert(state.DisplayWidth == (state.WidthHeight & 15), "Actual PC pixel rendering uses the packed low nibble for both screen dimensions.");
            index++;
        }
    }
}
