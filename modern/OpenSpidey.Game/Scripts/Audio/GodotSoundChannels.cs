using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Audio;

namespace OpenSpidey.Game.Audio;

/// <summary>Native synchronized left/right playback for source SFX channels and their independent stereo gains.</summary>
public partial class GodotSoundChannels : Node, ISoundChannelPlayback
{
    private sealed record StereoSamples(AudioStreamWav Left, AudioStreamWav Right);
    private sealed record Voice(AudioStreamPlayer Player, AudioStreamSynchronized Stream, int SampleRate);
    private readonly Dictionary<(string Path, bool Loop), StereoSamples> samples = [];
    private readonly Dictionary<int, Voice> voices = [];
    private bool disposed;
    public PkrArchive Archive { get; init; } = null!;
    public SoundGroup PlayerGroup { get; init; } = SoundCatalog.Find("spidey");
    public SoundGroup LevelGroup { get; init; } = null!;
    public int StartedCount { get; private set; }
    public int FinishedCount { get; private set; }
    public int MixUpdates { get; private set; }
    public int LiveVoices => voices.Count;
    public SoundChannelController Controller { get; private set; } = null!;
    public override void _Ready() => Controller = new(this);
    public bool IsPlaying(int channel) => voices.TryGetValue(channel, out Voice? voice) && voice.Player.Playing;

    public void Play(int channel, SoundDefinition definition, bool levelSpecific, SoundMix mix)
    {
        Stop(channel);
        string path = (levelSpecific ? LevelGroup : PlayerGroup).ArchivePath(definition.AssetIndex);
        var key = (path, definition.Looped);
        if (!samples.TryGetValue(key, out StereoSamples? stereo))
        {
            stereo = ReadSamples(path, definition.Looped);
            samples.Add(key, stereo);
        }
        var stream = new AudioStreamSynchronized { StreamCount = 2 };
        stream.SetSyncStream(0, stereo.Left);
        stream.SetSyncStream(1, stereo.Right);
        var player = new AudioStreamPlayer { Name = $"SoundChannel{channel}", Stream = stream };
        AddChild(player);
        voices.Add(channel, new(player, stream, stereo.Left.MixRate));
        UpdateMix(channel, mix);
        player.Finished += () =>
        {
            if (voices.GetValueOrDefault(channel)?.Player != player) return;
            FinishedCount++;
            Stop(channel);
        };
        player.Play();
        StartedCount++;
    }

    public void UpdateMix(int channel, SoundMix mix)
    {
        if (!voices.TryGetValue(channel, out Voice? voice)) return;
        int frequency = mix.PlaybackFrequency(voice.SampleRate);
        voice.Player.PitchScale = frequency == 0 ? 1 : frequency / (float)voice.SampleRate;
        voice.Stream.SetSyncStreamVolume(0, mix.VolumeDb + mix.LeftDb);
        voice.Stream.SetSyncStreamVolume(1, mix.VolumeDb + mix.RightDb);
        MixUpdates++;
    }

    public void Stop(int channel)
    {
        if (!voices.Remove(channel, out Voice? voice)) return;
        voice.Player.Stop();
        voice.Player.Stream = null;
        voice.Player.QueueFree();
        voice.Stream.Dispose();
    }

    internal AudioStreamPlayer? PlayerForChannel(int channel) => voices.GetValueOrDefault(channel)?.Player;

    internal (ulong Id, string Source, RefCounted Owner)[] ReadOwnedResources() => samples
        .SelectMany(entry => new[]
        {
            (entry.Value.Left.GetInstanceId(), entry.Key.Path + ": left PCM", (RefCounted)entry.Value.Left),
            (entry.Value.Right.GetInstanceId(), entry.Key.Path + ": right PCM", (RefCounted)entry.Value.Right)
        })
        .Concat(voices.Select(entry => (entry.Value.Stream.GetInstanceId(), $"channel {entry.Key}: synchronized stream", (RefCounted)entry.Value.Stream)))
        .ToArray();

    internal int ValidateCatalogSamples()
    {
        string[] paths = SoundCatalog.Groups.SelectMany(group => Enumerable.Range(0, group.Assets.Count)
            .Select(group.ArchivePath)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (string path in paths)
        {
            StereoSamples stereo = ReadSamples(path, loop: false);
            try
            {
                if (stereo.Left.GetLength() <= 0 || stereo.Left.GetLength() != stereo.Right.GetLength() ||
                    stereo.Left.MixRate <= 0 || stereo.Left.MixRate != stereo.Right.MixRate)
                    throw new InvalidDataException($"Source sound {path} lost its PCM duration or sample clock.");
            }
            finally { stereo.Left.Dispose(); stereo.Right.Dispose(); }
        }
        return paths.Length;
    }

    private StereoSamples ReadSamples(string path, bool loop)
    {
        PcmWaveSample source = PcmWaveSample.Read(Archive.Read(path));
        int width = source.BytesPerSample;
        byte[] data = source.Data;
        int sourceChannels = source.Channels;
        int frames = source.FrameCount;
        var left = new byte[frames * width * 2];
        var right = new byte[left.Length];
        for (int frame = 0; frame < frames; frame++)
        {
            data.AsSpan(frame * width * sourceChannels, width).CopyTo(left.AsSpan(frame * width * 2, width));
            int rightOffset = frame * width * sourceChannels + (source.Channels == 2 ? width : 0);
            data.AsSpan(rightOffset, width).CopyTo(right.AsSpan(frame * width * 2 + width, width));
        }
        if (width == 1)
        {
            // Godot stores signed 8-bit PCM. Convert samples without changing the zeroed opposite stereo channel.
            for (int frame = 0; frame < frames; frame++)
            {
                left[frame * 2] = unchecked((byte)(left[frame * 2] - 128));
                right[frame * 2 + 1] = unchecked((byte)(right[frame * 2 + 1] - 128));
            }
        }
        return new(Create(left), Create(right));
        AudioStreamWav Create(byte[] pcm) => new()
        {
            Data = pcm, Format = width == 1 ? AudioStreamWav.FormatEnum.Format8Bits : AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = source.SampleRate, Stereo = true,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0, LoopEnd = frames
        };
    }

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || disposed) return;
        disposed = true;
        foreach (int channel in voices.Keys.ToArray()) Stop(channel);
        foreach (StereoSamples stereo in samples.Values) { stereo.Left.Dispose(); stereo.Right.Dispose(); }
        samples.Clear();
    }
}
