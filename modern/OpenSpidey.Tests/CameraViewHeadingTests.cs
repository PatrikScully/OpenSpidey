using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraViewHeadingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(CameraViewHeadingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-view-heading.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var state = new CameraModeState { ViewHeading = (short)input[4] };
            state.LoadViewHeading(new FixedQuaternion(input[0], input[1], input[2], input[3]).ToMatrix());
            assert(state.ViewHeading == test.GetProperty("expected").GetInt16(),
                "Original PC LoadIntoMikeCamera heading23A uses the exact final display quaternion matrix, retaining prior heading for a zero XZ column; it is independent of focus-aim mAngles.Y.");
        }
    }
}
