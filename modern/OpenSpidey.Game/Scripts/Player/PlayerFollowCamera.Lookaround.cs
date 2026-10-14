using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    public CameraModeState LookaroundMode => ScriptRig.ModeState;
    public bool LookaroundModeEnabled => LookaroundMode.Mode == 7;
    public ushort SourceControlMode => ScriptRig.Mode;

    public void PushLookaroundMode()
    {
        SynchronizeFromNode();
        LookaroundMode.PushMode();
    }
    public void SetFrontLookaroundMode()
    {
        LookaroundMode.SetMode(7);
    }
    public void PopLookaroundMode()
    {
        LookaroundMode.PopMode();
        ScriptRig.SetMode(checked((ushort)LookaroundMode.Mode));
        if (LookaroundMode.Mode is 4 or 5 or 6) ApplySourcePose(LookaroundMode.CurrentRotation);
    }
    public void SetSourceCameraMode(ushort mode)
    {
        // Source SetMode writes the current mode without modifying its saved PushMode slot.
        ScriptRig.SetMode(mode);
    }
    public void SetLookaroundDesiredRotation(FixedQuaternion rotation) => LookaroundMode.DesiredRotation = rotation;
    /// <summary>SetupLookaroundCamera writes camera mPos directly before the camera's own mode7 update.</summary>
    public void SetLookaroundPosition(FixedVector3 position)
    {
        SynchronizeFromNode();
        LookaroundMode.Position = position;
        ApplySourcePose(LookaroundMode.DisplayRotation);
    }
}
