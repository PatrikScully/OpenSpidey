using System.Text.Json;
using OpenSpidey.Core.Player;

internal static class PlayerForwardSelectionTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerForwardSelectionTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-forward-selection.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100) { LookaroundActive = input[1] != 0, AnimationId = (ushort)input[4], Field8E8 = (byte)input[5],
                HeadingTurnTicksRemaining = (int)input[10], IsCrawling = input[11] != 0, Field551 = (byte)input[14], FieldAD7 = (byte)input[15],
                ActionState = (uint)input[16], FieldEA6 = 456 };
            var host = new Host(combat);
            bool accepted = PlayerForwardSelection.Check(combat, new(input[0] != 0, input[2] != 0 || input[3] != 0, input[13] != 0,
                (ushort)input[8], (short)input[6], (short)input[7], (ushort)input[9], input[12] != 0), host);
            long[] actual = [accepted ? 1 : 0, combat.FieldEA6, combat.ActionState, combat.AnimationId, combat.HeadingTurnTicksRemaining,
                combat.Field551, combat.FieldAD7, host.SoundEntryPresent ? 1 : 0];
            JsonElement expected = test.GetProperty("expected");
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC CheckForwards[{string.Join(',', input)}] exact input/clip gates, roll priority, signed heading, held/crawl start clips and source flag writes; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            long[] wantedEvents = expected[actual.Length].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC CheckForwards[{string.Join(',', input)}] original fast torso target and reset/run-animation boundaries; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
        }
    }
    private sealed class Host(PlayerCombatState combat) : IPlayerForwardSelectionHost
    {
        public List<long> Events { get; } = [];
        public bool SoundEntryPresent { get; private set; } = true;
        public void SetTargetTorsoAngle(short wanted, bool fast) => Events.AddRange([1, wanted, fast ? 1 : 0]);
        public void SelectAndRunAnimation(ushort animation)
        { SoundEntryPresent = false; Events.AddRange([2, animation, 0, -1]); combat.AnimationId = animation; }
    }
}
