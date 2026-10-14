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
    public FixedCameraRotation FixedOrientation { get; } = new();
    public CameraModeState ModeState { get; } = new();
    public bool Enabled { get; private set; }
    public ushort Mode => checked((ushort)ModeState.Mode);
    public bool AngleLocked => readAngleLock?.Invoke() ?? angleLocked;
    public bool DistanceInterpolationEnabled { get; set; } = true;
    public OpenSpidey.Core.UI.CinematicScreenState CinematicScreen { get; set; } = new();
    public ushort WideScreenScriptValue => unchecked((ushort)CinematicScreen.WideScreenValue);
    public short Heading { get; set; }
    public CameraParameterTransition Parameter(CameraParameter parameter) => parameters[(int)parameter];

    public void SetMode(ushort mode)
    {
        if (mode is not (3 or 4 or 5)) throw new NotSupportedException($"Camera mode {mode} has not been ported.");
        ModeState.SetMode(mode);
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
        foreach (var parameter in parameters) parameter.Advance(ticks);
        Zoom.Advance(ticks);
        if (Enabled && Mode is 4 or 5) FixedPosition.Advance(ticks);
        if (Enabled && Mode == 5) FixedOrientation.Advance(ticks);
    }
    public ScriptedCameraPose DesiredPose(FixedVector3 tripod)
    {
        FixedVector3 offset = CameraOffset();
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

    private FixedVector3 CameraOffset()
    {
        (short yawSine, short yawCosine) = GameAngle.Heading(Heading);
        int x = parameters[0].Value, y = parameters[1].Value, z = parameters[2].Value;
        return new FixedVector3(unchecked(-(yawCosine * x + yawSine * z)), unchecked(y << 12),
            unchecked(yawSine * x - yawCosine * z));
    }
}
