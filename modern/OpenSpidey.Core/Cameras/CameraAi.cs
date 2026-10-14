using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Live bodies and services read by one CCamera AI update, beside the416B10 line query and player transport.</summary>
public interface ICameraAiHost : ICameraPlacementHost
{
    CameraFocusTarget? Tripod { get; } // Source mTripod0xFC.
    bool TripodDead { get; }
    FixedVector3 TripodHookPosition(int hook);
    CameraFocusTarget? Boss { get; } // Source gBossRelated0x56E998.
    uint TotalTime { get; } // Source G_TTIME0x60CFA8, read only while a shake is active.
}

/// <summary>
/// Whole original CCamera AI417CB0 after its debug free-camera, F8 and post-water gates: tripod, focus, zoom,
/// SetCam* timers, offset, yaw, mode routine, 416B10 placement, Utils_CalcAim, shake and LoadIntoMikeCamera416A20.
/// </summary>
public sealed class CameraAi
{
    public CameraAi(ScriptedCameraRig rig, CameraTripodState tripod, CameraNormalPlacementState normal, CameraShakeState shake)
    {
        Rig = rig ?? throw new ArgumentNullException(nameof(rig));
        Tripod = tripod ?? throw new ArgumentNullException(nameof(tripod));
        Normal = normal ?? throw new ArgumentNullException(nameof(normal));
        Shake = shake ?? throw new ArgumentNullException(nameof(shake));
        Focus = new(rig.ModeState);
    }

    public ScriptedCameraRig Rig { get; }
    public CameraModeState Mode => Rig.ModeState;
    public CameraHeadingTransition Heading => Rig.HeadingTransition;
    public CameraTripodState Tripod { get; }
    public CameraNormalPlacementState Normal { get; }
    public CameraShakeState Shake { get; }
    public CameraFocusMotion Focus { get; }
    public CameraBossMode Boss { get; } = new();
    /// <summary>The retained application globals548858/54885C, shared by every camera.</summary>
    public CameraNormalProjectionCache Projection { get; set; } = new();
    /// <summary>Source offset global56F260 as rebuilt by the last update, including CM_Boss3's cleared X/Z.</summary>
    public FixedVector3 Offset { get; private set; }
    /// <summary>The source416B10 item-hiding phase, run when field F9 is set. Null keeps its NotSupported guard.</summary>
    public Action? HideItems { get; set; }
    public Action<string>? Diagnostic { get; set; }

    /// <summary>Source SetStartPosition416AA0: tripod104 from the body, CM_Normal, then mPos and1E4 only.</summary>
    public void SetStartPosition(FixedVector3 tripodBody)
    {
        if (Mode.Mode != 3) return;
        Tripod.Position = tripodBody;
        Normal.Heading = Heading.Heading;
        Normal.SetStartPosition(3, tripodBody, Projection);
        Heading.Heading = Normal.Heading;
        Rig.FixedPosition.Target = Normal.DesiredPosition;
        Mode.Position = Normal.Position;
        Mode.CurrentRotation = Normal.Rotation;
    }

    public void Update(int elapsedTicks, ICameraAiHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        CameraFocusTarget? tripod = host.Tripod;
        Tripod.Advance(tripod?.Position, host.TripodDead, host.TripodHookPosition, elapsedTicks);
        Focus.Advance(elapsedTicks);
        Rig.AdvanceParameters(elapsedTicks);
        Offset = Rig.Offset;
        uint mode = Mode.Mode;
        Heading.Advance(elapsedTicks, mode);
        int horizontal = Rig.Parameter(CameraParameter.HorizontalDistance).Value;
        int vertical = Rig.Parameter(CameraParameter.VerticalDistance).Value;
        if (mode == 3) Projection.UpdateFromDistances(horizontal, vertical);
        Normal.DesiredPosition = Rig.FixedPosition.Target;
        switch (mode)
        {
            case 3:
                Normal.Heading = Heading.Heading;
                Normal.Focus = Tripod.Position;
                Normal.CalculateNormal(Projection);
                Heading.Heading = Normal.Heading;
                Mode.DesiredRotation = Normal.DesiredRotation;
                break;
            case 4:
                Normal.Focus = Mode.FocusPosition;
                Heading.Heading = CameraModeRoutines.FixedPosition(Mode, Rig.FixedPosition, Offset, elapsedTicks);
                break;
            case 5:
                Rig.FixedPosition.Synchronize(Mode.Position);
                Rig.FixedPosition.AdvanceWithoutTimer(elapsedTicks);
                Mode.Position = Rig.FixedPosition.Value;
                Rig.FixedOrientation.AdvanceDesired(elapsedTicks, Mode);
                break;
            case 6:
                Heading.Heading = CameraModeRoutines.FixedFocus(Mode, Rig.FixedFocus, Projection, Tripod.Position, elapsedTicks);
                break;
            case 7:
                break;
            case 8:
                CameraModeRoutines.TripodFocus(Mode, Tripod.Position);
                break;
            case 15 or 16 or 17:
                Normal.Heading = Heading.Heading;
                Offset = Boss.Calculate(Normal, Projection, Focus, tripod, host.Boss, Tripod.Position, Offset, horizontal, vertical);
                Heading.Heading = Normal.Heading;
                Mode.DesiredRotation = Normal.DesiredRotation;
                break;
            default:
                throw new NotSupportedException($"Camera mode {mode} has no original CCamera AI routine in this port.");
        }
        Rig.FixedPosition.Target = Normal.DesiredPosition;
        bool tripodPresent = tripod is not null;
        if (mode is 4 or 5) CameraPlacement.MoveFixed(Mode, tripodPresent, Tripod.TrackingEnabled, false, Diagnostic);
        else if (mode == 7)
        {
            if (!tripodPresent && Tripod.TrackingEnabled) Diagnostic?.Invoke("No valid tripod");
            Mode.MoveFrontToDesiredPosition(false);
        }
        else CameraPlacement.Move(Mode, Projection, Tripod.Position, Offset, Normal.Pitch, Normal.Roll, tripodPresent,
            Tripod.TrackingEnabled, false, host, HideItems, Diagnostic);
        if (mode == 5) Rig.FixedOrientation.Synchronize(Mode.CurrentRotation);
        Mode.CalculateAimAngles();
        if (Shake.Active) Mode.DisplayRotation = Shake.Advance(Mode.CurrentRotation, host.TotalTime);
        Mode.LoadViewHeading(Mode.DisplayRotation.ToMatrix());
    }
}
