using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scorpion;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Actors;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera
{
    internal short EncounterCameraAngle => ScriptRig.ModeState.ViewHeading;
    internal void SetEncounterCameraAngle(short angle, ushort ticks) => SetCameraAngle(angle, ticks);
    internal void SetEncounterCameraXOffset(short offset, ushort ticks) =>
        ScriptRig.Parameter(CameraParameter.OffsetX).Set(offset, ticks, ScriptRig.DistanceInterpolationEnabled);
    internal void SetEncounterCameraYOffset(short offset, ushort ticks) => SetCameraVerticalDistance(offset, ticks);
    internal void SetEncounterCameraZOffset(short offset, ushort ticks) =>
        ScriptRig.Parameter(CameraParameter.OffsetZ).Set(offset, ticks, ScriptRig.DistanceInterpolationEnabled);
    internal void SetEncounterHorizontalDistance(ushort distance, ushort ticks) =>
        ScriptRig.Parameter(CameraParameter.HorizontalDistance).Set(distance, ticks, ScriptRig.DistanceInterpolationEnabled);

    internal void TrackScorpionDeathView(FixedVector3 position, ActorAngles angles)
    {
        InitializeSourceTracking();
        ScriptRig.ModeState.FocusPosition = position;
        SetCameraAngle(angles.Y, 0);
        Ai.Focus.Target = new CameraFocusTarget(this, () => position);
        SetTripodMotion(position, 0);
    }

    internal void TrackJonahEncounterView(JonahState jonah, FixedVector3 position, short heading)
    {
        InitializeSourceTracking();
        ScriptRig.ModeState.FocusPosition = position;
        SetCameraAngle(heading, 0);
        Ai.Focus.Target = new CameraFocusTarget(jonah, () => position);
        SetTripodMotion(position, 0);
    }
}
