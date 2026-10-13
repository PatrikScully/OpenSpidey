using System.Text.Json;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;

internal static class SoundChannelTests
{
    public static void Run(Action<bool, string> assert)
    {
        using (JsonDocument document = Fixture("sound-frequency.json"))
            foreach (JsonElement test in document.RootElement.EnumerateArray())
                assert(new SoundMix(0, 15, test.GetProperty("pitch").GetByte()).PlaybackFrequency(test.GetProperty("rate").GetInt32()) ==
                    test.GetProperty("frequency").GetInt32(), "SFX pitch conversion retains the PC integer frequency at the source sample rate.");
        using (JsonDocument document = Fixture("spatial-sound.json"))
            foreach (JsonElement test in document.RootElement.EnumerateArray())
            {
                StereoAttenuation result = SpatialSound.Calculate(Vector(test.GetProperty("position")),
                    test.GetProperty("near").GetInt32(), test.GetProperty("far").GetInt32(), new(Vector(test.GetProperty("listener"))));
                int[] expected = test.GetProperty("expected").EnumerateArray().Select(value => value.GetInt32()).ToArray();
                assert(result.Left == expected[0] && result.Right == expected[1],
                    "SFX near/far attenuation and world-axis panning match the recovered PC helper, including its unchanged GTE read register.");
            }
        using (JsonDocument document = Fixture("sound-channels.json"))
            foreach (JsonElement sequence in document.RootElement.EnumerateArray())
            {
                var backend = new Playback();
                var controller = new SoundChannelController(backend) { PlayerBank = Bank(false), LevelBank = Bank(true) };
                foreach (JsonElement step in sequence.EnumerateArray())
                {
                    controller.MasterVolume = step.GetProperty("master").GetInt32();
                    controller.Mono = step.GetProperty("mono").GetBoolean();
                    uint sound = step.GetProperty("sound").GetUInt32(), handle = 0;
                    FixedVector3 position = Vector(step.GetProperty("position"));
                    switch (step.GetProperty("kind").GetInt32())
                    {
                        case 0: handle = controller.Play(sound, step.GetProperty("volume").GetInt16()); break;
                        case 1: backend.Playing[step.GetProperty("channel").GetInt32()] = false; break;
                        case 2: controller.Stop(sound); break;
                        case 3: handle = controller.PlayAt(sound, position, new(default)); break;
                        case 4: controller.UpdatePosition(sound, position, new(default)); break;
                    }
                    long[] actual = new long[194];
                    actual[0] = handle; actual[1] = controller.AllocatedCount;
                    for (int channel = 0; channel < 32; channel++)
                    {
                        SoundChannelState state = controller.ReadChannel(channel);
                        int offset = 2 + channel * 6;
                        actual[offset] = state.Allocated ? 1 : 0; actual[offset + 1] = state.Looped ? 1 : 0;
                        actual[offset + 2] = state.Playing ? 1 : 0; actual[offset + 3] = state.Mix.Volume;
                        actual[offset + 4] = state.Mix.Pan; actual[offset + 5] = state.Mix.Pitch;
                    }
                    long[] expected = step.GetProperty("expected").EnumerateArray().Select(value => value.GetInt64()).ToArray();
                    int mismatch = -1;
                    for (int index = 0; index < actual.Length; index++)
                        if (actual[index] != expected[index]) { mismatch = index; break; }
                    assert(mismatch < 0,
                        $"SFX source snapshot mismatch at field {mismatch}: actual={actual[Math.Max(0, mismatch)]}, expected={expected[Math.Max(0, mismatch)]}, command={step.GetRawText()[..Math.Min(170, step.GetRawText().Length)]}.");
                }
            }
        assert(SoundCatalog.Groups.Count == 58 && SoundCatalog.Find("SPIDEY.kat").Assets.SequenceEqual(PlayerSoundCatalog.Names),
            "Full PC group metadata retains the validated player WAV order.");
        assert(SoundCatalog.Find("data/L1A1.sfx").ArchivePath(33) == "data/audio/heli1.wav" &&
            SoundCatalog.Find("dem3").Group == 53 && SoundCatalog.Find("l4a4").Group == 13,
            "Recovered group aliases resolve the original level WAV names and selector exceptions.");
        var missing = new SoundChannelController(new Playback());
        assert(missing.Play(0) == 0 && missing.Play(1) == uint.MaxValue && missing.PlayAt(0x8001, default, new(default)) == uint.MaxValue,
            "Unavailable source banks retain the PC failure handle while a zero sound remains a no-op.");
    }
    private static SoundDefinitionBank Bank(bool level)
    {
        byte[] pitches = [60, 59, 61, 0, 255, 127, 64, 80];
        return new(Enumerable.Range(0, 8).Select(index => new SoundDefinition(
            1u | ((uint)index << 16) | (index % 2 == 1 ? 0x80000000u : 0) | (level ? 0x40000000u : 0),
            pitches[index] | ((uint)(4096 + index * 100) << 16), (ushort)(1024 + index * 1000), (ushort)(80 + index))).ToArray());
    }
    private static FixedVector3 Vector(JsonElement element)
    {
        int[] values = element.EnumerateArray().Select(value => value.GetInt32()).ToArray();
        return new(values[0], values[1], values[2]);
    }
    private static JsonDocument Fixture(string name)
    {
        using Stream resource = typeof(SoundChannelTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures." + name)!;
        return JsonDocument.Parse(resource);
    }
    private sealed class Playback : ISoundChannelPlayback
    {
        public bool[] Playing { get; } = new bool[32];
        public bool IsPlaying(int channel) => Playing[channel];
        public void Play(int channel, SoundDefinition definition, bool levelSpecific, SoundMix mix) => Playing[channel] = true;
        public void UpdateMix(int channel, SoundMix mix) { }
        public void Stop(int channel) => Playing[channel] = false;
    }
}
