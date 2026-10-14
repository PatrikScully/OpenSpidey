using OpenSpidey.Core.Mathematics;
using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Cameras;

/// <summary>Actual CCamera mode snapshot and front-mode MoveToDesiredPos fields; PushMode stores one saved mode, not a stack.</summary>
public sealed class CameraModeState
{
    public uint Mode { get; set; } = 3;
    public uint SavedMode { get; private set; }
    public FixedVector3 Position { get; set; }
    public FixedVector3 SavedPosition { get; private set; }
    public FixedVector3 FocusPosition { get; set; }
    public AimAngles Angles { get; private set; }
    public short ViewHeading { get; set; }
    public FixedQuaternion CurrentRotation { get; set; }
    public FixedQuaternion DesiredRotation { get; set; }
    public FixedQuaternion PreviousRotation { get; private set; }
    public FixedQuaternion DisplayRotation { get; set; }
    public FixedQuaternion SavedRotation { get; private set; }
    public int AngleInterpolationTicks { get; set; }
    public byte ItemHidingEnabled { get; set; } // Source0xF9, independent of the placement collision result0x180.
    public byte CollisionFlag { get; set; }

    public void PushMode()
    {
        SavedMode = Mode;
        if (Mode is 4 or 5 or 6) { SavedPosition = Position; SavedRotation = CurrentRotation; }
    }
    public void PopMode()
    {
        if (SavedMode is 4 or 5 or 6) { Position = SavedPosition; CurrentRotation = SavedRotation; }
        Mode = SavedMode;
    }
    public uint SetMode(uint mode)
    {
        uint previous = Mode; Mode = mode;
        if (mode is 16 or 17) AngleInterpolationTicks = 0;
        return previous;
    }
    public void MoveFrontToDesiredPosition(bool postWaterEffect)
    {
        CollisionFlag = 0;
        if (postWaterEffect) return;
        if (Mode != 7) throw new InvalidOperationException("This leaf owns only the actual front camera mode7 MoveToDesiredPos branch.");
        PreviousRotation = CurrentRotation;
        CurrentRotation = DisplayRotation = DesiredRotation;
    }
    public void CalculateAimAngles() => Angles = FixedAim.Calculate(Position, FocusPosition);
    public void LoadViewHeading(FixedJointTransform displayRotation)
    {
        if (displayRotation.M02 != 0 || displayRotation.M22 != 0)
            ViewHeading = unchecked((short)((-1024 - GameAngle.Atan2(displayRotation.M22, displayRotation.M02)) & 4095));
    }
}
