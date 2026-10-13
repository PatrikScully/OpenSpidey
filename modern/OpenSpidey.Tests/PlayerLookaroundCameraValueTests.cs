using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.Player;

internal static class PlayerLookaroundCameraValueTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLookaroundCameraValueTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-lookaround-camera-value.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            int[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var state = new PlayerLookaroundState(new(100, 100)) { FloorPitch = input[3], WallPitch = input[4], CeilingPitch = input[5], Pitch = input[6] };
            var handler = new PlayerLookaroundCameraValueCommandHandler(() => state);
            byte[] arguments = new byte[6];
            BinaryPrimitives.WriteUInt16LittleEndian(arguments, (ushort)input[0]);
            BinaryPrimitives.WriteUInt16LittleEndian(arguments.AsSpan(2), (ushort)input[1]);
            BinaryPrimitives.WriteInt16LittleEndian(arguments.AsSpan(4), (short)input[2]);
            handler.Execute(new(new TriggerNode(0, 4, 0, ReadOnlyMemory<byte>.Empty), 0), new(183, 0, arguments));
            int[] actual = [state.FloorPitch, state.WallPitch, state.CeilingPitch, state.Pitch];
            assert(actual.SequenceEqual(row.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32())),
                $"Original lookaround camera value{index++}: exact signed surface profile write, rejected parameter/unknown surface and preserved currently active pitch.");
        }
        foreach (bool present in new[] { false, true })
        {
            var state = new PlayerLookaroundState(new(100, 100));
            var handler = new PlayerLookaroundCameraValueCommandHandler(() => present ? state : throw new InvalidOperationException("Missing player must not be recovered."), () => present);
            byte[] commands = [4, 0, 183, 0, 0, 0, 0, 0, 100, 0, 183, 0, 2, 0, 0, 0, 224, 252, 255, 255];
            var runtime = new MissionRuntime(new([new(0, 4, 0, commands)]), new MissionRuntimeTests.ActorFactory(), [handler]);
            runtime.QueueAutoexec();
            assert(runtime.ExecutePending() == 1 && state.FloorPitch == (present ? 100 : 170) &&
                state.CeilingPitch == (present ? -800 : 170) && state.WallPitch == 170 && state.Pitch == 0,
                "Source183 consumes all three words with or without a live player and preserves signed authored floor100/ceiling−800 without changing current setup pitch.");
            state.InitializeAnglesOnEnter(123, false, false);
            assert(state.Pitch == state.FloorPitch, "Genuine floor entry consumes the same initial pitch owner written by183.");
            state.ClearAnglesOnExit(); state.InitializeAnglesOnEnter(456, false, true);
            assert(state.Pitch == state.CeilingPitch, "Genuine ceiling entry consumes raw signed−800 from183 rather than a normalized or copied profile.");
        }
    }
}
