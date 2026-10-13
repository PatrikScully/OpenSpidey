using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Scripted quaternion transition followed by the recovered per-update camera damping.</summary>
public sealed class FixedCameraRotation
{
    private FixedQuaternion start = FixedQuaternion.Identity;
    private int duration;
    private bool initialSwitch;
    public FixedQuaternion Value { get; private set; } = FixedQuaternion.Identity;
    public FixedQuaternion Desired { get; private set; } = FixedQuaternion.Identity;
    public FixedQuaternion Target { get; private set; } = FixedQuaternion.Identity;
    public int RemainingTicks { get; private set; }

    public void Set(FixedQuaternion current, FixedQuaternion target, ushort ticks)
    {
        Value = current;
        Target = target;
        RemainingTicks = ticks;
        initialSwitch = true;
        if (ticks != 0) { duration = ticks; start = current; }
    }

    public void Advance(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        int remaining = RemainingTicks;
        if (remaining != 0) remaining = unchecked(remaining - ticks);
        Desired = remaining > 0
            ? FixedQuaternion.Slerp(ref start, Target, unchecked((duration - remaining) << 12) / duration)
            : Target;
        // The recovered quaternion mode retains an overshot negative timer rather than clamping it.
        RemainingTicks = remaining;
        if (initialSwitch)
        {
            initialSwitch = false;
            if (remaining == 0) Value = Desired;
        }
        FixedQuaternion previous = Value;
        Value = FixedQuaternion.Slerp(ref previous, Desired, 1023);
    }
}
