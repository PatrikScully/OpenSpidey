using OpenSpidey.Core.Animation;
using OpenSpidey.Core.Player.Scripting;

namespace OpenSpidey.Core.Player;

/// <summary>Animation driver that retains the source's reset/preserve sound-script policy.</summary>
public sealed class PlayerAnimationSoundDriver(IPlayerAnimationDriver animation, PlayerFrameSounds sounds) : IPlayerAnimationDriver
{
    public Action<int>? SourceScriptsSelected {get;set;}
    public AnimationPlaybackState Playback => animation.Playback;
    public int Speed { get => animation.Speed; set => animation.Speed = value; }
    public void Advance(int ticks) => animation.Advance(ticks);
    public void Run(int clip, int from = 0, int to = -1)
    {
        sounds.Select(clip);
        SourceScriptsSelected?.Invoke(clip);
        animation.Run(clip, from, to);
    }
    public void RunWithSoundPolicy(int clip, ScriptAnimationPolicy policy)
    {
        if (policy == ScriptAnimationPolicy.ResetSoundScript)
        {sounds.Select(clip);SourceScriptsSelected?.Invoke(clip);}
        animation.Run(clip);
    }
}
