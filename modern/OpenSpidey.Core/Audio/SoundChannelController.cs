using OpenSpidey.Assets.Audio;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Audio;

public readonly record struct SoundMix(int Volume, int Pan, byte Pitch)
{
    public float VolumeDb => SoundVolume.Decibels(Volume);
    public int PlaybackFrequency(int sampleRate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        return (int)(Pitch / 1200f * sampleRate);
    }
    public float LeftDb => Math.Min(0, -(645 * Pan - 10000)) / 100f;
    public float RightDb => Math.Min(0, 645 * Pan - 10000) / 100f;
}

public interface ISoundChannelPlayback
{
    bool IsPlaying(int channel);
    void Play(int channel, SoundDefinition definition, bool levelSpecific, SoundMix mix);
    void UpdateMix(int channel, SoundMix mix);
    void Stop(int channel);
}

public readonly record struct SoundChannelState(bool Allocated, bool Looped, bool Playing, SoundMix Mix);

/// <summary>Recovered shared SFX voice allocation, source mixing and bit-mask handles.</summary>
public sealed class SoundChannelController(ISoundChannelPlayback playback)
{
    private sealed class Channel
    {
        public SoundDefinition? Definition;
        public SoundMix Mix;
        public ushort StoredVolume; // Actual SSfxEntry16 survives voice release.
        public ushort PositionFalloff;
    }
    private readonly Channel[] channels = Enumerable.Range(0, 32).Select(_ => new Channel()).ToArray();
    private int voiceCursor = 1;
    public SoundDefinitionBank? PlayerBank { get; set; }
    public SoundDefinitionBank? LevelBank { get; set; }
    public int MasterVolume { get; set; } = 0x2FFF;
    public bool Mono { get; set; }
    public int AllocatedCount => channels.Count(channel => channel.Definition is not null);
    public SoundChannelState ReadChannel(int channel)
    {
        if ((uint)channel >= channels.Length) throw new ArgumentOutOfRangeException(nameof(channel));
        Channel state = channels[channel];
        return new(state.Definition is not null, state.Definition?.Looped == true, playback.IsPlaying(channel), state.Mix);
    }

    public ushort ReadVoiceVolume(int channel)
    {
        if ((uint)channel >= channels.Length) throw new ArgumentOutOfRangeException(nameof(channel));
        return channels[channel].StoredVolume;
    }

    /// <summary>PC472150 updates only the first selected voice; its stored ushort is zero-extended before mixing.</summary>
    public void SetVoiceVolume(uint handle, short volume)
    {
        if (handle == 0) return;
        int index = System.Numerics.BitOperations.TrailingZeroCount(handle);
        Channel channel = channels[index];
        channel.StoredVolume = unchecked((ushort)volume);
        if (channel.Definition is null) return;
        channel.Mix = new(Math.Min(channel.StoredVolume, (ushort)16383), 15, channel.Mix.Pitch);
        channel.StoredVolume = unchecked((ushort)channel.Mix.Volume);
        playback.UpdateMix(index, channel.Mix);
    }

    public uint Play(uint sound, short volume = 8192)
    {
        SoundDefinition? definition = Resolve(sound);
        if (definition is null) return sound == 0 || HasBank(sound) ? 0 : uint.MaxValue;
        int scaled = unchecked(volume * MasterVolume) >> 14;
        return Start(definition, (sound & 0x8000) != 0, scaled, scaled);
    }

    public uint PlayAt(uint sound, FixedVector3 position, SoundListener listener)
    {
        SoundDefinition? definition = Resolve(sound);
        if (definition is null) return sound == 0 || HasBank(sound) ? 0 : uint.MaxValue;
        StereoAttenuation attenuation = SpatialSound.Calculate(position, definition.Falloff >> 2, definition.Falloff * 2, listener);
        (int left, int right) = Scale(attenuation);
        return Start(definition, (sound & 0x8000) != 0, left, right, definition.Falloff);
    }

    public void UpdatePosition(uint handle, FixedVector3 position, SoundListener listener)
    {
        for (int index = 0; index < 32; index++)
        {
            if ((handle & (1u << index)) == 0 || channels[index].Definition is null) continue;
            ushort falloff = channels[index].PositionFalloff;
            StereoAttenuation attenuation = SpatialSound.Calculate(position, falloff >> 2, falloff * 2, listener);
            (int left, int right) = Scale(attenuation);
            channels[index].Mix = Mix(left, right, channels[index].Mix.Pitch);
            channels[index].StoredVolume = unchecked((ushort)channels[index].Mix.Volume);
            playback.UpdateMix(index, channels[index].Mix);
        }
    }

    public void Stop(uint handle)
    {
        for (int index = 0; index < 32; index++)
        {
            if ((handle & (1u << index)) == 0) continue;
            // The PC routine returns at the first unallocated selected channel.
            if (channels[index].Definition is null) return;
            Release(index);
        }
    }

    private SoundDefinition? Resolve(uint sound) => ((sound & 0x8000) != 0 ? LevelBank : PlayerBank)?.Resolve(sound);
    private bool HasBank(uint sound) => ((sound & 0x8000) != 0 ? LevelBank : PlayerBank) is not null;

    private uint Start(SoundDefinition definition, bool levelSpecific, int left, int right, ushort? positionFalloff = null)
    {
        ReclaimInactiveVoice();
        for (int index = 1; index < 32; index++)
        {
            if (channels[index].Definition is not null) continue;
            int logarithmicPitch = ConvertPitch((100 * (definition.Pitch - 60)) & 255);
            SoundMix mix = Mix(left, right, unchecked((byte)logarithmicPitch));
            playback.Play(index, definition, levelSpecific, mix);
            channels[index].Definition = definition;
            channels[index].Mix = mix;
            channels[index].StoredVolume = unchecked((ushort)mix.Volume);
            if (positionFalloff is { } falloff) channels[index].PositionFalloff = falloff;
            return 1u << index;
        }
        return 0;
    }

    private void ReclaimInactiveVoice()
    {
        int previous = voiceCursor;
        for (int index = voiceCursor; index < 32; index++)
            if (TryReclaim(index)) return;
        for (int index = 1; index < previous; index++)
            if (TryReclaim(index)) return;
    }

    private bool TryReclaim(int channel)
    {
        if (playback.IsPlaying(channel)) return false;
        Release(channel);
        voiceCursor = channel == 31 ? 1 : channel + 1;
        return true;
    }

    private void Release(int channel)
    {
        if (channels[channel].Definition is null) return;
        playback.Stop(channel);
        channels[channel].Definition = null;
    }

    private (int Left, int Right) Scale(StereoAttenuation attenuation)
    {
        int left = unchecked((attenuation.Left << 2) * MasterVolume) >> 14;
        int right = unchecked((attenuation.Right << 2) * MasterVolume) >> 14;
        if (Mono) left = right = (left + right) / 2;
        return (left, right);
    }

    private static SoundMix Mix(int left, int right, byte pitch) => new(Math.Clamp((left + right) >> 1, -16383, 16383),
        left == right ? 15 : 31 * left / (left + right), pitch);
    private static int ConvertPitch(int pitch) => pitch <= 0 ? 0 : (int)((1.442695f * MathF.Log(pitch) - 10f) * 1200f);
}
