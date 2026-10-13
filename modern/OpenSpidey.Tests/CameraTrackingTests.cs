using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class CameraTrackingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CameraTrackingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-tracking.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            string kind = test.GetProperty("kind").GetString()!;
            if (kind == "heading")
            {
                var heading = new CameraHeadingTransition { Heading = (short)input[1], RemainingTicks = (int)input[4], Target = (int)input[5],
                    Rate = (int)input[6], PublishedHeading = (int)input[7] };
                heading.Set((short)input[2], (ushort)input[3], (uint)input[0]);
                int frame = 0;
                foreach (JsonElement result in expected.EnumerateArray())
                {
                    long[] actual = [heading.Heading, heading.PublishedHeading, heading.RemainingTicks, heading.Target, heading.Rate];
                    Verify(actual, result, $"camera yaw frame{frame++}");
                    heading.Advance((int)input[8], (uint)input[0]);
                }
            }
            else if (kind == "tripod")
            {
                var tripod = new CameraTripodState { Position = Vector(0), TrackingEnabled = input[10] != 0, HookIndex = (int)input[12],
                    RemainingTicks = (int)input[13], Target = Vector(14), Rate = Vector(17), EaseX = (int)input[20], EaseY = (int)input[21], EaseZ = (int)input[22] };
                var hooks = new List<long>();
                tripod.Advance(input[9] == 0 ? null : Vector(3), input[11] != 0, hook => { hooks.Add(hook); return Vector(6); }, (int)input[23]);
                long[] actual = [tripod.RemainingTicks, tripod.Position.X, tripod.Position.Y, tripod.Position.Z];
                Verify(actual, expected, "source posed tripod/easing/timed position", 4);
                Verify(hooks, expected[4], "source posed hook selection only when the tracked player is alive and tripod interpolation is inactive");
            }
            else
            {
                var host = new BehindHost();
                PlayerCameraBehind.Apply(new(input[0] != 0, input[1] != 0, (short)input[2], Vector(3), (uint)input[6],
                    (sbyte)input[7], (sbyte)input[8], (uint)input[9], (short)input[10], (short)input[11]), (int)input[12], host);
                // Effective heading is a genuine source boundary; this host's scalar frame already holds its result.
                if (input[0] == 0 && input[1] == 0) host.Events.Insert(0, 1);
                Verify(host.Events, expected, "whole source PutCameraBehind with lock, surface up, wall profile, signed axes and original call order");
            }

            FixedVector3 Vector(int start) => new((int)input[start], (int)input[start + 1], (int)input[start + 2]);
            void Verify(IEnumerable<long> actualValues, JsonElement result, string operation, int? count = null)
            {
                long[] actual = actualValues.ToArray();
                IEnumerable<JsonElement> wantedValues = result.EnumerateArray();
                if (count is { } length) wantedValues = wantedValues.Take(length);
                long[] wanted = wantedValues.Select(value => value.GetInt64()).ToArray();
                assert(actual.SequenceEqual(wanted), $"Actual PC {kind}[{string.Join(',', input)}] {operation}; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            }
        }
    }

    private sealed class BehindHost : IPlayerCameraBehindHost
    {
        public List<long> Events { get; } = [];
        public void SetCameraAngle(short heading, ushort duration) => Events.AddRange([2, heading, duration]);
        public void SetCameraVerticalDistance(short distance, ushort duration) => Events.AddRange([3, distance, duration]);
    }
}
