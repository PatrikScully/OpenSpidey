using System.Text.Json;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

internal static class PlayerWebInventoryTests
{
    private sealed class Host(long[] input) : IPlayerWebInventoryHost
    {
        public short Health => (short)input[2];
        public int Suit => (int)input[3];
        public uint Clock { get; set; } = unchecked((uint)input[4]);
        public bool SynthesizedInputActive => input[10] != 0;
        public bool ConsumptionDisabled => input[11] != 0;
        public int Difficulty => (int)input[12];
        public bool SpeechBusy => input[13] != 0;
        public FixedVector3 Position => new((int)input[16], (int)input[17], (int)input[18]);
        public List<int> Events { get; } = [];
        public int Random(int range) { Events.AddRange([1, range]); return (int)input[15]; }
        public void PlayPositionSound(ushort sound, FixedVector3 position, int pitchOffset) =>
            Events.AddRange([2, sound, position.X, position.Y, position.Z, pitchOffset]);
        public void PlaySpeech(SpeechRequest request) => Events.AddRange([3, request.Bank, request.Slot, request.Priority]);
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerWebInventoryTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-web-inventory.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var state = new PlayerWebInventory { Webbing = (int)input[5], Cartridges = (int)input[6], FireWebbing = (byte)input[7],
                PickupRevision = (int)input[8], LastCartridgeClock = unchecked((uint)input[9]), EmptyUseOverride = (byte)input[14] };
            var host = new Host(input);
            int frame = 0;
            foreach (JsonElement expected in test.GetProperty("expected").EnumerateArray())
            {
                host.Events.Clear();
                bool result = input[0] switch
                {
                    0 => PlayerWebInventoryRules.Increase(state, (int)input[1], host),
                    1 => PlayerWebInventoryRules.Decrease(state, (int)input[1], host),
                    _ => PlayerWebInventoryRules.SetFireWebbing(state)
                };
                long[] actual = [result ? 1 : 0, state.Webbing, state.Cartridges, state.FireWebbing, state.PickupRevision, state.LastCartridgeClock];
                assert(actual.SequenceEqual(expected.EnumerateArray().Take(6).Select(value => value.GetInt64())) &&
                    host.Events.SequenceEqual(expected[6].EnumerateArray().Select(value => value.GetInt32())),
                    $"Web inventory{index}:{frame++} matches PC resource accounting, wrapped cost, gates, cartridge sound and empty-supply XA request.");
                assert(state.EmptyUseOverride == (byte)input[14], "Web inventory preserves its separate source empty-use override.");
                host.Clock = unchecked(host.Clock + 7);
            }
            index++;
        }

        foreach ((int difficulty, short health, int cartridges) in new (int, short, int)[] { (0, 600, 9), (1, 200, 9), (2, 100, 7), (3, 80, 2) })
        {
            assert(PlayerWebInventoryRules.StartingResources(difficulty, 0, 0x101) == new PlayerStartingResources(health, 4096, cartridges),
                "Original difficulties select their constructor health and cartridge supply.");
            foreach (int suit in new[] { 6, 9, 10 })
            {
                assert(PlayerWebInventoryRules.StartingResources(difficulty, suit, 0x101, 7, 20) == new PlayerStartingResources(health, 7, 2),
                    "Limited source suits clamp restored cartridges after save restore.");
                assert(PlayerWebInventoryRules.StartingResources(difficulty, suit, 0x101, 0, -1) == new PlayerStartingResources(health, 0, -1),
                    "A restored nonzero source cartridge count preserves its raw web fill and signed count.");
            }
        }
        assert(PlayerWebInventoryRules.StartingResources(2, 0, 0x806).Cartridges == 7 &&
            PlayerWebInventoryRules.StartingResources(2, 0, 0x901, 17, 19) == new PlayerStartingResources(100, 17, 2),
            "The source unsigned level-chapter gate applies after restored inventory.");
    }
}
