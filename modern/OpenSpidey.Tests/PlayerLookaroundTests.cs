using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerLookaroundTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerLookaroundTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-lookaround.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            JsonElement expected = test.GetProperty("expected");
            if (test.GetProperty("kind").GetInt32() == 0) VerifyInput(input, expected, assert);
            else VerifyAngles(input, expected, assert);
        }
    }

    private static void VerifyInput(long[] input, JsonElement expected, Action<bool, string> assert)
    {
        var combat = new PlayerCombatState(100, 100) { ActionState = (uint)input[12], ElapsedTicks = (ushort)input[18], Field8E9 = (byte)input[24] };
        var melee = new PlayerMeleeState(combat);
        melee.Input.Sideways = (sbyte)input[15]; melee.Input.Forward = (sbyte)input[16];
        melee.Input.Mirrors[16] = 77; melee.Input.Mirrors[7] = 88;
        var kick = new PlayerKickState(melee) { HeldObject = input[13] != 0 ? new(new(), 401, default, 0) : null };
        var web = new PlayerWebAttackState { ButtonLatched = (byte)input[19], BlockedVariants = (uint)input[11] };
        var state = new PlayerLookaroundState(combat) { Active = input[5] != 0, Allowed = (byte)input[6],
            SwingHeldLatch = (byte)input[7], JumpReleased = (byte)input[8], ActivateTarget = (byte)input[9], HoldTicks = (int)input[10],
            EntranceTicks = (int)input[20], ExitTicks = (int)input[21], Pitch = (int)input[22], Yaw = (int)input[23],
            SmoothedPitch = 333, SmoothedYaw = -444 };
        var host = new InputHost(state, (short)input[25]);
        PlayerLookaroundButtons buttons = PlayerLookaroundInput.Step(state, web, kick,
            new(new((byte)input[0], (byte)input[1], (byte)input[2], (byte)input[3], (byte)input[4]), (int)input[17],
                (byte)input[14], (ushort)input[26], input[27] != 0, input[28] != 0, input[29] != 0), host);
        long[] actual = [buttons.LookHeld, buttons.SwingHeld, buttons.SwingTriggered, buttons.JumpHeld, buttons.JumpTriggered,
            state.Active ? 1 : 0, state.SwingHeldLatch, state.JumpReleased, state.ActivateTarget, state.HoldTicks, web.BlockedVariants,
            state.EntranceTicks, state.ExitTicks, state.Pitch, state.Yaw, state.SmoothedPitch, state.SmoothedYaw,
            melee.Input.Mirrors[16], melee.Input.Mirrors[7]];
        long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
        wanted[5] = wanted[5] != 0 ? 1 : 0;
        assert(actual.SequenceEqual(wanted), $"Original PC lookaround input[{string.Join(',', input)}]: raw button slots, DWORD direction releases, hold15 boundary, camera admission, wrapped pitch/yaw and joint activation mirrors.");
        assert(host.Events.SequenceEqual(expected[actual.Length].EnumerateArray().Select(value => value.GetInt64())),
            "Original PC lookaround input retains exact enter/exit, heading evaluation and excess-yaw torso targeting call order.");
    }

    private static void VerifyAngles(long[] input, JsonElement expected, Action<bool, string> assert)
    {
        var combat = new PlayerCombatState(100, 100) { HeadingTurnTicksRemaining = (int)input[15] };
        var state = new PlayerLookaroundState(combat) { Active = input[0] != 0, EntranceTicks = (int)input[1], ExitTicks = (int)input[2],
            PreviousHeading = (short)input[4], Pitch = (int)input[7], Yaw = (int)input[8], SmoothedPitch = (int)input[9], SmoothedYaw = (int)input[10],
            NeckPitch = (short)input[11], NeckYaw = (short)input[12], HeadPitch = (short)input[13], HeadYaw = (short)input[14], TransitionFade = (int)input[16] };
        bool advanced = PlayerLookaroundAngles.Advance(state, (short)input[5], input[6] != 0, (int)input[3]);
        long[] actual = [state.Active ? 1 : 0, state.EntranceTicks, state.ExitTicks, state.PreviousHeading, state.Pitch, state.Yaw,
            state.SmoothedPitch, state.SmoothedYaw, state.NeckPitch, state.NeckYaw, state.HeadPitch, state.HeadYaw,
            state.HeadingTurnTicksRemaining, state.TransitionFade, state.TransitionFade];
        assert(actual.SequenceEqual(expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64())),
            $"Original PC scalar SetupLookaroundCamera[{string.Join(',', input)}]: transition counters/fade, heading short wrap512, ceiling compensation, genuine joints1/3, signed decay and smoothing192 dead zone.");
        assert(advanced == state.Active, "The scalar camera-angle slice stops when lookaround is inactive or its exit teardown finishes.");
        assert(expected[actual.Length].GetArrayLength() == (advanced ? 1 : 0), "Original PC evaluates the effective heading only for the active scalar camera-angle slice.");
    }

    private sealed class InputHost(PlayerLookaroundState state, short heading) : IPlayerLookaroundInputHost
    {
        public List<long> Events { get; } = [];
        public short EffectiveHeading { get { Events.Add(3); return heading; } }
        public void EnterLookaroundMode() { Events.Add(1); state.InitializeAnglesOnEnter(heading, false, false); }
        public void ExitLookaroundMode() { Events.Add(2); state.ClearAnglesOnExit(); }
        public void SetTargetTorsoAngle(short wanted, bool fast) => Events.AddRange([4, wanted, fast ? 1 : 0]);
    }
}
