using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerDeathProgressTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerDeathProgressTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-death-progress.json")
            ?? throw new InvalidDataException("Source death-clock fixture is missing.");
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            JsonElement input = test.GetProperty("input"), expected = test.GetProperty("expected");
            var state = new PlayerCombatState(0, 100) { DeathTimer = input[0].GetInt32(),
                ElapsedTicks = input[2].GetInt32(), FieldEA6 = input[3].GetInt16() };
            var host = new Host();
            PlayerDeathProgressRules.Step(state, host, input[1].GetInt32() != 0);
            assert(state.DeathTimer == expected[0].GetInt32() && state.FieldEA6 == expected[1].GetInt16(),
                "Death clock starts only after animation completion, resets before completion and preserves source signed wrapping.");
            assert(host.Events.SequenceEqual(expected[2].EnumerateArray().Select(v => v.GetInt32())),
                "Source death handoff resets initialization and status before flash, testing the previous clock against 120.");
        }
    }
    private sealed class Host : IPlayerDeathProgressHost
    {
        public List<int> Events { get; } = [];
        public void ResetInitializationFlag() => Events.AddRange([1, 0]);
        public void SetLevelStatus(int value) => Events.AddRange([2, value]);
        public void ResetFlash() => Events.Add(3);
    }
}
