using System.Text.Json;
using OpenSpidey.Core.Actors;

internal static class ActorFrameTimingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorFrameTimingTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.actor-frame-timing.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            long[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var timing = new ActorFrameTiming
            {
                LastUpdateTick = (uint)input[1], AccumulatedTicks = (int)input[2], PreviousFrame = (short)input[8],
                BeforeUpdateAnimation = new((short)input[9], (ushort)input[10], (sbyte)input[11])
            };
            ushort flags = timing.Prepare((uint)input[0], (ushort)input[3], (ushort)input[4],
                new((short)input[5], (ushort)input[6], (sbyte)input[7]));
            long[] actual = [timing.LastUpdateTick, timing.ElapsedTicks, timing.AccumulatedTicks, flags,
                timing.PreviousFrame, timing.BeforeUpdateAnimation.Frame, timing.BeforeUpdateAnimation.Animation,
                timing.BeforeUpdateAnimation.Direction, (input[3] & 4) == 0 ? 1 : 0, timing.TimingError ? 1 : 0];
            assert(actual.SequenceEqual(expected), "CBody frame timing must match the original executable, including untouched caches and signed wraparound.");
        }
    }
}
