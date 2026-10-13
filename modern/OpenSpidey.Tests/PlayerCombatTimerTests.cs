using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerCombatTimerTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerCombatTimerTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-combat-timers.json")
            ?? throw new InvalidDataException("Player damage timer fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            var state = new PlayerCombatState(100, 100)
            { Field36C = input[0], Field534 = input[1], ElectrificationTicks = input[2], ElapsedTicks = input[3], Electrified = 1 };
            var host = new Host { VibrationEnabled = input[4] != 0 };
            PlayerCombatTimers.Advance(state, host);
            JsonElement expected = test.GetProperty("expected");
            assert(state.Field36C == expected[0].GetInt32() && state.Field534 == expected[1].GetInt32() && state.ElectrificationTicks == expected[2].GetInt32(),
                "Damage protection, feedback and electric timers match source subtraction, clamping and wrapping.");
            assert(host.Events.SequenceEqual(expected[3].EnumerateArray().Select(value => value.GetInt32())) && state.Electrified == 1,
                "Electric expiration and original pad arguments occur in source order without clearing the untouched field_508 flag.");
        }
    }
    private sealed class Host : IPlayerDamageTimerHost
    {
        public bool VibrationEnabled { get; init; }
        public List<int> Events { get; } = [];
        public void ActuatorOn(int controller, ushort time, int argument, int strength) => Events.AddRange([1, controller, time, argument, strength]);
        public void UnElectrify() => Events.Add(2);
    }
}
