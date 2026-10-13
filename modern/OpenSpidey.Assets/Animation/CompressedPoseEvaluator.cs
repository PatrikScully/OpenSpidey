namespace OpenSpidey.Assets.Animation;

public static class CompressedPoseEvaluator
{
    public static void Evaluate(CompressedAnimationLibrary library, DecodedAnimationClip clip,
        int frame, Span<FixedJointTransform> destination)
    {
        if ((uint)frame >= clip.FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        if (library.Parents.Count != clip.PartCount || library.CalculationOrder.Count != clip.PartCount)
            throw new ArgumentException("Clip and skeleton part counts differ.", nameof(library));
        if (destination.Length < clip.PartCount)
            throw new ArgumentException("Destination cannot hold all joint transforms.", nameof(destination));
        foreach (ushort part in library.CalculationOrder)
        {
            FixedJointTransform transform = FixedJointTransform.FromJoint(clip.GetJoint(frame, part));
            int parent = library.Parents[part];
            // Angles already describe the part's model-space orientation. Only its pivot inherits the parent rotation.
            destination[part] = parent == part ? transform : transform.WithParentTranslation(destination[parent]);
        }
    }
}
