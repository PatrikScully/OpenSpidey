using System.Buffers.Binary;
using System.Text.Json;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;

internal static class ActorSpeechCommandTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream stream = typeof(ActorSpeechCommandTests).Assembly.GetManifestResourceStream(
            "OpenSpidey.Tests.Fixtures.actor-speech.json")!;
        using JsonDocument document = JsonDocument.Parse(stream);
        VerifyCommands(document.RootElement.GetProperty("commands"), assert);
        VerifyPlayback(document.RootElement.GetProperty("playback"), assert);
    }

    private static void VerifyCommands(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            ushort[] words = test.GetProperty("words").EnumerateArray().Select(value => value.GetUInt16()).ToArray();
            byte[] bytes = new byte[words.Length * 2];
            for (int word = 0; word < words.Length; word++) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(word * 2), words[word]);
            var cursor = new ActorScriptCursor(bytes);
            var host = new CommandHost(cursor);
            var actor = new ActorScriptState { ActorType = test.GetProperty("actorType").GetInt16(),
                SkipDuringDeath = test.GetProperty("skip").GetInt32() != 0 };
            int[] position = test.GetProperty("position").EnumerateArray().Select(value => value.GetInt32()).ToArray();
            bool result = ActorSpeechCommands.TryExecute(test.GetProperty("opcode").GetUInt16(), cursor, actor,
                new(FastForwardDeath: test.GetProperty("fast").GetInt32() != 0), new(position[0], position[1], position[2]), host);
            JsonElement expected = test.GetProperty("expected");
            assert(result && expected.GetProperty("result").GetInt32() == 1 && cursor.Position == expected.GetProperty("consumed").GetInt32() &&
                host.Calls.SequenceEqual(expected.GetProperty("calls").EnumerateArray().Select(value => value.GetInt64())),
                $"Whole PC speech command {index++} matches three raw unsigned words, exact six-byte cursor at playback, actor position and source203 death fast-forward suppression.");
        }
        var unsupported = new ActorScriptCursor(new byte[] { 1, 0 });
        assert(!ActorSpeechCommands.TryExecute(0x42A2, unsupported, new(), default, default, new CommandHost(unsupported)) && unsupported.Position == 0,
            "An unrelated inherited command retains its cursor and never reaches the XA host.");
    }

    private static void VerifyPlayback(JsonElement cases, Action<bool, string> assert)
    {
        int index = 0;
        foreach (JsonElement test in cases.EnumerateArray())
        {
            var backend = new Playback();
            var speech = new SpeechController(backend);
            JsonElement expectedRows = test.GetProperty("expected");
            int operation = 0;
            foreach (JsonElement row in test.GetProperty("operations").EnumerateArray())
            {
                int kind = row[0].GetInt32(), bank = row[1].GetInt32(), slot = row[2].GetInt32(), priority = row[3].GetInt32();
                backend.Clock = row[4].GetUInt32();
                backend.ClockAdvance = row[7].GetInt32();
                var environment = new SpeechEnvironment(FileIoBusy: row[5].GetInt32() != 0, MovieActive: row[6].GetInt32() != 0);
                int result = -1, clockReads = 0;
                if (kind == 0) result = speech.Play(bank, slot, priority, environment) ? 1 : 0;
                if (kind == 1) result = speech.PlayAtPosition(bank, slot, priority, () => { clockReads++; return backend.Clock; }, environment) ? 1 : 0;
                if (kind == 2) speech.Reset();
                if (kind == 3) speech.Stop();
                SpeechRequest pending = speech.Pending ?? default;
                PositionSpeechStart start = speech.LastPositionStart;
                long[] actual = [result, speech.Busy ? 1 : 0, speech.Priority, speech.Cooldown,
                    pending.Bank, pending.Slot, pending.Priority, start.Bank, start.Slot, start.Vblank,
                    backend.Plays, backend.Stops, backend.LastTrack, backend.Clock];
                JsonElement expected = expectedRows[operation++];
                long[] original = expected.EnumerateArray().Select(value => value.GetInt64()).Where((_, field) => field != 13).ToArray();
                assert(actual.SequenceEqual(original) && clockReads == (kind == 1 && result == 1 ? 1 : 0),
                    $"Whole PC XAPlayPos/XAPlay operation {index++} retains original availability/priority/IO/movie/pending gates, accepted transport, after-play vblank, separate positional marker and reset/stop/global-play retention.");
            }
        }
    }

    private sealed class CommandHost(ActorScriptCursor cursor) : IActorSpeechCommandHost
    {
        public List<long> Calls { get; } = [];
        public void PlayGlobalSpeech(SpeechRequest request) => Calls.AddRange([0, request.Bank, request.Slot, request.Priority, cursor.Position]);
        public void PlayPositionSpeech(SpeechRequest request, FixedVector3 position) =>
            Calls.AddRange([1, request.Bank, request.Slot, request.Priority, position.X, position.Y, position.Z, cursor.Position]);
    }

    private sealed class Playback : ISpeechPlayback
    {
        public bool Finished => false;
        public uint Clock { get; set; }
        public int ClockAdvance { get; set; }
        public int Plays { get; private set; }
        public int Stops { get; private set; }
        public int LastTrack { get; private set; } = -1;
        public void Play(SpeechClip clip)
        { Plays++; LastTrack = clip.Index; Clock = unchecked(Clock + (uint)ClockAdvance); }
        public void Stop() => Stops++;
        public void Pause(bool paused) => throw new InvalidOperationException("This original XA command fixture never pauses audio.");
    }
}
