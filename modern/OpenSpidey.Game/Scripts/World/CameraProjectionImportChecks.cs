using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Game.Player;

namespace OpenSpidey.Game.World;

internal static class CameraProjectionImportChecks
{
    public static async Task VerifyAsync(Node owner, PlayerFollowCamera camera)
    {
        VerifyProjection(camera);
        camera.ScriptRig.SetZoom(1000, 4);
        for (int frame = 0; frame < 8 && camera.ScriptRig.Zoom.RemainingTicks != 0; frame++)
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        // PhysicsFrame is emitted before node processing; wait through the final camera update.
        await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.PhysicsFrame);
        if (camera.ScriptRig.Zoom.Value != 1000 || camera.ScriptRig.Zoom.RemainingTicks != 0)
            throw new InvalidDataException("The native camera did not complete its timed script zoom.");
        VerifyProjection(camera);
        camera.ScriptRig.SetZoom(CameraProjection.InitialZoom, 0);
        GD.Print("Verified Godot camera projection: original vertical framing, viewport aspect and timed script zoom.");
    }

    private static void VerifyProjection(PlayerFollowCamera camera)
    {
        Vector2 size = camera.GetViewport().GetVisibleRect().Size;
        Projection projection = camera.GetCameraProjection();
        double verticalScale = 4096.0 * CameraProjection.OriginalAspect / camera.ScriptRig.Zoom.Value;
        double horizontalScale = verticalScale * size.Y / size.X;
        if (camera.KeepAspect != Camera3D.KeepAspectEnum.Height ||
            Math.Abs(projection.Y.Y - verticalScale) > 0.0001 || Math.Abs(projection.X.X - horizontalScale) > 0.0001 ||
            Math.Abs(camera.Fov - CameraProjection.VerticalFieldOfView(camera.ScriptRig.Zoom.Value)) > 0.0001)
            throw new InvalidDataException("Native camera projection changed vertical framing or distorted the viewport aspect.");
    }
}
