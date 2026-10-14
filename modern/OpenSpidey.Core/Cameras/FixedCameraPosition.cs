using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Fixed-camera movement from the recovered setter and CM_FixedPos update.</summary>
public sealed class FixedCameraPosition
{
    public FixedVector3 Value { get; private set; }
    /// <summary>Source0x24C, also the CM_Normal/CM_Boss3 desired position.</summary>
    public FixedVector3 Target { get; internal set; }
    public FixedVector3 Rate { get; private set; }
    public int RemainingTicks { get; internal set; }
    /// <summary>Source byte0x2AC: every fixed setter raises it; CM_FixedPos consumes it on its first update.</summary>
    public bool InitialSwitch { get; internal set; }

    public void Set(FixedVector3 current, FixedVector3 target, ushort duration)
    {
        Value = current;
        Target = target;
        RemainingTicks = duration;
        InitialSwitch = true;
        if (duration != 0) Rate = (target - current) / duration;
    }

    /// <summary>SetFixedFocusMode writes the shared timer0x2BC and switch byte without a position target.</summary>
    internal void SetTimer(ushort duration)
    {
        RemainingTicks = duration;
        InitialSwitch = true;
    }

    internal void Synchronize(FixedVector3 current) => Value = current;

    /// <summary>CM_FixedPosAngles0x418800 position half: reads timer0x2BC without consuming it.</summary>
    internal void AdvanceWithoutTimer(int ticks)
    {
        if (RemainingTicks != 0 && unchecked(RemainingTicks - ticks) > 0) Value += Rate * ticks;
        else Value = Target;
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
