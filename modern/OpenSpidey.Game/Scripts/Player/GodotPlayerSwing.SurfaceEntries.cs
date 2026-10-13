using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

public sealed partial class GodotPlayerSwing : IPlayerSurfaceEntryHost
{
    public void SetCameraAngle(short heading, ushort duration) => camera.SetCameraAngle(heading, duration);
    public void SetWallCamera(int duration) => camera.SetWallCamera(duration);
    public void SetCeilingCamera(int duration) => camera.SetCeilingCamera(duration);
}
