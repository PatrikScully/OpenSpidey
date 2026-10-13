using System.Text.Json;
using OpenSpidey.Core.Audio;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Missions;

internal static class SpeechTests
{
    public static void Run(Action<bool, string> assert)
    {
        assert(SpeechCatalog.Clips.Count == 1264 && SpeechCatalog.Clips.Count(clip => clip.Available) == 934 &&
            SpeechCatalog.Clips.Count(clip => clip.Available && !clip.HasMedia) == 20,
            "PC speech preserves separate filename and XA availability tables, including enabled blank slots.");
        assert(SpeechCatalog.Get(67, 15)?.Name == "stan_00.bik" && SpeechCatalog.Get(79, 0) is null && SpeechCatalog.Get(-1, 0) is null,
            "The scripted tutorial voice resolves its original PC filename and out-of-range banks fail safely.");
        assert(SpeechCatalog.Get(0, 16) == SpeechCatalog.Get(1, 0) && SpeechCatalog.Get(1, -1) == SpeechCatalog.Get(0, 15),
            "Bounded cross-bank slot addressing retains the original linear speech index.");
        using Stream resource = typeof(SpeechTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.speech.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            var backend = new Backend();
            var speech = new SpeechController(backend);
            JsonElement operations = test.GetProperty("operations"), expected = test.GetProperty("expected");
            for (int index = 0; index < operations.GetArrayLength(); index++)
            {
                JsonElement op = operations[index];
                var environment = new SpeechEnvironment(op[5].GetInt32() != 0, op[6].GetInt32() != 0, op[4].GetInt32() != 0);
                int result = -1;
                switch (op[0].GetInt32())
                {
                    case 0: result = speech.Play(op[1].GetInt32(), op[2].GetInt32(), op[3].GetInt32(), environment) ? 1 : 0; break;
                    case 1: speech.UpdateVblank(environment); break;
                    case 2: speech.Stop(); break;
                    case 3: speech.Pause(op[1].GetInt32() != 0); break;
                    case 4: backend.Finished = true; break;
                    case 5: speech.Allowed = op[1].GetInt32() != 0; break;
                }
                int[] actual = [result, speech.Busy ? 1 : 0, speech.Paused ? 1 : 0, speech.Priority, speech.Cooldown,
                    speech.Pending?.Bank ?? 0, speech.Pending?.Slot ?? 0, speech.Pending?.Priority ?? 0, speech.Allowed ? 1 : 0,
                    backend.Plays, backend.Stops, backend.Pauses, backend.LastTrack];
                assert(actual.SequenceEqual(expected[index].EnumerateArray().Select(value => value.GetInt32())),
                    $"Speech operation {index} [{string.Join(',', op.EnumerateArray().Select(v => v.GetInt32()))}] differs: " +
                    $"actual=[{string.Join(',', actual)}], expected={expected[index].GetRawText()}.");
            }
        }
        assert(SpeechController.BinkVolume(0) == 0 && SpeechController.BinkVolume(255) == 49152,
            "Speech volume retains original mute and maximum Bink gain.");
        using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, System.Text.Encoding.UTF8, leaveOpen: true))
            foreach (ushort word in new ushort[] { 4, 137, 139, 67, 15, 250, 136, ushort.MaxValue }) writer.Write(word);
        var missionBackend = new Backend();
        var missionSpeech = new SpeechController(missionBackend);
        var runtime = new MissionRuntime(new([new(0, 4, 0, data.ToArray())]), new MissionRuntimeTests.ActorFactory(),
            SpeechCommandHandler.CreateHandlers(missionSpeech, () => default));
        runtime.QueueAutoexec();
        assert(runtime.ExecutePending() == 1 && missionBackend.LastTrack == 67 * 16 + 15 && missionSpeech.Allowed &&
            missionSpeech.Priority == 250, "Original allow/disallow/play speech commands keep source order and the caller-owned allow flag.");
    }

    private sealed class Backend : ISpeechPlayback
    {
        public bool Finished { get; set; } = true;
        public int Plays { get; private set; }
        public int Stops { get; private set; }
        public int Pauses { get; private set; }
        public int LastTrack { get; private set; } = -1;
        public void Play(SpeechClip clip) { Plays++; LastTrack = clip.Index; Finished = !clip.HasMedia; }
        public void Stop() { Stops++; Finished = true; }
        public void Pause(bool paused) => Pauses++;
    }
}
