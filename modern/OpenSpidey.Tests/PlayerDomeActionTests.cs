using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerDomeActionTests
{
    private sealed class Host(int[] input) : IPlayerWebDomeActionHost
    {
        public uint Clock => unchecked((uint)input[2]);
        public ushort Radius { get; private set; } = 100;
        public ushort MinorRadius { set => Radius = value; }
        public List<long> Events { get; } = [];
        public void PlayAnimation(ushort animation, int firstFrame, int lastFrame) => Events.AddRange([1, animation, firstFrame, lastFrame]);
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitch) => Events.AddRange([2, sound, position.X, position.Y, position.Z, pitch]);
        public void SwitchToStandMode() => Events.Add(6);
        public bool BurstDome(ref PlayerKickHandle handle)
        {
            Events.Add(3);
            if (input[8] == 0) { handle = handle with { Target = null }; return false; }
            Events.AddRange([4, 5]); return true;
        }
    }
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerDomeActionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-dome-action.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => unchecked((int)value.GetInt64())).ToArray();
            var combat = new PlayerCombatState(100, 100) { AnimationId = (ushort)input[0], Position = new(12345, -54321, 67890) };
            var melee = new PlayerMeleeState(combat);
            melee.Input.Pressed[18] = (byte)input[4]; melee.Input.Pressed[19] = (byte)input[5]; melee.Input.Pressed[16] = (byte)input[6];
            var state = new PlayerWebAttackState { DomeCreationClock = input[3], DomeHandle = new(input[7] == 0 ? null : new object(), 999) };
            var host = new Host(input);
            PlayerWebDomeAction.Step(melee, state, (ushort)input[1], host);
            JsonElement expected = test.GetProperty("expected");
            long[] actual = [host.Radius, state.DomeHandle.Target is null ? 0 : 1, state.DomeHandle.Validation];
            assert(actual.SequenceEqual(expected.EnumerateArray().Take(3).Select(value => value.GetInt64())),
                $"Player dome action source case {index}: source radius150/100, strict150 timer and complete/expired handle ownership.");
            assert(host.Events.SequenceEqual(expected[3].EnumerateArray().Select(value => value.GetInt64())),
                $"Player dome action source case {index}: jump16/kick18/punch19 release, original clip/sound and burst/clear order.");
            index++;
        }
    }
}
