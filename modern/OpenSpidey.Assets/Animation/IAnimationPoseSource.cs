namespace OpenSpidey.Assets.Animation;

public interface IAnimationPoseSource
{
    int ClipCount { get; }
    IReadOnlyList<ushort> Parents { get; }
    IReadOnlyList<ushort> CalculationOrder { get; }
    IAnimationPoseClip DecodeClip(int id);
}
