using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraTripodMotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CameraTripodMotionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-tripod-motion.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            var state = new CameraTripodState { Position = Vector(0), RemainingTicks = (int)input[9] };
            state.SetMotion(Vector(6), (uint)input[12], input[10] != 0 ? Vector(3) : null, input[11] != 0);
            int[] actual = [state.RemainingTicks, state.Position.X, state.Position.Y, state.Position.Z,
                state.Target.X, state.Target.Y, state.Target.Z, state.Rate.X, state.Rate.Y, state.Rate.Z];
            int[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            assert(actual.SequenceEqual(expected), $"Actual PC SetTripodMotion case{index++}: live-body reset only during an existing interpolation, wrapped duration and signed vector division.");
        }
    }
}
