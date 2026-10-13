namespace OpenSpidey.Assets.Animation;

/// <summary>Supplies model-space poses regardless of the original clip's storage format.</summary>
public interface IAnimationPoseClip
{
    int FrameCount { get; }
    void SampleFrame(int frame, bool loop, Span<FixedJointTransform> destination);
}
