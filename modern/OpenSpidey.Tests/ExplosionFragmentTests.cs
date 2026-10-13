using System.Text.Json;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;

internal static class ExplosionFragmentTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(ExplosionFragmentTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.explosion-fragment.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            uint seed = unchecked((uint)input[13]);
            int Random(int bound)
            {
                seed = unchecked(seed * 1664525u + 1013904223u);
                return bound == 0 ? 0 : (int)(seed % (uint)bound);
            }
            var state = new ExplosionFragment(new(new((int)input[0], (int)input[1], (int)input[2]),
                (byte)input[3], (byte)input[4], (byte)input[5], (int)input[6], (ushort)input[7],
                input[8] != 0, (int)input[9], (int)input[10], (int)input[11]), (int)input[12], Random);
            int index = 0;
            foreach (JsonElement expected in test.GetProperty("states").EnumerateArray())
            {
                if (index++ != 0) state.AdvanceBits();
                long[] actual = [state.Position.X, state.Position.Y, state.Position.Z, state.Velocity.X,
                    state.Velocity.Y, state.Velocity.Z, state.Acceleration.X, state.Acceleration.Y, state.Acceleration.Z,
                    state.Age, state.Lifetime, state.Scale, state.Frame, state.FrameCount, state.CodeRgb,
                    state.PostScale, state.SemiTransparencyRate, state.FrigDeltaZ, state.Alive ? 1 : 0];
                assert(actual.SequenceEqual(expected.EnumerateArray().Select(value => value.GetInt64())),
                    "CFrag position, wrapping velocity/friction, tint, scale, byte frame, age and lifetime match actual PC constructor/motion snapshots.");
                assert(state.DisplayFrame == 0,
                    "Original fragment display retains SetAnim's frame0 pointer even while its independent signed frame counter changes.");
            }
        }
    }
}
