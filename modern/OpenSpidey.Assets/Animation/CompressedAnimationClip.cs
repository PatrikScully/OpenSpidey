namespace OpenSpidey.Assets.Animation;

/// <summary>A compressed clip owns its encoded bytes; decoding does not depend on an archive's lifetime.</summary>
public sealed class CompressedAnimationClip
{
    private readonly byte[] encoded;
    public int Id { get; }
    public int FrameCount { get; }
    public int PartCount { get; }

    internal CompressedAnimationClip(int id, int frameCount, int partCount, ReadOnlySpan<byte> source)
    {
        Id = id;
        FrameCount = frameCount;
        PartCount = partCount;
        encoded = source.ToArray();
    }

    public DecodedAnimationClip Decode()
    {
        int stride = checked(PartCount * 6);
        long sampleCount = (long)FrameCount * stride;
        if (sampleCount > 16_000_000)
            throw new InvalidDataException($"Animation {Id} exceeds the decoded sample limit.");
        var samples = new short[(int)sampleCount];
        int cursor = 0;
        for (int channel = 0; channel < stride; channel++)
        {
            try
            {
                cursor += AnimationChannelDecoder.Decode(encoded.AsSpan(cursor), samples.AsSpan(channel), FrameCount, stride);
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException($"Animation {Id}, channel {channel}, byte {cursor}: {exception.Message}", exception);
            }
        }
        return new DecodedAnimationClip(FrameCount, PartCount, samples, cursor, encoded.Length);
    }
}
