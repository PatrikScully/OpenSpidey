using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

public readonly record struct ScriptedCameraPose(FixedVector3 Position, FixedVector3 Focus, FixedVector3 Down);

/// <summary>Recovered camera positions and parameter state, independent of rendering and collision response.</summary>
public sealed class ScriptedCameraRig
{
    private readonly CameraParameterTransition[] parameters = [new(0, true), new(0, true), new(0, true), new(512, false), new(-128, false)];
    private bool angleLocked;
    private Func<bool>? readAngleLock;
    private Action<bool>? writeAngleLock;
    public CameraParameterProfiles Profiles { get; } = new();
    public CameraParameterTransition Zoom { get; } = new(CameraProjection.InitialZoom, false);
    public FixedCameraPosition FixedPosition { get; } = new();
    public FixedCameraRotation FixedOrientation { get; }
    public CameraModeState ModeState { get; } = new();
    public CameraFixedFocus FixedFocus { get; } = new();
    /// <summary>Field236 and the shared SetCamAngle globals; one owner for the script, native and AI readers.</summary>
    public CameraHeadingTransition HeadingTransition { get; } = new();
    public bool Enabled { get; private set; }
    public ushort Mode => checked((ushort)ModeState.Mode);
    public bool AngleLocked => readAngleLock?.Invoke() ?? angleLocked;
    public bool DistanceInterpolationEnabled { get; set; } = true;
    public OpenSpidey.Core.UI.CinematicScreenState CinematicScreen { get; set; } = new();
    public ushort WideScreenScriptValue => unchecked((ushort)CinematicScreen.WideScreenValue);
    public short Heading { get => HeadingTransition.Heading; set => HeadingTransition.Heading = value; }
    public CameraParameterTransition Parameter(CameraParameter parameter) => parameters[(int)parameter];
    /// <summary>Source GetHorizontalDistance4179E0 and GetVerticalDistance417A60: the low word of the current value.</summary>
    public ushort HorizontalDistance => unchecked((ushort)parameters[3].Value);
    public short VerticalDistance => unchecked((short)parameters[4].Value);
    /// <summary>Source SetHorizontalDistance4179F0 and SetVerticalDistance417A70; only the timer honours field23C.</summary>
    public void SetHorizontalDistance(ushort distance, ushort duration) => parameters[3].Set(distance, duration, DistanceInterpolationEnabled);
    public void SetVerticalDistance(short distance, ushort duration) => parameters[4].Set(distance, duration, DistanceInterpolationEnabled);

    public ScriptedCameraRig() => FixedOrientation = new(FixedPosition);

    /// <summary>Source SetMode4167F0 over the modes whose CCamera AI routines are ported; 16 and17 also stop the timed yaw.</summary>
    public void SetMode(ushort mode)
    {
        if (mode is not (3 or 4 or 5 or 6 or 7 or 8 or 15 or 16 or 17))
            throw new NotSupportedException($"Camera mode {mode} has no original CCamera AI routine in this port.");
        ModeState.SetMode(mode);
        if (mode is 16 or 17) HeadingTransition.RemainingTicks = 0;
        Enabled = true;
    }
    public void BindAngleLock(Func<bool> read, Action<bool> write)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(write);
        if (readAngleLock is not null) throw new InvalidOperationException("The source player angle lock already has an owner.");
        readAngleLock = read; writeAngleLock = write;
    }
    public void SetAngleLock(ushort value)
    {
        bool locked = value == 0;
        if (writeAngleLock is not null) writeAngleLock(locked);
        else angleLocked = locked;
    }
    public void SetWideScreenScriptValue(ushort value) => CinematicScreen.Set(value);
    public void SetFixedPosition(FixedVector3 current, FixedVector3 target, ushort duration)
    {
        FixedPosition.Set(current, target, duration);
        SetMode(4);
    }
    public void SetFixedPositionAndRotation(FixedVector3 current, FixedVector3 target,
        FixedQuaternion currentRotation, FixedQuaternion targetRotation, ushort duration)
    {
        FixedPosition.Set(current, target, duration);
        FixedOrientation.Set(currentRotation, targetRotation, duration);
        SetMode(5);
    }
    /// <summary>Source4166C0 (script188): writes mode6 directly, the shared fixed timer/switch and a third of the authored distance.</summary>
    public void SetFixedFocus(FixedVector3 target, ushort distance, ushort duration)
    {
        ModeState.Mode = 6;
        FixedPosition.SetTimer(duration);
        FixedFocus.Target = target;
        FixedFocus.Distance = unchecked((short)(distance / 3));
        Enabled = true;
    }
    public void SetZoom(ushort value, ushort duration)
    {
        ArgumentOutOfRangeException.ThrowIfZero(value);
        Zoom.Set(value, duration);
    }
    public void SetPlayerValue(CameraSurface surface, CameraParameter parameter, short value, ushort duration, bool apply, CameraPlayerState player)
    {
        if (!Profiles.Set(surface, parameter, value, apply, player)) return;
        if (Mode is 4 or 5 && parameter <= CameraParameter.OffsetZ) return;
        int target = parameter == CameraParameter.HorizontalDistance ? unchecked((ushort)value) : value;
        parameters[(int)parameter].Set(target, duration, DistanceInterpolationEnabled);
    }
    public void Advance(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        AdvanceParameters(ticks);
        if (Enabled && Mode == 4) FixedPosition.Advance(ticks);
        if (Enabled && Mode == 5) { FixedPosition.AdvanceWithoutTimer(ticks); FixedOrientation.Advance(ticks); }
    }
    /// <summary>The AI's zoom and SetCam* scalar timers, before the offset globals are rebuilt.</summary>
    public void AdvanceParameters(int ticks)
    {
        Zoom.Advance(ticks);
        foreach (var parameter in parameters) parameter.Advance(ticks);
    }
    public ScriptedCameraPose DesiredPose(FixedVector3 tripod)
    {
        FixedVector3 offset = Offset;
        if (Mode == 4) return new(FixedPosition.Value, tripod + offset, new(0, 4096, 0));
        if (Mode == 5)
        {
            FixedJointTransform matrix = FixedOrientation.Value.ToMatrix();
            return new(FixedPosition.Value, FixedPosition.Value + new FixedVector3(matrix.M02, matrix.M12, matrix.M22),
                new(matrix.M01, matrix.M11, matrix.M21));
        }
        int horizontal = parameters[3].Value, vertical = parameters[4].Value;
        int distance = FixedVectorMath.OriginalSquareRoot(unchecked(horizontal * horizontal + vertical * vertical));
        int look = GameAngle.Atan2(-vertical, horizontal);
        (short sine, short cosine) = GameAngle.Heading(look);
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(0, Heading, 0, 0, 0, 0));
        FixedVector3 forward = FixedRotation.Transform(rotation, new(0, -sine, cosine));
        FixedVector3 up = FixedRotation.Transform(rotation, new(0, cosine, sine));
        return new(tripod + offset + forward * distance, tripod, up);
    }

    /// <summary>Source camera offset globals56F260/264/268, rotated by the current heading.</summary>
    public FixedVector3 Offset
    {
        get
        {
            (short yawSine, short yawCosine) = GameAngle.Heading(Heading);
            int x = parameters[0].Value, y = parameters[1].Value, z = parameters[2].Value;
            return new FixedVector3(unchecked(-(yawCosine * x + yawSine * z)), unchecked(y << 12),
                unchecked(yawSine * x - yawCosine * z));
        }
    }
}
