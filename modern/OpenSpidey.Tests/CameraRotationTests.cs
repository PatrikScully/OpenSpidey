using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraRotationTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(CameraRotationTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.camera-rotations.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var rotation = new FixedCameraRotation();
            rotation.Set(Quaternion(test.GetProperty("current")), Quaternion(test.GetProperty("target")),
                (ushort)test.GetProperty("duration").GetInt32());
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                rotation.Advance(test.GetProperty("ticks").GetInt32());
                assert(rotation.Value == Quaternion(expected) && rotation.Desired == Quaternion(expected, 4) &&
                    rotation.RemainingTicks == expected[8].GetInt32(),
                    "Scripted camera quaternion, damped view and signed timer match recovered updates.");
            }
        }
    }

    private static FixedQuaternion Quaternion(JsonElement value, int offset = 0) => new(value[offset].GetInt32(),
        value[offset + 1].GetInt32(), value[offset + 2].GetInt32(), value[offset + 3].GetInt32());
}
