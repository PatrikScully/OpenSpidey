using OpenSpidey.Core.Cameras;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    public CameraNormalProjectionCache? SourceProjectionCache { get; set; }
    public CameraNormalPlacementState SourceNormalPlacement { get; } = new();

    /// <summary>Actual416AA0: no time, tripod interpolation, offsets, terrain query, shake, or render phase.</summary>
    public void SetStartPosition()
    {
        if (SourceControlMode != 3) return;
        Ai.Projection = SourceProjectionCache ??
            throw new InvalidOperationException("Camera start placement requires the retained application normal-projection globals.");
        Ai.SetStartPosition(Target.Controller.Position);
        ApplySourcePose(ScriptRig.ModeState.CurrentRotation);
    }
}
