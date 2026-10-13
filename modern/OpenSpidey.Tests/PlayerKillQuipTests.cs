using System.Text.Json;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;

internal static class PlayerKillQuipTests
{
    private sealed class Host(uint seed) : IPlayerKillQuipHost
    {
        private uint randomState = seed;
        public uint Clock { get; set; }
        public short Health { get; set; }
        public IReadOnlyList<PlayerKillQuipEnemy> SourceBaddyOrder { get; set; } = [];
        public List<int> Events { get; } = [];
        public Action<SpeechRequest>? Speech;
        public int Random(int range)
        {
            Events.AddRange([1, range]);
            randomState = unchecked(randomState * 1664525 + 1013904223);
            return range == 0 ? 0 : (int)(randomState % (uint)range);
        }
        public void PlaySpeech(SpeechRequest request)
        {
            Events.AddRange([2, request.Bank, request.Slot, request.Priority]); Speech?.Invoke(request);
        }
    }
    private sealed class Playback : ISpeechPlayback
    {
        public bool Finished => false;
        public int Calls;
        public void Play(SpeechClip clip) => Calls++;
        public void Stop() { }
        public void Pause(bool paused) { }
    }

    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(PlayerKillQuipTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-kill-quips.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        int index = 0;
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] input = test.GetProperty("input").EnumerateArray().Select(value => value.GetInt64()).ToArray();
            var history = new PlayerKillQuipHistory { LastBank = 77, LastVariant = (int)input[8], NotificationCount = (int)input[9] };
            for (int i = 0; i < 5; i++) history.RecentSounds[i] = (int)input[10 + i];
            var quips = new PlayerKillQuips(history) { EncounterActive = (byte)input[1], StartingHealth = (int)input[2], EncounterClock = (uint)input[3] };
            var host = new Host((uint)input[7]) { Health = (short)input[4], Clock = (uint)input[5],
                SourceBaddyOrder = Enumerable.Range(0, 4).Select(i => new PlayerKillQuipEnemy((uint)input[15 + i * 3], (short)input[16 + i * 3], (uint)input[17 + i * 3])).ToArray() };
            int invocation = 0;
            foreach (JsonElement reference in test.GetProperty("expected").EnumerateArray())
            {
                host.Events.Clear();
                if (input[0] == 0) quips.NotifyKill((ushort)input[6], host, host.Random);
                else { quips.SetFirstContactDetails(host.Health, host.Clock); if (invocation == 3) quips.UpdateTargetCount(0); }
                long[] actual = [quips.EncounterActive, quips.StartingHealth, quips.EncounterClock, history.LastBank, history.LastVariant, history.NotificationCount];
                assert(actual.SequenceEqual(reference.EnumerateArray().Take(6).Select(value => value.GetInt64())) &&
                    history.RecentSounds.SequenceEqual(reference[6].EnumerateArray().Select(value => value.GetInt32())) &&
                    host.Events.SequenceEqual(reference[7].EnumerateArray().Select(value => value.GetInt32())),
                    $"Kill quip{index} call{invocation} matches actual PC gates, signed windows, live-enemy suppression, six source tables, random retry order, bank/slot globals, five-ID history and wrapping call counter.");
                host.Clock = unchecked(host.Clock + 17); host.Health = unchecked((short)(host.Health - 1)); invocation++;
            }
            index++;
        }
        var playback = new Playback(); var speech = new SpeechController(playback); speech.Stop();
        var attachment = new ActorSpeechAttachment(); attachment.Attach(101, 4, 2);
        var global = new PlayerKillQuipHistory(); var state = new PlayerKillQuips(global);
        state.SetFirstContactDetails(100, 0);
        var cooldown = new Host(0) { Health = 100, Speech = request => speech.Play(request.Bank, request.Slot, request.Priority) };
        state.NotifyKill(304, cooldown, range => range == 2 ? 1 : 0);
        assert(playback.Calls == 0 && speech.Cooldown == 30 && speech.Pending == new SpeechRequest(60, 0, 20) && global.RecentSounds[4] == 960 && global.NotificationCount == 1,
            "Actual kill notification consumes source history before global XA cooldown defers its priority20 request; it adds no independent timer.");
        assert(attachment.AttachedClock == 101 && attachment.Bank == 4 && attachment.Slot == 2,
            "NotifyKill calls global XA and leaves source actor speech attachment unchanged.");
        speech.Reset(); speech.Play(60, 1, 21);
        state.NotifyKill(306, cooldown, range => range == 2 ? 1 : 0);
        assert(playback.Calls == 1 && speech.Current == new SpeechRequest(60, 1, 21) && speech.Pending is null && global.RecentSounds[4] == (20 << 4) + 11,
            "Higher-priority XA playback rejects the kill quip while the source history still records that selected sound.");
        int initialHealth = state.StartingHealth; uint initialClock = state.EncounterClock;
        state.SetFirstContactDetails(12, 999); state.UpdateTargetCount(1);
        assert(state.StartingHealth == initialHealth && state.EncounterClock == initialClock && state.EncounterActive == 1,
            "Later contacts and nonzero target counts retain the first combat snapshot.");
        state.UpdateTargetCount(0); state.SetFirstContactDetails(12, 999);
        assert(state.EncounterActive == 1 && state.StartingHealth == 12 && state.EncounterClock == 999,
            "A new encounter snapshots health/clock only after the actual target-count reset.");
    }
}
