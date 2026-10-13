namespace OpenSpidey.Core.Cameras;

/// <summary>CCamera's field236 yaw and the shared SetCamAngle/AI interpolation globals.</summary>
public sealed class CameraHeadingTransition
{
    public short Heading { get; set; }
    public int PublishedHeading { get; set; }
    public int Target { get; set; }
    public int RemainingTicks { get; set; }
    public int Rate { get; set; }

    public void Set(short wanted, ushort duration, uint mode)
    {
        if (mode is 15 or 16 or 17) return;
        short target = unchecked((short)(wanted & 4095));
        if (duration == 0)
        {
            RemainingTicks = 0;
            Heading = target;
            PublishedHeading = target;
            return;
        }
        Target = target;
        int difference = target - Heading;
        if (difference == 0) { Rate = 0; RemainingTicks = 0; return; }
        if (difference > 2048) difference -= 4096;
        else if (difference < -2048) difference += 4096;
        RemainingTicks = duration;
        Rate = difference / duration;
    }

    public void Advance(int elapsedTicks, uint mode)
    {
        int yaw;
        if (RemainingTicks <= elapsedTicks)
        {
            if (RemainingTicks != 0)
            {
                RemainingTicks = 0;
                Heading = unchecked((short)Target);
                yaw = Heading;
            }
            else yaw = PublishedHeading;
        }
        else
        {
            RemainingTicks = unchecked(RemainingTicks - elapsedTicks);
            // The PC multiplies AX by the low signed word of the rate before masking to twelve bits.
            yaw = unchecked(Heading + (short)elapsedTicks * (short)Rate) & 4095;
            Heading = unchecked((short)yaw);
        }
        PublishedHeading = yaw & 4095;
        if (mode is not (15 or 16 or 17)) Heading = unchecked((short)PublishedHeading);
    }
}
