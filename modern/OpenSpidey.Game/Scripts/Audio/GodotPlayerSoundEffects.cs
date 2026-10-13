using Godot;
using OpenSpidey.Core.Player;
using System.Numerics;

namespace OpenSpidey.Game.Audio;

/// <summary>Player animation sound events share the session's recovered SFX channel pool.</summary>
public partial class GodotPlayerSoundEffects : Node
{
    public GodotSoundChannels Backend { get; init; } = null!;
    public int StartedCount { get; private set; }
    public int FinishedCount => Backend.FinishedCount;
    public uint LastSound { get; private set; }
    public uint LastHandle { get; private set; }
    public AudioStreamPlayer? LastVoice => LastHandle is 0 or uint.MaxValue ? null
        : Backend.PlayerForChannel(BitOperations.TrailingZeroCount(LastHandle));

    public void Play(PlayerFrameSound sound)
    {
        LastHandle = Backend.Controller.Play(sound.Sound, unchecked((short)sound.Volume));
        if (LastHandle is 0 or uint.MaxValue) return;
        StartedCount++;
        LastSound = sound.Sound;
    }
}
