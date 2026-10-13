using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Audio;

/// <summary>The source render music owner uses the scene's real shared SFX pool and the current actual player.</summary>
public sealed class GodotMusicController : ISourceMusicPlayback
{
    private readonly SoundChannelController sounds;
    private readonly Func<PlayerCombatState?> player;
    private readonly Func<int> levelId, postWaterEffect, endTraining;
    public SourceMusicController Controller { get; }
    public SourceMusicState State => Controller.State;

    public GodotMusicController(SourceMusicState state, GodotSoundChannels sounds, Func<PlayerCombatState?> player,
        Func<int> levelId, Func<int> postWaterEffect, Func<int> endTraining)
    {
        this.sounds = (sounds ?? throw new ArgumentNullException(nameof(sounds))).Controller;
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.levelId = levelId ?? throw new ArgumentNullException(nameof(levelId));
        this.postWaterEffect = postWaterEffect ?? throw new ArgumentNullException(nameof(postWaterEffect));
        this.endTraining = endTraining ?? throw new ArgumentNullException(nameof(endTraining));
        Controller = new(state, this);
    }

    public void Render(uint vblank) => Controller.Render(vblank, levelId(), player(), postWaterEffect(), endTraining());
    public uint Play(uint sound, short volume, int pitchOffset)
    {
        if (pitchOffset != 0) throw new InvalidOperationException("Music_MusicUpdate supplies zero source pitch offset.");
        return sounds.Play(sound, volume);
    }
    public void SetVoiceVolume(uint handle, short volume) => sounds.SetVoiceVolume(handle, volume);

    public void StopPlayerVoice(PlayerCombatState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        uint handle = state.FightMusicVoiceHandle;
        state.Field528 = state.Field52C = 0;
        if (handle == 0) return;
        sounds.Stop(handle); state.FightMusicVoiceHandle = 0;
    }
}
