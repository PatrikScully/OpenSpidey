using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraStartPositionTests
{
    public static void Run(Action<bool, string> assert)
    {
        var initial = new CameraNormalProjectionCache();
        assert(initial.LookAngle == 300 && initial.Distance == 200, "The actual PE normal camera globals begin at300/200 and belong to the application, not individual cameras.");
        using JsonDocument document = JsonDocument.Parse(typeof(CameraStartPositionTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-start-position.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = Values(test.GetProperty("input"));
            var cache = new CameraNormalProjectionCache { LookAngle = input[5], Distance = input[6] };
            var state = new CameraNormalPlacementState
            {
                Pitch = (short)input[2], Heading = (short)input[3], Roll = (short)input[4],
                Focus = new(13, 29, 43), DesiredPosition = new(-13, -29, -43), Position = new(91, 97, 101)
            };
            if (input[0] == 0) state.SetStartPosition(input[1], new(input[7], input[8], input[9]), cache);
            else state.CalculateNormal(cache);
            int[] actual = [state.Heading, state.Focus.X, state.Focus.Y, state.Focus.Z,
                state.DesiredPosition.X, state.DesiredPosition.Y, state.DesiredPosition.Z,
                state.DesiredRotation.X, state.DesiredRotation.Y, state.DesiredRotation.Z, state.DesiredRotation.W,
                state.Position.X, state.Position.Y, state.Position.Z,
                state.Rotation.X, state.Rotation.Y, state.Rotation.Z, state.Rotation.W,
                cache.LookAngle, cache.Distance];
            int[] expected = Values(test.GetProperty("expected"));
            assert(actual.SequenceEqual(expected), $"Original PC camera-start case{index++}[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
        }
    }
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
