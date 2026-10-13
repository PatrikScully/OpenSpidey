using Godot;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Game.Player;

public partial class PlayerFollowCamera : IPlayerCameraBehindHost
{
    private bool sourceTrackingInitialized;
    public CameraHeadingTransition HeadingTransition { get; } = new();
    public CameraTripodState Tripod { get; } = new();
    public FixedVector3 SourceTripodPosition => Tripod.Position;

    /// <summary>Called once after the real camera's initial orbit heading has been assigned.</summary>
    public void InitializeSourceTracking()
    {
        if (sourceTrackingInitialized) return;
        sourceTrackingInitialized = true;
        HeadingTransition.Heading = OrbitSourceHeading;
        HeadingTransition.PublishedHeading = HeadingTransition.Heading;
        Tripod.Position = SourcePosition;
    }

    /// <summary>The source camera's tripod/heading phase, before mode placement and display shake.</summary>
    public void AdvanceSourceTracking(int elapsedTicks)
    {
        InitializeSourceTracking();
        Tripod.Advance(Target.Controller.Position, Target.ActorState.IsDead,
            hook => (Target.Melee ?? throw new InvalidOperationException("A camera tripod hook requires the real posed player.")).GetPlayerHookPosition(hook), elapsedTicks);
        HeadingTransition.Advance(elapsedTicks, SourceControlMode);
        ApplySourceOrbitHeading();
    }

    public void SynchronizeSourceHeadingFromOrbit()
    {
        InitializeSourceTracking();
        HeadingTransition.Set(OrbitSourceHeading, 0, SourceControlMode);
        ScriptRig.Heading = HeadingTransition.Heading;
    }

    public void SetSourceFocusHook(int sourceIndex)
    {
        if (sourceIndex != -1)
        {
            _ = Target.Melee ?? throw new InvalidOperationException("Camera focus hooks require the original player rig.");
            // Evaluate the genuine pose only in the camera's next source update, matching field12C's write.
            if ((uint)sourceIndex >= 9) throw new ArgumentOutOfRangeException(nameof(sourceIndex));
        }
        Tripod.HookIndex = FocusHookIndex = sourceIndex;
    }

    public void SourcePutBehind(int duration)
    {
        InitializeSourceTracking();
        PlayerCombatState combat = Target.Combat.State;
        PlayerMovementInputState input = Target.Controller.MovementInput;
        PlayerCameraBehind.Apply(new(ScriptRig.AngleLocked, combat.Field8E8 != 0,
            Target.Controller.Orientation.GetEffectiveHeading(Target.Controller.OnWall), Target.Controller.Orientation.Up,
            SourceControlMode, input.Axes.Forward, input.Axes.Sideways, combat.ActionState, input.Heading,
            ScriptRig.Profiles.Get(CameraSurface.Wall, CameraParameter.VerticalDistance)), duration, this);
    }

    public void SetCameraAngle(short heading, ushort duration)
    {
        InitializeSourceTracking();
        HeadingTransition.Set(heading, duration, SourceControlMode);
        ApplySourceOrbitHeading();
    }
    public void SetCameraVerticalDistance(short distance, ushort duration) =>
        ScriptRig.Parameter(CameraParameter.VerticalDistance).Set(distance, duration, ScriptRig.DistanceInterpolationEnabled);

    public void SetTripodMotion(FixedVector3 target, uint duration)
    {
        InitializeSourceTracking();
        Tripod.SetMotion(target, duration, Target.Controller.Position, Target.ActorState.IsDead);
    }

    public void SetFloorCamera(int duration)
    {
        if (SourceControlMode != 3) return;
        foreach (CameraParameter parameter in new[] { CameraParameter.OffsetX, CameraParameter.OffsetY,
            CameraParameter.OffsetZ, CameraParameter.HorizontalDistance, CameraParameter.VerticalDistance })
        {
            short value = ScriptRig.Profiles.Get(CameraSurface.Floor, parameter);
            int target = parameter == CameraParameter.HorizontalDistance ? unchecked((ushort)value) : value;
            ScriptRig.Parameter(parameter).Set(target, unchecked((ushort)duration), ScriptRig.DistanceInterpolationEnabled);
        }
        Target.Combat.State.FloorCameraState = 0;
    }

    private void ApplySourceOrbitHeading()
    {
        yaw = (2048 - HeadingTransition.Heading) * (Mathf.Tau / 4096);
        ScriptRig.Heading = HeadingTransition.Heading;
    }
}
