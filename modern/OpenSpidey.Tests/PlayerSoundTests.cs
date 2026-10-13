using System.Text.Json;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Player;
using OpenSpidey.Core.Player.Scripting;

internal static class PlayerSoundTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(PlayerSoundTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.player-sounds.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            int random = 0;
            var sounds = new PlayerFrameSounds(maximum => random++ % maximum);
            sounds.Select(test.GetProperty("selected").GetInt32());
            JsonElement frames = test.GetProperty("frames"), expected = test.GetProperty("expected");
            for (int index = 0; index < frames.GetArrayLength(); index++)
            {
                PlayerFrameSound? sound = sounds.Update(test.GetProperty("animation").GetInt32(), frames[index].GetInt32(),
                    test.GetProperty("alias").GetInt32() != 0);
                assert((sound is { } played ? (int)played.Sound : -1) == expected[index][0].GetInt32() &&
                    sounds.Cursor == expected[index][1].GetInt32() && sounds.Active == (expected[index][2].GetInt32() != 0) &&
                    sounds.LastFootstep == expected[index][3].GetUInt32(),
                    "Frame sounds, cursor lifetime and nonrepeating footsteps match recovered ProcessSFXArray updates.");
            }
        }
        byte[] definitions = Words([0xAA0302FE, 0x1234BEEF, 80, 0, uint.MaxValue]);
        SoundDefinitionBank bank = SoundDefinitionBank.Read(definitions, levelSpecific: true);
        SoundDefinition definition = bank.Sounds.Single();
        assert(definition.Flags == 0xC0020008 && definition.Parameters == 0x123400AA && definition.Falloff == 0xBEEF &&
            definition.Alias == 80 && definition.AssetIndex == 2 && definition.Looped && definition.Pitch == 170,
            "SFX records retain the source bit packing, loop flag, pitch, falloff and alias widths.");
        assert(bank.Resolve(0x8050) == definition && bank.Resolve(0x8051) is null && bank.Resolve(0) is null && bank.Resolve(1) == definition,
            "Sound aliases use the original first-match index and direct IDs are one-based.");
        assert(Rejects(() => SoundDefinitionBank.Read(definitions[..15])) && Rejects(() => SoundDefinitionBank.Read(definitions[..16])),
            "Truncated sound records and absent terminators fail before entering audio playback.");
        assert(PlayerSoundCatalog.ArchivePath(0) == "data/audio/spstep1.wav" && PlayerSoundCatalog.ArchivePath(8) == "data/audio/spwall1.wav" &&
            PlayerSoundCatalog.Decibels(0) == -99.45f && PlayerSoundCatalog.Decibels(8192) == 0,
            "The PC player WAV table and original DirectSound attenuation curve retain mute and full source gain.");
        var scheduler = new PlayerFrameSounds(_ => 0);
        var driver = new PlayerAnimationSoundDriver(new Animation(), scheduler);
        driver.Run(21);
        scheduler.Update(21, 1);
        driver.RunWithSoundPolicy(176, ScriptAnimationPolicy.PreserveSoundScript);
        assert(scheduler.Active && scheduler.Cursor == 1, "Raw scripted animation preserves the selected sound-script cursor.");
        driver.RunWithSoundPolicy(176, ScriptAnimationPolicy.ResetSoundScript);
        assert(!scheduler.Active && scheduler.Cursor == 0, "Player animation restart selects and resets its sound script.");
    }

    private static byte[] Words(uint[] words)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        foreach (uint word in words) writer.Write(word);
        return stream.ToArray();
    }
    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidDataException) { return true; }
    }
    private sealed class Animation : IPlayerAnimationDriver
    {
        public AnimationPlaybackState Playback { get; private set; } = AnimationPlaybackState.Initial;
        public int Speed { get; set; }
        public void Advance(int ticks) { }
        public void Run(int clip, int from = 0, int to = -1) => Playback = Playback with { ClipId = (ushort)clip };
    }
}
