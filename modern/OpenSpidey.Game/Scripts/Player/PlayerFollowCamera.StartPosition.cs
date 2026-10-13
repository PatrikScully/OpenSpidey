using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    public CameraNormalProjectionCache? SourceProjectionCache { get; set; }
    public CameraNormalPlacementState SourceNormalPlacement { get; } = new();

    /// <summary>Actual416AA0: no time, tripod interpolation, offsets, terrain query, shake, or render phase.</summary>
    public void SetStartPosition()
    {
        if (SourceControlMode != 3) return;
        CameraNormalProjectionCache cache = SourceProjectionCache ??
            throw new InvalidOperationException("Camera start placement requires the retained application normal-projection globals.");
        SourceNormalPlacement.Heading = HeadingTransition.Heading;
        // Source mTripod is the actual player body; field104 is the separately eased camera focus.
        Tripod.Position = Target.Controller.Position;
        SourceNormalPlacement.SetStartPosition(3, Target.Controller.Position, cache);
        ScriptRig.Heading = SourceNormalPlacement.Heading;
        GlobalPosition = PsxCoordinates.FixedWorldPosition(SourceNormalPlacement.Position);
        GlobalBasis = PsxCoordinates.CharacterTransform(SourceNormalPlacement.Rotation.ToMatrix()).Basis.Orthonormalized();
        shakeApplied = false;
    }

    /// <summary>The genuine normal-mode camera update refreshes these globals; setters and constructors do not.</summary>
    public void UpdateNormalProjectionCache()
    {
        if (SourceControlMode != 3 || SourceProjectionCache is not { } cache) return;
        cache.UpdateFromDistances(ScriptRig.Parameter(CameraParameter.HorizontalDistance).Value,
            ScriptRig.Parameter(CameraParameter.VerticalDistance).Value);
    }
}
