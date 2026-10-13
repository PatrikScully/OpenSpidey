namespace OpenSpidey.Assets.Animation;

public sealed class DecodedAnimationClip
{
    private readonly short[] samples;
    public int FrameCount { get; }
    public int PartCount { get; }
    public int BytesConsumed { get; }
    public int EncodedSize { get; }

    internal DecodedAnimationClip(int frames, int parts, short[] samples, int consumed, int encodedSize)
    {
        FrameCount = frames;
        PartCount = parts;
        this.samples = samples;
        BytesConsumed = consumed;
        EncodedSize = encodedSize;
    }

    public RawJointPose GetJoint(int frame, int part)
    {
        if ((uint)frame >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        if ((uint)part >= PartCount) throw new ArgumentOutOfRangeException(nameof(part));
        int offset = (frame * PartCount + part) * 6;
        return new(samples[offset], samples[offset + 1], samples[offset + 2],
            samples[offset + 3], samples[offset + 4], samples[offset + 5]);
    }
}
