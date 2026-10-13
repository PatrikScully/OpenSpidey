using OpenSpidey.Core.Animation;

namespace OpenSpidey.Core.Player;

/// <summary>Actor animation commands without a dependency on a render engine.</summary>
public interface IPlayerAnimationDriver
{
    AnimationPlaybackState Playback { get; }
    int Speed { get; set; }
    void Advance(int ticks);
    void Run(int clip, int from = 0, int to = -1);
}
