namespace OpenSpidey.Core.Animation;

/// <summary>Recovered actor animation state: signed frames, a Q16 fraction and Q16 playback speed.</summary>
public readonly record struct AnimationPlaybackState(ushort ClipId, short Frame, ushort Fraction,
    sbyte Direction, AnimationPlaybackMode Mode, ushort FrameCount, short TargetFrame, byte Finished, int Speed)
{
    public bool IsFinished => Finished != 0;
    public static AnimationPlaybackState Initial => new(0, 0, 0, 0, AnimationPlaybackMode.OneShot, 1, 0, 1, 0x10000);
}
