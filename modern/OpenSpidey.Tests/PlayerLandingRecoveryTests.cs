using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerLandingRecoveryTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLandingRecoveryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-landing-recovery.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { AnimationId = (ushort)input[0], FieldE8C = (byte)input[2], CollisionFlags = (ushort)input[7],
                FieldAE4 = (byte)input[9], FieldAE5 = (byte)input[10], ActionState = (uint)input[12], FieldEA6 = 456 };
            var melee = new PlayerMeleeState(combat); melee.Input.Pressed[16] = (byte)input[3];
            var lookaround = new PlayerLookaroundState(combat) { JumpReleased = (byte)input[11] };
            var state = new PlayerLandingRecoveryState(melee, lookaround) { Jump = new(123456, (int)input[6], 987654), SpeedRamp = 999 };
            var host = new Host(combat, input);
            bool windup = test.GetProperty("kind").GetString() == "windup";
            if (windup) PlayerLandingRecoveryBehavior.StepWindup(state, input[1] != 0, host);
            else PlayerLandingRecoveryBehavior.Step(state, (ushort)input[1], host);
            long[] actual = [combat.FieldEA6, combat.FieldAE4, combat.FieldAE5, combat.FieldE8C, lookaround.JumpReleased, combat.ActionState,
                combat.AnimationId, state.Jump.Velocity, state.Jump.Phase, state.Jump.Hold, state.SpeedRamp, melee.Input.Pressed[16]];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC {(windup ? "64windup" : "8recovery")}[{string.Join(',', input)}] protected clip, shared AE4/E8C/E8D bytes, support impulse and repeat-jump state; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC {(windup ? "64windup" : "8recovery")}[{string.Join(',', input)}] every source guard/animation/stand/random callback in precise priority; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
        }
    }

    private sealed class Host(PlayerCombatState combat, long[] input) : IPlayerLandingRecoveryHost
    {
        public List<long> Events { get; } = [];
        public int? SupportVerticalVelocity => input[4] != 0 ? (int)input[5] : null;
        private bool Guard(int index) { Events.AddRange([1, index]); return input[8] == index; }
        public bool CheckGroundGone() => Guard(1);
        public bool CheckForwards(bool allowBackwards)
        { if (!allowBackwards) throw new InvalidDataException("Action8 source CheckForwards requires true."); return Guard(2); }
        public bool CheckJump() => Guard(3);
        public bool CheckKick() => Guard(4);
        public bool CheckJumpingSwingWeb() => Guard(5);
        public bool CheckJumpingR1ZipWeb() => Guard(6);
        public bool CheckJumpingR2ZipWeb() => Guard(7);
        public int Random(int range) { int result = (int)input[13]; Events.AddRange([4, range, result]); return result; }
        public void PlaySingleAnimation(ushort animation) { Events.AddRange([2, animation, 0, -1]); combat.AnimationId = animation; }
        public void SwitchToStandMode() => Events.Add(3);
    }
}
