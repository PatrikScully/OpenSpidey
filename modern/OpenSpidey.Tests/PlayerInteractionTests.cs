using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerInteractionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using JsonDocument document = JsonDocument.Parse(typeof(PlayerInteractionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-interaction.json")!);
        assert(document.RootElement.GetProperty("sourceActionSlicesVerified").GetBoolean() && document.RootElement.GetProperty("originalPcHardThrowDivisionVerified").GetBoolean(),
            "Player interaction gold requires compiled original slices and independent PC hard-throw division evidence.");
        int index = 0;
        foreach (JsonElement test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { FieldEA6 = 77 };
            var state = new PlayerKickState(new(combat));
            var host = new Host();
            FixedVector3 Vector(int offset) => new((int)input[offset], (int)input[offset + 1], (int)input[offset + 2]);
            long[] actual;
            if (input[0] == 1)
            {
                combat.Position = Vector(1); combat.ForwardDirection = Vector(10);
                state.HeldObject = new(host.Identity, 401, default, 0);
                state.Melee.HeadingDelta = (int)input[15]; host.Pose = new((short)input[13], (short)input[14]);
                PlayerInteractionActions.UpdateHeldPose(state, Vector(4), Vector(7), host);
                actual = host.Events.ToArray();
            }
            else
            {
                combat.ActionState = input[0] switch { 0 => 0x100000, 2 => 0x200000, _ => 0x80000000 };
                combat.AnimationId = (ushort)input[1]; combat.AnimationFrame = (int)input[2];
                state.Melee.AnimationFinished = input[3] != 0;
                host.Item = new(host.Identity, 401, Vector(12), 0); host.Valid = input[5] != 0;
                state.HeldObject = input[4] != 0 ? host.Item : null;
                state.ManipulationTarget = new(1, 123); state.SwitchTarget = new(2, 456);
                state.SwitchActionProgress = (int)input[6];
                state.TorsoTarget = input[8] != 0 ? new(new object(), 304, Vector(15), 0) : null;
                combat.Position = Vector(9); combat.ForwardDirection = Vector(18); combat.UpDirection = Vector(21);
                bool handled = PlayerInteractionActions.Update(state, (ushort)input[7], host);
                assert(handled, "Selected interaction states must have a genuine dispatcher.");
                // Independent original PC instruction probe in fixture validates this raw denominator before source minimum-speed clamp.
                FixedVector3 target = Vector(15);
                int distance = Distance(combat.Position with { Y = target.Y }, target);
                actual = [combat.FieldEA6, state.HeldObject is not null ? 1 : 0, state.SwitchActionProgress,
                    unchecked(distance << 12) / 24, state.ManipulationTarget.Target is null ? 0 : 1, state.ManipulationTarget.Validation,
                    state.SwitchTarget.Target is null ? 0 : 2, state.SwitchTarget.Validation, ..host.Events];
            }
            long[] expected = test.GetProperty("events").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(expected), $"Player interaction source{index}, input[{string.Join(',', input)}]: actual[{string.Join(',', actual)}] expected[{string.Join(',', expected)}].");
            index++;
        }
    }
    private static int Distance(FixedVector3 a, FixedVector3 b)
    {
        int x = GameWord.Absolute(unchecked(a.X - b.X)), y = GameWord.Absolute(unchecked(a.Y - b.Y)), z = GameWord.Absolute(unchecked(a.Z - b.Z));
        if (x < y) (x, y) = (y, x); if (y < z) (y, z) = (z, y); if (x < y) (x, y) = (y, x);
        return unchecked(x + (y >> 1) + (z >> 2)) >> 12;
    }
    private sealed class Host : IPlayerInteractionHost
    {
        public object Identity { get; } = new(); public bool Valid; public PlayerKickObject Item = null!; public PlayerHeldPose Pose;
        public List<long> Events { get; } = [];
        public PlayerKickObject? RecoverManipulation(ref PlayerKickHandle handle) { Events.AddRange([1, 1, handle.Validation]); if (Valid) return Item; handle = handle with { Target = null }; return null; }
        public object? RecoverSwitch(ref PlayerKickHandle handle) { Events.AddRange([2, 2, handle.Validation]); if (Valid) return Identity; handle = handle with { Target = null }; return null; }
        public void Hold(PlayerKickObject item) => Events.AddRange([5, 1]);
        public void Pickup(object identity) => Events.AddRange([6, 1]);
        public void ThrowVelocity(object identity, FixedVector3 velocity) => Events.AddRange([10, 1, velocity.X, velocity.Y, velocity.Z]);
        public void ThrowAt(object identity, FixedVector3 target, int count) => Events.AddRange([11, 1, target.X, target.Y, target.Z, count]);
        public void Release(object identity) => Events.AddRange([12, 1]);
        public void FlickSwitch(object identity) => Events.AddRange([13, 2]);
        public void SwitchToStandMode() => Events.Add(7);
        public void CheckJump() => Events.Add(8);
        public void PutCameraBehind(int mode) => Events.AddRange([9, mode]);
        public void PlaySound(ushort sound, int volume, int pitch) => Events.AddRange([4, sound, volume, pitch]);
        public void PlayPositionalSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([3, sound, position.X, position.Y, position.Z, pitch]);
        public PlayerHeldPose ReadHeldPose(object identity) => Pose;
        public void SetHeldPose(object identity, FixedVector3 position, short yaw) => Events.AddRange([position.X, position.Y, position.Z, yaw]);
    }
}
