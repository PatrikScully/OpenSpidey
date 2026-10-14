using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraBossModeTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CameraBossModeTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-boss-mode.json")!);
        object tripodIdentity = new(), bossIdentity = new();
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = Values(test.GetProperty("input"));
            var focusPosition = new FixedVector3(input[5], input[6], input[7]);
            var bossPosition = new FixedVector3(input[8], input[9], input[10]);
            CameraFocusTarget? tripod = input[1] != 0 ? new(tripodIdentity, () => focusPosition) : null;
            CameraFocusTarget? boss = input[0] != 0 ? new(bossIdentity, () => bossPosition) : null;
            var cache = new CameraNormalProjectionCache { LookAngle = input[18], Distance = input[19] };
            var normal = new CameraNormalPlacementState
            {
                Pitch = (short)input[2], Heading = (short)input[3], Roll = (short)input[4],
                Focus = new(1, 2, 3), DesiredPosition = new(-13, -29, -43)
            };
            var focus = new CameraFocusMotion(new CameraModeState());
            var mode = new CameraBossMode { SpinRate = input[11], StationaryRadius = input[12] };
            FixedVector3 offset = mode.Calculate(normal, cache, focus, tripod, boss, focusPosition,
                new(input[13], input[14], input[15]), input[16], input[17]);
            int owner = focus.Target is null ? 0 : ReferenceEquals(focus.Target.Identity, bossIdentity) ? 2 : 1;
            int[] actual = [owner, normal.Heading, normal.Focus.X, normal.Focus.Y, normal.Focus.Z,
                normal.DesiredPosition.X, normal.DesiredPosition.Y, normal.DesiredPosition.Z,
                normal.DesiredRotation.X, normal.DesiredRotation.Y, normal.DesiredRotation.Z, normal.DesiredRotation.W,
                offset.X, offset.Y, offset.Z, cache.LookAngle, cache.Distance];
            int[] expected = Values(test.GetProperty("expected"));
            assert(actual.SequenceEqual(expected), $"Original PC camera boss-mode case{index++}[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
        }
    }
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
