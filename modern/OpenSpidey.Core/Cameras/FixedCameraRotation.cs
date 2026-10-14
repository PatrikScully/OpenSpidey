using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Scripted quaternion transition followed by the recovered per-update camera damping.</summary>
public sealed class FixedCameraRotation
{
    private readonly FixedCameraPosition? shared;
    private FixedQuaternion start = FixedQuaternion.Identity;
    private int duration;
    private int remainingTicks;
    private bool initialSwitch;

    public FixedCameraRotation() { }

    /// <summary>The live camera has one timer0x2BC and one switch byte0x2AC for both fixed modes.</summary>
    public FixedCameraRotation(FixedCameraPosition position) => shared = position;

    public FixedQuaternion Value { get; private set; } = FixedQuaternion.Identity;
    public FixedQuaternion Desired { get; private set; } = FixedQuaternion.Identity;
    public FixedQuaternion Target { get; private set; } = FixedQuaternion.Identity;
    public int RemainingTicks
    {
        get => shared?.RemainingTicks ?? remainingTicks;
        private set { if (shared is null) remainingTicks = value; else shared.RemainingTicks = value; }
    }
    private bool InitialSwitch
    {
        get => shared?.InitialSwitch ?? initialSwitch;
        set { if (shared is null) initialSwitch = value; else shared.InitialSwitch = value; }
    }

    public void Set(FixedQuaternion current, FixedQuaternion target, ushort ticks)
    {
        Value = current;
        Target = target;
        RemainingTicks = ticks;
        InitialSwitch = true;
        if (ticks != 0) { duration = ticks; start = current; }
    }

    /// <summary>CM_FixedPosAngles0x418800 on the live camera; the following416B10 rotation exit applies the damping slerp.</summary>
    internal void AdvanceDesired(int ticks, CameraModeState camera)
    {
        Step(ticks);
        camera.DesiredRotation = Desired;
        if (InitialSwitch)
        {
            InitialSwitch = false;
            if (RemainingTicks == 0) camera.CurrentRotation = camera.PreviousRotation = Desired;
        }
    }

    internal void Synchronize(FixedQuaternion current) => Value = current;

    public void Advance(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        Step(ticks);
        if (InitialSwitch)
        {
            InitialSwitch = false;
            if (RemainingTicks == 0) Value = Desired;
        }
        FixedQuaternion previous = Value;
        Value = FixedQuaternion.Slerp(ref previous, Desired, 1023);
    }

    private void Step(int ticks)
    {
        int remaining = RemainingTicks;
        if (remaining != 0) remaining = unchecked(remaining - ticks);
        Desired = remaining > 0
            ? FixedQuaternion.Slerp(ref start, Target, unchecked((duration - remaining) << 12) / duration)
            : Target;
        // The recovered quaternion mode retains an overshot negative timer rather than clamping it.
        RemainingTicks = remaining;
    }
}
