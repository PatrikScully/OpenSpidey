using System.Text.Json;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;

internal static class SourceMusicTests
{
    public static void Run(Action<bool, string> assert)
    {
        using Stream resource = typeof(SourceMusicTests).Assembly.GetManifestResourceStream("OpenSpidey.Tests.Fixtures.source-music.json")!;
        using JsonDocument document = JsonDocument.Parse(resource);
        foreach (JsonElement test in document.RootElement.EnumerateArray())
        {
            long[] values = Numbers(test.GetProperty("input"));
            if (test.GetProperty("family").GetInt32() == 0) VerifyVoice(test, values, assert);
            else VerifyMusic(test, values, assert);
        }
    }

    private static void VerifyVoice(JsonElement test, long[] values, Action<bool, string> assert)
    {
        var playback = new Playback();
        var channels = new SoundChannelController(playback)
        {
            MasterVolume = 16384, PlayerBank = new([new SoundDefinition(1, 60, 0, 0)])
        };
        for (int index = 1; index < 32; index++) channels.Play(1, 4096);
        channels.Stop(unchecked(~(uint)values[2]) & 0xFFFFFFFE);
        playback.Updates.Clear();
        channels.SetVoiceVolume(unchecked((uint)values[0]), unchecked((short)values[1]));
        JsonElement expected = test.GetProperty("expected");
        long[] actualStored = Enumerable.Range(0, 32).Select(index => (long)channels.ReadVoiceVolume(index)).ToArray();
        assert(actualStored.SequenceEqual(Numbers(expected.GetProperty("stored"))),
            $"PC472150 first-mask-bit ushort storage differs: input=[{string.Join(',', values)}], actual=[{string.Join(',', actualStored)}].");
        long[][] updates = expected.GetProperty("updates").EnumerateArray().Select(Numbers).ToArray();
        assert(playback.Updates.Count == updates.Length && playback.Updates.Select((update, index) =>
                update.Channel == updates[index][0] && update.Mix.Volume == updates[index][1] && update.Mix.Pan == updates[index][2] &&
                Math.Abs(update.Mix.VolumeDb - 39f * (updates[index][3] - 255) / 100) < 0.0001f).All(value => value),
            $"PC472150/471D40 allocated/stale/native gain updates differ: input=[{string.Join(',', values)}].");
    }

    private static void VerifyMusic(JsonElement test, long[] v, Action<bool, string> assert)
    {
        var state = new SourceMusicState
        {
            TrackClock = (int)v[5], TrackIndex = (int)v[6], TrackEnding = (byte)v[7], FightIntroClock = (int)v[8],
            FightIntroIndex = (int)v[9], Phase = (int)v[10], LastVblank = (uint)v[11],
            SelectedIntro = (FightIntroPlaylist)v[12], FightVariant = (int)v[13], IntroCompleted = (byte)v[14]
        };
        var player = new PlayerCombatState(100, 100)
        {
            Field528 = (int)v[15], Field52C = (int)v[16], Field534 = (int)v[17], FightMusicClock = (int)v[18],
            FightMusicVoiceHandle = (uint)v[19], FightMusicEnabled = (byte)v[20], FightMusicTime = (int)v[21],
            FightMusicGain = (int)v[22], FightMusicFadeDown = (byte)v[23], FightMusicFadeUp = (byte)v[24]
        };
        var playback = new Playback { PlayResult = (uint)v[25] };
        new SourceMusicController(state, playback).Render((uint)v[1], (int)v[0], v[4] != 0 ? player : null, (int)v[2], (int)v[3]);
        long[] actualState = [state.TrackClock, state.TrackIndex, state.TrackEnding, state.FightIntroClock, state.FightIntroIndex,
            state.Phase, state.LastVblank, (int)state.SelectedIntro, state.FightVariant, state.IntroCompleted];
        long[] actualPlayer = [player.Field528, player.Field52C, player.Field534, player.FightMusicClock, player.FightMusicVoiceHandle,
            player.FightMusicEnabled, player.FightMusicTime, player.FightMusicGain, player.FightMusicFadeDown, player.FightMusicFadeUp];
        JsonElement expected = test.GetProperty("expected");
        long[][] events = expected.GetProperty("events").EnumerateArray().Select(Numbers).ToArray();
        assert(actualState.SequenceEqual(Numbers(expected.GetProperty("state"))) &&
               actualPlayer.SequenceEqual(Numbers(expected.GetProperty("player"))) &&
               playback.Events.Count == events.Length && playback.Events.Select((item, index) => item.SequenceEqual(events[index])).All(value => value),
            $"Whole PC459650 render differs: input=[{string.Join(',', v)}], state=[{string.Join(',', actualState)}], " +
            $"player=[{string.Join(',', actualPlayer)}], events={JsonSerializer.Serialize(playback.Events)}, expected={expected.GetRawText()}.");
    }

    private static long[] Numbers(JsonElement element) => element.EnumerateArray().Select(value => value.GetInt64()).ToArray();
    private sealed class Playback : ISoundChannelPlayback, ISourceMusicPlayback
    {
        private readonly bool[] playing = new bool[32];
        public List<(int Channel, SoundMix Mix)> Updates { get; } = [];
        public List<long[]> Events { get; } = [];
        public uint PlayResult { get; init; }
        public bool IsPlaying(int channel) => playing[channel];
        public void Play(int channel, SoundDefinition definition, bool levelSpecific, SoundMix mix) => playing[channel] = true;
        public void UpdateMix(int channel, SoundMix mix) => Updates.Add((channel, mix));
        public void Stop(int channel) => playing[channel] = false;
        public uint Play(uint sound, short volume, int pitchOffset)
        {
            Events.Add([0, sound, volume, pitchOffset]); return PlayResult;
        }
        public void SetVoiceVolume(uint handle, short volume) => Events.Add([1, handle, volume, 0]);
    }
}
