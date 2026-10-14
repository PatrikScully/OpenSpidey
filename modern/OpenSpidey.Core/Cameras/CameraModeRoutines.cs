using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>SetFixedFocusMode's distance2E4 and focus2E8 for CM_FixedFocus.</summary>
public sealed class CameraFixedFocus
{
    public short Distance { get; set; }
    public FixedVector3 Target { get; set; }
}

/// <summary>The CCamera AI mode routines that only build a desired rotation: CM_FixedPos, CM_FixedFocus and CM_TripodFocus.</summary>
public static class CameraModeRoutines
{
    /// <summary>Whole4189A0 (mode4). Returns the yaw it writes to field236 on every update.</summary>
    public static short FixedPosition(CameraModeState camera, FixedCameraPosition fixedPosition, FixedVector3 offset, int elapsedTicks)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(fixedPosition);
        fixedPosition.Synchronize(camera.Position);
        fixedPosition.Advance(elapsedTicks);
        camera.Position = fixedPosition.Value;
        FixedVector3 forward = FixedVectorMath.Normalize((camera.FocusPosition - (camera.Position - offset)) >> 12);
        FixedVector3 right = Cross(new(0, 4096, 0), forward);
        FixedVector3 up = Cross(forward, right);
        camera.DesiredRotation = FixedQuaternion.FromMatrix(Columns(right, up, forward));
        if (fixedPosition.InitialSwitch)
        {
            fixedPosition.InitialSwitch = false;
            if (fixedPosition.RemainingTicks == 0) camera.CurrentRotation = camera.PreviousRotation = camera.DesiredRotation;
        }
        return Yaw(forward);
    }

    /// <summary>Whole418C40 (mode6): eases the retained projection distance by sixteen per tick, then aims tripod to2E8.</summary>
    public static short FixedFocus(CameraModeState camera, CameraFixedFocus focus, CameraNormalProjectionCache projection,
        FixedVector3 tripod, int elapsedTicks)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(focus);
        ArgumentNullException.ThrowIfNull(projection);
        int target = unchecked((ushort)focus.Distance);
        if (projection.Distance >= target)
        {
            if (projection.Distance > target)
            {
                projection.Distance = unchecked(projection.Distance - 16 * elapsedTicks);
                if (projection.Distance < target) projection.Distance = target;
            }
        }
        else
        {
            projection.Distance = unchecked(projection.Distance + 16 * elapsedTicks);
            if (projection.Distance > target) projection.Distance = target;
        }
        AimAngles aim = FixedAim.Calculate(tripod, focus.Target);
        FixedJointTransform matrix = SourceCameraEulerRotation.Matrix(new ActorAngles(aim.Pitch, aim.Yaw, 0));
        camera.DesiredRotation = FixedQuaternion.FromMatrix(matrix);
        return Yaw(new(matrix.M02, matrix.M12, matrix.M22));
    }

    /// <summary>Whole4186B0 (mode8): aims the eased tripod at focus144 and leaves field236 untouched.</summary>
    public static void TripodFocus(CameraModeState camera, FixedVector3 tripod)
    {
        ArgumentNullException.ThrowIfNull(camera);
        camera.DesiredRotation = SourceCameraEulerRotation.FromAngles(FixedAim.Calculate(tripod, camera.FocusPosition));
    }

    private static short Yaw(FixedVector3 back) => unchecked((short)((-1024 - GameAngle.Atan2(back.Z, back.X)) & 4095));

    private static FixedVector3 Cross(FixedVector3 a, FixedVector3 b) => new(unchecked(a.Y * b.Z - a.Z * b.Y) >> 12,
        unchecked(a.Z * b.X - a.X * b.Z) >> 12, unchecked(a.X * b.Y - a.Y * b.X) >> 12);

    private static FixedJointTransform Columns(FixedVector3 right, FixedVector3 up, FixedVector3 back) =>
        new(unchecked((short)right.X), unchecked((short)up.X), unchecked((short)back.X),
            unchecked((short)right.Y), unchecked((short)up.Y), unchecked((short)back.Y),
            unchecked((short)right.Z), unchecked((short)up.Z), unchecked((short)back.Z), 0, 0, 0);
}
