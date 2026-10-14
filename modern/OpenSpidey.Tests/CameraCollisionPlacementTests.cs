using System.Text.Json;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

internal static class CameraCollisionPlacementTests
{
    private sealed class Host(int[][] hits, int[] input) : ICameraPlacementHost
    {
        private int cursor;
        public List<int> Events { get; } = [];
        public int Flags = input[26], HeadingCalls;
        public CameraHeadingTransition Heading { get; } = new()
        {
            Heading = unchecked((short)input[24]), PublishedHeading = input[32], RemainingTicks = input[33]
        };
        public uint Mode { get; init; }

        public CameraLineHit? CameraLineToItem(FixedVector3 start, FixedVector3 end)
        {
            Events.AddRange([start.X, start.Y, start.Z, end.X, end.Y, end.Z]);
            int[] hit = cursor < hits.Length ? hits[cursor] : new int[8];
            cursor++;
            return hit[0] == 0 ? null : new(hit[1], new(hit[2], hit[3], hit[4]),
                unchecked((short)hit[5]), unchecked((short)hit[6]), unchecked((short)hit[7]));
        }
        public bool PlayerActionLocked => input[27] != 0;
        public bool PlayerMoving => (input[28] | input[29]) != 0;
        public bool PlayerHiddenWhenFar => input[30] != 0;
        public short PlayerEffectiveHeading() { HeadingCalls++; return unchecked((short)input[31]); }
        public void HidePlayer() => Flags |= 0x800;
        public void ShowPlayer() => Flags &= ~0x800;
        public void SetCameraAngle(short heading) => Heading.Set(heading, 0, Mode);
    }

    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(CameraCollisionPlacementTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.camera-collision-placement.json")!);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = Values(test.GetProperty("input"));
            int[][] hits = test.GetProperty("hits").EnumerateArray().Select(Values).ToArray();
            var camera = new CameraModeState
            {
                Mode = (uint)input[0], Position = new(input[9], input[10], input[11]),
                Velocity = new(input[35], input[35] + 1, input[35] + 2),
                CollisionPosition = new(input[36], input[36] + 1, input[36] + 2),
                CurrentRotation = new(input[12], input[13], input[14], input[15]),
                DesiredRotation = new(input[16], input[17], input[18], input[19]),
                SmoothedDistance = input[20], DesiredDistance = input[34], CollisionFlag = 255, Field1A4 = 255
            };
            var projection = new CameraNormalProjectionCache { Distance = input[21], LookAngle = input[22] };
            var host = new Host(hits, input) { Mode = camera.Mode };
            int diagnostics = 0;
            CameraPlacement.Move(camera, projection, new(input[3], input[4], input[5]), new(input[6], input[7], input[8]),
                unchecked((short)input[23]), unchecked((short)input[25]), input[1] != 0, input[2] != 0, false, host, null,
                message => { if (message == "No valid tripod") diagnostics++; });
            int[] actual = [diagnostics, camera.Position.X, camera.Position.Y, camera.Position.Z,
                camera.Velocity.X, camera.Velocity.Y, camera.Velocity.Z, camera.SmoothedDistance, camera.DesiredDistance,
                .. Quaternion(camera.CurrentRotation), .. Quaternion(camera.PreviousRotation), .. Quaternion(camera.DisplayRotation),
                camera.CollisionPosition.X, camera.CollisionPosition.Y, camera.CollisionPosition.Z,
                camera.CollisionFlag, camera.Field1A4, host.Flags & 0xFFFF, host.Heading.Heading, host.Heading.PublishedHeading,
                host.Heading.RemainingTicks, host.HeadingCalls];
            JsonElement expectedRow = test.GetProperty("expected");
            int[] expected = expectedRow.EnumerateArray().Take(actual.Length).Select(item => item.GetInt32()).ToArray();
            int[] events = Values(expectedRow[actual.Length]);
            assert(actual.SequenceEqual(expected) && host.Events.SequenceEqual(events),
                $"Original PC camera collision placement case{index}[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] rays[{string.Join(',', host.Events)}] expected[{string.Join(',', expected)}] rays[{string.Join(',', events)}].");
            index++;
        }
        bool rejected = false;
        try { CameraPlacement.Move(new CameraModeState { Mode = 7 }, new(), default, default, 0, 0, true, true, false, new Host([], new int[37]), null); }
        catch (NotSupportedException) { rejected = true; }
        assert(rejected, "The camera placement leaf refuses the rotation-only modes4, 5 and7.");
    }
    private static int[] Quaternion(FixedQuaternion value) => [value.X, value.Y, value.Z, value.W];
    private static int[] Values(JsonElement value) => value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
}
