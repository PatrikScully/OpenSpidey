namespace OpenSpidey.Assets.Animation;

internal sealed class CompressedPoseClip(CompressedAnimationLibrary library, DecodedAnimationClip clip) : IAnimationPoseClip
{
    public int FrameCount => clip.FrameCount;
    public void SampleFrame(int frame, bool loop, Span<FixedJointTransform> destination) =>
        CompressedPoseEvaluator.Evaluate(library, clip, frame, destination);
}
