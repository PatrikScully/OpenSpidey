using OpenSpidey.Core.Animation;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerAnimationPlan(int Clip, int From = 0, int To = -1, bool Restart = true);

/// <summary>Recovered movement start, stop and reverse clip selection, independent of playback and sound effects.</summary>
public static class PlayerLocomotionAnimations
{
    public static int Start(bool crawling, bool hardSurface)
    {
        if (!crawling) return 1;
        return hardSurface ? 58 : 57;
    }

    public static int? Reverse(ushort clip) => clip switch { 21 => 25, 52 => 91, 50 or 51 => 89, _ => null };

    public static PlayerAnimationPlan Stand(AnimationPlaybackState playback, bool crawling, int? heldObjectFlags = null) => playback.ClipId switch
    {
        50 or 51 or 60 or 63 or 72 or 75 or 14 => new(55),
        52 or 66 or 69 or 78 or 81 => new(56),
        57 or 58 when playback.Direction == 1 => new(playback.ClipId, playback.Frame, 0),
        129 => new(130),
        133 => new(134),
        196 => new(200),
        190 => new(194),
        21 => new(playback.Frame switch { >= 18 or <= 3 => 11, < 10 or > 14 => 13, _ => 12 }),
        20 or 130 or 134 => new(playback.ClipId, Restart: false),
        _ => new(crawling ? 19 : heldObjectFlags is { } flags ? (flags & 8) == 0 ? 194 : 200 : 0)
    };
}
