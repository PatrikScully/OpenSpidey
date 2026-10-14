using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraFocusMotionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CameraFocusMotionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-focus-motion.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = Values(test.GetProperty("input"));
            var targetPosition = new FixedVector3(input[13], input[14], input[15]);
            var camera = new CameraModeState { FocusPosition = new(input[4], input[5], input[6]) };
            var focus = new CameraFocusMotion(camera)
            {
                RemainingTicks = input[0], Target = input[2] != 0 ? new(new object(), () => targetPosition) : null, TrackingEnabled = input[3] != 0,
                MotionTarget = new(input[7], input[8], input[9]), Rate = new(input[10], input[11], input[12])
            };
            focus.Advance(input[1]);
            int[] actual = [focus.RemainingTicks, camera.FocusPosition.X, camera.FocusPosition.Y, camera.FocusPosition.Z];
            int[] expected = Values(test.GetProperty("expected"));
            assert(actual.SequenceEqual(expected), $"Original PC camera focus case{index++}[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
        }
    }
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
