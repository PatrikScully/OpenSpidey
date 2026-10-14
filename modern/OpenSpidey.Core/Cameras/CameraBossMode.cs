using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Whole original4192F0: boss focus identity, per-frame bounded yaw and retained normal projection/placement.</summary>
public sealed class CameraBossMode
{
    public int SpinRate { get; set; } = 31; // Source camera constructor0x2A4; Scorpion sets127.
    public int StationaryRadius { get; set; } = 512; // Source0x2A8; Scorpion sets0.

    public FixedVector3 Calculate(CameraNormalPlacementState normal, CameraNormalProjectionCache projection,
        CameraFocusMotion focus, CameraFocusTarget? tripod, CameraFocusTarget? boss,
        FixedVector3 tripodPosition, FixedVector3 offset, int horizontalDistance, int verticalDistance)
    {
        ArgumentNullException.ThrowIfNull(normal);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(focus);
        focus.Target = boss ?? tripod;
        if (boss is not null)
        {
            offset = new(0, offset.Y, 0);
            FixedVector3 bossPosition = boss.Position;
            int distance = ApproximateDistance(tripodPosition, bossPosition);
            short yaw = normal.Heading;
            int delta = (FixedAim.Calculate(tripodPosition, bossPosition).Yaw & 4095) - (yaw & 4095);
            if (delta > 2048) delta -= 4096;
            else if (delta < -2048) delta += 4096;
            if (distance > StationaryRadius)
            {
                if (delta < -8)
                {
                    if (delta < unchecked(-SpinRate)) delta = unchecked(-SpinRate);
                    normal.Heading = unchecked((short)(yaw + delta));
                }
                else if (delta > 8)
                {
                    if (delta > SpinRate) delta = SpinRate;
                    normal.Heading = unchecked((short)(yaw + delta));
                }
            }
            normal.Heading = unchecked((short)(normal.Heading & 4095));
            projection.UpdateFromDistances(horizontalDistance, verticalDistance);
        }
        normal.Focus = tripodPosition;
        normal.CalculateNormal(projection);
        return offset;
    }

    private static int ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = Abs(unchecked(first.X - second.X)), middle = Abs(unchecked(first.Y - second.Y)), smallest = Abs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
        static int Abs(int value) => value < 0 ? unchecked(-value) : value;
    }
}
