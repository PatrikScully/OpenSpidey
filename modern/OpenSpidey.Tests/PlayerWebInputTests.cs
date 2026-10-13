using System.Text.Json;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWebInputTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerWebInputTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-web-input.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { ActionState = (uint)input[5], IsCrawling = input[9] != 0,
                Field5B4 = unchecked((uint)input[13] - (uint)input[12]), Position = new(123456, -54321, 76543), HeadingTurnTicksRemaining = 77 };
            var melee = new PlayerMeleeState(combat) { WebAttackMode = 7, WebAttackSubstate = 9, WebAttackStarted = 11,
                WebFiringSide = 13, AnimationFlags = 255 };
            melee.Input.Sideways = (sbyte)input[6]; melee.Input.Forward = (sbyte)input[7];
            melee.Input.Pressed[17] = (byte)input[1]; melee.Input.Pressed[18] = (byte)input[18]; melee.Input.Pressed[19] = (byte)input[19];
            var kick = new PlayerKickState(melee) { SurfaceTransition = (byte)input[10],
                HeldObject = input[2] != 0 ? new(new(), 401, default, 0) : null,
                TorsoTarget = input[16] != 0 ? new(new(), 304, new(11, -12, 13), 0) : null };
            var state = new PlayerWebAttackState { ButtonLatched = (byte)input[0], RecoveryFlag = (byte)input[3], BlockedVariants = (uint)input[4] };
            var host = new Host(input, combat);
            bool result = PlayerWebInputSelection.Check(kick, state, host);
            long[] actual = [result ? 1 : 0, state.ButtonLatched, state.RecoveryFlag, state.BlockedVariants,
                melee.WebAttackMode, melee.WebAttackSubstate, melee.WebAttackStarted, melee.WebFiringSide, combat.ActionState,
                combat.HeadingTurnTicksRemaining, melee.AnimationFlags, state.DomeCreationClock,
                state.DomeHandle.Target is null ? 0 : 1, melee.ZipDiveClock, melee.ZipDiveHandle.Target is null ? 0 : 1];
            JsonElement expected = test.GetProperty("expected");
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(15).Select(value => value.GetInt64())),
                $"Actual PC web input[{string.Join(',', input)}]: edge/held latches, DWORD direction mask, signed stick priority, strict cooldown30 and shared action/supply/aim fields.");
            assert(host.Events.SequenceEqual(expected[15].EnumerateArray().Select(value => value.GetInt64())),
                "Actual PC CheckWebShot preserves animation, cost, first-dome clock/sound/constructor/handle, source zip target and full torso heading/point callback order.");
        }
    }
    private sealed class Host(long[] input, PlayerCombatState combat) : IPlayerWebInputHost
    {
        public List<long> Events { get; } = [];
        public uint Clock => (uint)input[13];
        public int MovementRamp => (int)input[8];
        public byte FireWebbing => (byte)input[17];
        public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([1, animation, firstFrame, lastFrame]);
        public bool DecreaseWebbing(int amount) { Events.AddRange([2, amount]); return input[14] != 0; }
        public PlayerKickHandle SelectZipDiveTarget()
        { Events.AddRange([3, 190, -4096, 4096, 0, 8, input[16] != 0 ? 1 : 0]); return input[16] != 0 ? new(this, 555) : default; }
        public PlayerKickHandle CreateDome(byte fireVariant)
        {
            if (input[15] != 0) Events.AddRange([4, fireVariant]);
            Events.AddRange([8, input[15] != 0 ? 1 : 0]); return input[15] != 0 ? new(this, 555) : default;
        }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([5, sound, position.X, position.Y, position.Z, pitch]);
        public void AimTorso(PlayerKickState kick)
        {
            if (kick.SurfaceTransition != 0) Events.AddRange([9, 6, input[11] != 0 ? 1111 : 1357, 0]);
            else if (kick.TorsoTarget is { } target) Events.AddRange([7, target.Position.X, target.Position.Y, target.Position.Z]);
            else combat.HeadingTurnTicksRemaining = 0;
        }
    }
}
