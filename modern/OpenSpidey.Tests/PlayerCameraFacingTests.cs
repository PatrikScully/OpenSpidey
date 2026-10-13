using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerCameraFacingTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerCameraFacingTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-camera-facing.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement row in document.RootElement.EnumerateArray())
        {
            long[] input = row.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            {
                CameraAngleLock = (byte)input[1], Field54C = (byte)input[2], Field8E8 = (byte)input[3], ActionState = (uint)input[4],
                FieldEA6 = (short)input[7], LookaroundActive = input[10] != 0, Field551 = (byte)input[16],
                SwingCameraResetLatch = (byte)input[18], Position = new((int)input[20], (int)input[21], (int)input[22])
            };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Pressed[5] = (byte)input[5]; melee.Input.Triggered[4] = (byte)input[8]; melee.Input.Triggered[17] = (byte)input[9];
            melee.Input.Sideways = (sbyte)input[11]; melee.Input.Forward = (sbyte)input[12]; melee.Input.Mirrors[17] = (byte)input[19];
            var lookaround = new PlayerLookaroundState(combat) { HoldTicks = (int)input[17] };
            var state = new PlayerCameraFacingState(melee, new(melee) { ApplyHookPosition = (byte)input[0] }, lookaround)
            { CameraTarget = input[6] != 0 ? new(new object(), 1) : default };
            var host = new Host(input);
            PlayerCameraFacing.Update(state, host);
            long[] actual = [combat.Field551, lookaround.HoldTicks, combat.SwingCameraResetLatch,
                melee.Input.Triggered[4], melee.Input.Triggered[17], melee.Input.Mirrors[17]];
            JsonElement expected = row.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
                $"Original camera-facing {index}: actual shared551/201/56C and native4/17 edges/mirrors.");
            long[] wanted = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wanted), $"Original complete camera-facing {index++}: genuine E70 recovery/Utils_CalcAim, source heading ranges and exact interpolation durations; actual[{string.Join(',',host.Events)}], expected[{string.Join(',',wanted)}].");
        }
    }

    private sealed class Host(long[] input) : IPlayerCameraFacingHost
    {
        public List<long> Events { get; } = [];
        public short CameraHeading => (short)input[14];
        public short CameraReferenceHeading => (short)input[15];
        public short InputHeading => (short)input[13];
        public short GetEffectiveHeading() { Events.Add(3); return (short)input[26]; }
        public FixedVector3? RecoverCameraTarget(ref PlayerKickHandle handle)
        { Events.Add(4); return new((int)input[23], (int)input[24], (int)input[25]); }
        public void PutCameraBehind(int duration) => Events.AddRange([1, duration]);
        public void SetCameraAngle(short heading, ushort duration) => Events.AddRange([2, heading, duration]);
    }
}
