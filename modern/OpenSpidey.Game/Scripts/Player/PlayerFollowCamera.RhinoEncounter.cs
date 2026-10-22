using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    /// <summary>CRhino::DieRhino case0: CAMERAMODE_DEMO with mTripod/field_13C on the Rhino, zero offsets, XZ 0x1A8, Y -0x68.</summary>
    internal void BeginRhinoDeathView(object owner, Func<FixedVector3> position)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(position);
        ScriptRig.SetMode(3);
        InitializeSourceTracking();
        var rhino = new CameraFocusTarget(owner, position);
        playerBody = rhino;
        Ai.Focus.Target = rhino;
        foreach (CameraParameter offset in new[] { CameraParameter.OffsetX, CameraParameter.OffsetY, CameraParameter.OffsetZ })
            ScriptRig.Parameter(offset).Set(0, 0, ScriptRig.DistanceInterpolationEnabled);
        ScriptRig.SetHorizontalDistance(0x1A8, 0);
        ScriptRig.SetVerticalDistance(-0x68, 0);
    }

    /// <summary>CCamera field_236, read by DieRhino's orbit before each SetCamAngle.</summary>
    internal short RhinoDeathViewHeading => ScriptRig.ModeState.ViewHeading;
}
