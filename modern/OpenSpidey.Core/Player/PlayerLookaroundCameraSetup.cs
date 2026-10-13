using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerLookaroundCameraTrace(bool Hit, int Distance, int Length, FixedVector3 Position);
public readonly record struct PlayerLookaroundCameraSetupResult(bool SelectTarget, FixedVector3 Anchor, FixedJointTransform RawLookRotation);

public interface IPlayerLookaroundCameraSetupHost
{
    short EffectiveHeading { get; }
    void SetTargetVisible(bool visible);
    void DeleteTransitionPath(FixedQuaternion[] path);
    void SetDesiredCameraRotation(FixedQuaternion rotation);
    void SetCameraPosition(FixedVector3 position);
    PlayerLookaroundCameraTrace TraceWorld(FixedVector3 start, FixedVector3 end);
}

/// <summary>Actual SetupLookaroundCamera through its original anchor/clearance grid, before the separately owned target/action-selection slice.</summary>
public static class PlayerLookaroundCameraSetup
{
    public static PlayerLookaroundCameraSetupResult Step(PlayerLookaroundTransitionState state, FixedVector3 playerPosition,
        FixedJointTransform bodyRotation, bool ceiling, int elapsedTicks, IPlayerLookaroundCameraSetupHost host)
    {
        PlayerLookaroundState angles = state.Angles;
        if (!angles.Active) return default;
        host.SetTargetVisible(false);
        // On the terminal exit tick the original returns before GetEffectiveHeading.
        bool finishesExit = angles.EntranceTicks <= elapsedTicks && angles.ExitTicks != 0 && angles.ExitTicks <= elapsedTicks;
        short heading = finishesExit ? angles.PreviousHeading : host.EffectiveHeading;
        if (!PlayerLookaroundAngles.Advance(angles, heading, ceiling, elapsedTicks))
        {
            if (state.Path is { } dead) { host.DeleteTransitionPath(dead); state.Path = null; }
            return default;
        }
        FixedJointTransform matrix = bodyRotation;
        int transitionRemaining = angles.EntranceTicks != 0 ? angles.EntranceTicks : angles.ExitTicks;
        if (transitionRemaining != 0)
        {
            FixedQuaternion[] path = state.Path ?? throw new InvalidOperationException("Active lookaround transition requires its original quaternion path.");
            matrix = path[24 - transitionRemaining].ToMatrix();
        }
        FixedJointTransform raw = FixedJointTransform.FromJoint(new(unchecked((short)angles.Pitch), unchecked((short)angles.Yaw), 0, 0, 0, 0));
        if (angles.ExitTicks == 0)
        {
            FixedJointTransform smooth = FixedJointTransform.FromJoint(new(unchecked((short)angles.SmoothedPitch), unchecked((short)angles.SmoothedYaw), 0, 0, 0, 0));
            matrix = MultiplyRotation(matrix, smooth);
            state.CurrentLookRotation = FixedQuaternion.FromMatrix(matrix);
        }
        matrix = PlayerLookaroundTransition.FlipCameraColumns(matrix);
        host.SetDesiredCameraRotation(FixedQuaternion.FromMatrix(matrix));
        FixedVector3 anchor = state.Anchor = playerPosition + state.Offset;
        PlayerLookaroundCameraTrace adjust = host.TraceWorld(playerPosition, anchor);
        if (adjust.Hit)
            anchor = adjust.Distance < 16 ? playerPosition : playerPosition + state.Offset * (unchecked(3 * adjust.Distance) / 4) / adjust.Length;
        FixedVector3 cameraPosition;
        if (angles.EntranceTicks != 0)
            cameraPosition = state.EntryCameraPosition +
                (anchor + new FixedVector3(bodyRotation.M02, bodyRotation.M12, bodyRotation.M22) * 512 - state.EntryCameraPosition) * (24 - angles.EntranceTicks) / 24;
        else if (angles.ExitTicks != 0)
            cameraPosition = state.LastCameraPosition + (state.ExitCameraPosition - state.LastCameraPosition) * (24 - angles.ExitTicks) / 24;
        else cameraPosition = anchor - new FixedVector3(matrix.M02, matrix.M12, matrix.M22) * 512;
        FixedVector3 right = new(matrix.M00, matrix.M10, matrix.M20), forward = new(matrix.M02, matrix.M12, matrix.M22);
        PlayerLookaroundCameraTrace first = host.TraceWorld(anchor, cameraPosition);
        int stepsLeft = first.Length;
        if (first.Hit) { cameraPosition = first.Position + forward * 16; stepsLeft = unchecked(stepsLeft - 16); }
        while (true)
        {
            FixedVector3 lateralEnd = cameraPosition + right * 16;
            if (host.TraceWorld(cameraPosition, lateralEnd).Hit)
            {
                stepsLeft = unchecked(stepsLeft - 8);
                if (stepsLeft <= 16) break;
                cameraPosition += forward * 8; continue;
            }
            if (!host.TraceWorld(cameraPosition, lateralEnd - right * 32).Hit) break;
            stepsLeft = unchecked(stepsLeft - 8);
            if (stepsLeft <= 16) break;
            cameraPosition += forward * 8;
        }
        if (angles.ExitTicks == 0) state.LastCameraPosition = cameraPosition;
        host.SetCameraPosition(cameraPosition);
        return new(angles.EntranceTicks == 0 && angles.ExitTicks == 0, anchor, raw);
    }

    public static void Exit(PlayerLookaroundTransitionState state, IPlayerLookaroundExitHost host)
    {
        if (!state.Angles.Active) return;
        state.Angles.ClearAnglesOnExit();
        if (state.Path is { } dead) { host.DeleteTransitionPath(dead); state.Path = null; }
        host.PopCameraMode(); host.PutCameraBehind(0); state.ReticleType = 0; host.SetTargetVisible(false);
    }

    public static FixedJointTransform MultiplyRotation(FixedJointTransform left, FixedJointTransform right)
    {
        short Dot(short x, short y, short z, short a, short b, short c) => unchecked((short)(unchecked(x * a + y * b + z * c) >> 12));
        return new(Dot(left.M00, left.M01, left.M02, right.M00, right.M10, right.M20), Dot(left.M00, left.M01, left.M02, right.M01, right.M11, right.M21), Dot(left.M00, left.M01, left.M02, right.M02, right.M12, right.M22),
            Dot(left.M10, left.M11, left.M12, right.M00, right.M10, right.M20), Dot(left.M10, left.M11, left.M12, right.M01, right.M11, right.M21), Dot(left.M10, left.M11, left.M12, right.M02, right.M12, right.M22),
            Dot(left.M20, left.M21, left.M22, right.M00, right.M10, right.M20), Dot(left.M20, left.M21, left.M22, right.M01, right.M11, right.M21), Dot(left.M20, left.M21, left.M22, right.M02, right.M12, right.M22), left.X, left.Y, left.Z);
    }

    /// <summary>InitLineInfo narrows each whole-unit axis to signed16 before computing its original wrapping squared length.</summary>
    public static int SourceLineLength(FixedVector3 start, FixedVector3 end)
    {
        FixedVector3 delta = end - start;
        int x = unchecked((short)(delta.X >> 12)), y = unchecked((short)(delta.Y >> 12)), z = unchecked((short)(delta.Z >> 12));
        int horizontal = unchecked(x * x + z * z);
        return horizontal == 0 ? Math.Abs(y) : FixedVectorMath.OriginalSquareRoot(unchecked(horizontal + y * y));
    }
}
