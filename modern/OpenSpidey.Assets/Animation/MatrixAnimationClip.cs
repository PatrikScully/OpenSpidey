namespace OpenSpidey.Assets.Animation;

/// <summary>Samples original type-42 matrices, including sparse-key interval interpolation and terminal extrapolation.</summary>
public sealed class MatrixAnimationClip : IAnimationPoseClip
{
    private readonly short[] keys;
    public int Id { get; }
    public int FrameCount { get; }
    public int PartCount { get; }
    public int Interval { get; }

    internal MatrixAnimationClip(int id, int frames, int parts, int interval, short[] keys)
    {
        Id = id;
        FrameCount = frames;
        PartCount = parts;
        Interval = interval;
        this.keys = keys;
    }

    public void SampleFrame(int frame, bool loop, Span<FixedJointTransform> destination)
    {
        if ((uint)frame >= FrameCount) throw new ArgumentOutOfRangeException(nameof(frame));
        if (destination.Length < PartCount)
            throw new ArgumentException("Destination cannot hold all matrix parts.", nameof(destination));
        int lowerFrame = frame - frame % Interval;
        int upperFrame = lowerFrame + Interval;
        int fraction;
        if (Interval == 1)
        {
            fraction = 0;
        }
        else if (upperFrame < FrameCount)
        {
            fraction = ((frame - lowerFrame) << 12) / (upperFrame - lowerFrame);
        }
        else if (loop)
        {
            upperFrame = 0;
            fraction = ((frame % Interval) << 12) / (FrameCount - lowerFrame);
        }
        else
        {
            lowerFrame = Math.Max(0, lowerFrame - Interval);
            upperFrame -= Interval;
            // The recovered runtime extrapolates the preceding interval here, rather than clamping the final key.
            fraction = upperFrame == 0 ? 0 : ((frame - lowerFrame) << 12) / (upperFrame - lowerFrame);
        }

        int lower = lowerFrame / Interval * PartCount * 12;
        int upper = upperFrame / Interval * PartCount * 12;
        Span<short> values = stackalloc short[12];
        for (int part = 0; part < PartCount; part++)
        {
            for (int component = 0; component < 12; component++)
            {
                int index = part * 12 + component;
                int start = keys[lower + index];
                values[component] = fraction == 0 ? (short)start :
                    unchecked((short)(start + (((keys[upper + index] - start) * fraction) >> 12)));
            }
            destination[part] = new(values[0], values[1], values[2], values[3], values[4], values[5],
                values[6], values[7], values[8], values[9], values[10], values[11]);
        }
    }
}
