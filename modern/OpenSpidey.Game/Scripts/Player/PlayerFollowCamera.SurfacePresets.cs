using OpenSpidey.Core.Cameras;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    public void SetWallCamera(int duration) => SetSourceSurfacePreset(CameraSurface.Wall, duration, 1);
    public void SetCeilingCamera(int duration) => SetSourceSurfacePreset(CameraSurface.Ceiling, duration, 2);

    private void SetSourceSurfacePreset(CameraSurface surface, int duration, int sourceFloorCameraState)
    {
        if (SourceControlMode != 3) return;
        foreach (CameraParameter parameter in new[] { CameraParameter.OffsetX, CameraParameter.OffsetY,
            CameraParameter.OffsetZ, CameraParameter.HorizontalDistance, CameraParameter.VerticalDistance })
        {
            short value = ScriptRig.Profiles.Get(surface, parameter);
            int target = parameter == CameraParameter.HorizontalDistance ? unchecked((ushort)value) : value;
            ScriptRig.Parameter(parameter).Set(target, unchecked((ushort)duration), ScriptRig.DistanceInterpolationEnabled);
        }
        Target.Combat.State.FloorCameraState = sourceFloorCameraState;
    }
}
