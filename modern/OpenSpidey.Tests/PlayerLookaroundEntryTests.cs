using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerLookaroundEntryTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLookaroundEntryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-lookaround-entry.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            var combat = new PlayerCombatState(100, 100) { HeadingTurnTicksRemaining = 77 };
            var angles = new PlayerLookaroundState(combat) { PreviousHeading = 888, ExitTicks = input[20],
                FloorPitch = input[21], WallPitch = input[22], CeilingPitch = input[23], Pitch = input[24] };
            var transition = new PlayerLookaroundTransitionState(angles);
            var host = new Host(input);
            bool result = PlayerLookaroundTransition.Enter(transition, new FixedQuaternion(input[0], input[1], input[2], input[3]).ToMatrix(),
                new(input[8], input[9], input[10]), input[18] != 0, input[19] != 0, host);
            int[] actual = [angles.Active ? 1 : 0, angles.EntranceTicks, angles.ExitTicks, angles.HeadingTurnTicksRemaining,
                angles.PreviousHeading, angles.Pitch, transition.Offset.X, transition.Offset.Y, transition.Offset.Z,
                transition.Anchor.X, transition.Anchor.Y, transition.Anchor.Z,
                transition.EntryCameraPosition.X, transition.EntryCameraPosition.Y, transition.EntryCameraPosition.Z,
                transition.BodyRotation.X, transition.BodyRotation.Y, transition.BodyRotation.Z, transition.BodyRotation.W,
                transition.CameraRotation.X, transition.CameraRotation.Y, transition.CameraRotation.Z, transition.CameraRotation.W];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt32())),
                $"Actual PC lookaround entry[{string.Join(',', input)}]: original transformed camera/body quaternions, wrapping up128 offset, genuine hook8 anchor, pitch surface priority and entrance24 state.");
            assert(result == (input[20] == 0), "Actual source exit-in-progress guard must return before every lookaround entry callback.");
            JsonElement path = expected[actual.Length];
            assert((transition.Path?.Length ?? 0) == path.GetArrayLength(), "Actual PC entry allocates precisely24 quaternion samples only after mode7 activation.");
            if (transition.Path is { } samples)
                for (int index = 0; index < samples.Length; index++)
                    assert(new[] { samples[index].X, samples[index].Y, samples[index].Z, samples[index].W }
                        .SequenceEqual(path[index].EnumerateArray().Select(value => value.GetInt32())),
                        $"Original PC sample{index} follows camera-to-body order and retains the source Slerp start sign mutation.");
            assert(host.Events.SequenceEqual(expected[actual.Length + 1].EnumerateArray().Select(value => value.GetInt32())),
                "Actual PC lookaround entry preserves heading, camera position, hook8, PushMode, SetMode7 and path allocation boundary order.");
        }
    }

    private sealed class Host(int[] input) : IPlayerLookaroundTransitionHost
    {
        public List<int> Events { get; } = [];
        public short EffectiveHeading { get { Events.Add(1); return (short)input[17]; } }
        public FixedQuaternion CameraRotation => new(input[4], input[5], input[6], input[7]);
        public FixedVector3 CameraPosition { get { Events.Add(2); return new(input[14], input[15], input[16]); } }
        public FixedVector3 GetPlayerHookPosition(int hook) { Events.AddRange([3, hook]); return new(input[11], input[12], input[13]); }
        public void PushCameraMode() => Events.Add(4);
        public void SetCameraMode(ushort mode) => Events.AddRange([5, mode]);
        public FixedQuaternion[] AllocateTransitionPath(int count) { Events.AddRange([6, count * 16, 0, 1, 0, 1]); return new FixedQuaternion[count]; }
    }
}
