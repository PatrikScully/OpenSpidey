using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraUserPlacementTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CameraUserPlacementTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-user-placement.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = Values(test.GetProperty("input"));
            var camera = new CameraModeState
            {
                Mode = (uint)input[0], Position = new(input[10], input[11], input[12]),
                Velocity = new(input[25], input[25] + 1, input[25] + 2),
                CurrentRotation = new(input[13], input[14], input[15], input[16]),
                DesiredRotation = new(input[17], input[18], input[19], input[20]),
                SmoothedDistance = input[21], DesiredDistance = input[24], CollisionFlag = 255, Field1A4 = 255
            };
            var projection = new CameraNormalProjectionCache { Distance = input[22] };
            int playerFlags = input[23], diagnostics = 0;
            CameraUserPlacement.Move(camera, projection, new(input[4], input[5], input[6]), new(input[7], input[8], input[9]),
                input[1] != 0, input[2] != 0, input[3] != 0, () => playerFlags |= 8, null,
                message => { if (message == "No valid tripod") diagnostics++; });
            int[] actual = [diagnostics, camera.Position.X, camera.Position.Y, camera.Position.Z,
                camera.Velocity.X, camera.Velocity.Y, camera.Velocity.Z, camera.SmoothedDistance, camera.DesiredDistance,
                .. Quaternion(camera.CurrentRotation), .. Quaternion(camera.PreviousRotation), .. Quaternion(camera.DisplayRotation),
                camera.CollisionFlag, camera.Field1A4, playerFlags];
            int[] expected = Values(test.GetProperty("expected"));
            assert(actual.SequenceEqual(expected), $"Original PC camera mode16/17 placement case{index++}[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
        }
        bool rejected = false;
        try { CameraUserPlacement.Move(new CameraModeState { Mode = 3 }, new(), default, default, true, true, false, () => { }, null); }
        catch (NotSupportedException) { rejected = true; }
        assert(rejected, "The mode16/17 placement leaf refuses modes owned by other source branches.");
    }
    private static int[] Quaternion(FixedQuaternion value) => [value.X, value.Y, value.Z, value.W];
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
