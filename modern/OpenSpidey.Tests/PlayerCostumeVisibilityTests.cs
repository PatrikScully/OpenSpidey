using System.Text.Json;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerCostumeVisibilityTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerCostumeVisibilityTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-costume-visibility.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int cases = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var combat = new PlayerCombatState(100, 100)
            {
                SynthesizedInputActive = input[3] != 0, FieldE18 = (int)input[4], SuitArmorEnabled = (byte)input[5], Position = new(111, 222, 333)
            };
            var host = new Host(input, combat);
            PlayerCostumeVisibility.Update(combat, host);
            JsonElement expected = test.GetProperty("expected");
            long[] actual = [combat.SuitArmorEnabled, host.ItemFlags, host.LeftTwoTriggered, host.Brightness];
            long[] wanted = expected.EnumerateArray().Take(actual.Length).Select(value => value.GetInt64()).ToArray();
            assert(actual.SequenceEqual(wanted), $"Actual PC SpideyAI0 costume[{string.Join(',', input)}] player57C, CItem flag800, pad LeftTwo trigger and570 brightness; actual[{string.Join(',', actual)}], expected[{string.Join(',', wanted)}].");
            assert(expected[4].GetInt64() == 90 && expected[5].GetInt64() == 90, $"Actual PC costume[{string.Join(',', input)}] writes only SControl+0x51.");
            long[] sourceEvents = expected[6].EnumerateArray().Select(value => value.GetInt64()).ToArray();
            assert(sourceEvents.Take(4).SequenceEqual([1, 0x40, 1, 0x100]), $"Actual PC costume[{string.Join(',', input)}] resolves both pure keyboard mappings first.");
            long[] wantedEvents = sourceEvents.Skip(4).ToArray();
            assert(host.Events.SequenceEqual(wantedEvents), $"Actual PC SpideyAI0 costume[{string.Join(',', input)}] keyboard mapping, short-circuit press and SFX_PlayPos order; actual[{string.Join(',', host.Events)}], expected[{string.Join(',', wantedEvents)}].");
            cases++;
        }
        assert(cases == 2446, $"The original PC costume-visibility fixture keeps all 2446 cases, actual {cases}.");
    }

    private sealed class Host(long[] input, PlayerCombatState combat) : IPlayerCostumeVisibilityHost
    {
        public List<long> Events { get; } = [];
        public int CurrentSuit => (int)input[0];
        public ushort ItemFlags { get; set; } = (ushort)input[6];
        public long LeftTwoTriggered { get; private set; } = 90;
        public long Brightness { get; private set; } = input[9];

        public bool KeyboardActionTriggered(uint action)
        {
            long key = action switch { 0x40 => input[7], 0x100 => input[8], _ => throw new InvalidDataException($"Unexpected source action {action:X}.") };
            Events.AddRange([2, key, 1]);
            return (key == input[7] ? input[1] : input[2]) != 0;
        }

        public void ClearLeftTwoTrigger() => LeftTwoTriggered = 0;
        public void ResetBrightness() => Brightness = 0;

        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset)
        {
            if (position != combat.Position) throw new InvalidDataException("SFX_PlayPos receives the player's mPos.");
            Events.AddRange([3, sound, 8, pitchOffset]);
        }
    }
}
