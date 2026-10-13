using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Fixed-camera movement from the recovered setter and CM_FixedPos update.</summary>
public sealed class FixedCameraPosition
{
    public FixedVector3 Value { get; private set; }
    public FixedVector3 Target { get; private set; }
    public FixedVector3 Rate { get; private set; }
    public int RemainingTicks { get; private set; }

    public void Set(FixedVector3 current, FixedVector3 target, ushort duration)
    {
        Value = current;
        Target = target;
        RemainingTicks = duration;
        if (duration != 0) Rate = (target - current) / duration;
    }

    public void Advance(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        if (RemainingTicks <= ticks)
        {
            RemainingTicks = 0;
            Value = Target;
            return;
        }
        RemainingTicks -= ticks;
        Value += Rate * ticks;
    }
}
