using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed class PlayerLookaroundTransitionState(PlayerLookaroundState angles)
{
    public PlayerLookaroundState Angles { get; } = angles ?? throw new ArgumentNullException(nameof(angles));
    public FixedVector3 Offset { get; set; }
    public FixedVector3 Anchor { get; set; }
    public FixedVector3 EntryCameraPosition { get; set; }
    public FixedQuaternion BodyRotation { get; set; }
    public FixedQuaternion CameraRotation { get; set; }
    public FixedQuaternion CurrentLookRotation { get; set; }
    public FixedVector3 LastCameraPosition { get; set; }
    public FixedVector3 ExitCameraPosition { get; set; }
    public byte ReticleType { get; set; }
    public FixedQuaternion[]? Path { get; set; }
}

public interface IPlayerLookaroundExitHost
{
    void DeleteTransitionPath(FixedQuaternion[] path);
    void PopCameraMode();
    void PutCameraBehind(int argument);
    void SetTargetVisible(bool visible);
}

public interface IPlayerLookaroundTransitionHost
{
    short EffectiveHeading { get; }
    FixedQuaternion CameraRotation { get; }
    FixedVector3 CameraPosition { get; }
    FixedVector3 GetPlayerHookPosition(int hook);
    void PushCameraMode();
    void SetCameraMode(ushort mode);
    FixedQuaternion[] AllocateTransitionPath(int count);
}

/// <summary>Actual PC EnterLookaroundMode, including camera-to-body quaternion path ordering and the source-mutated starting quaternion.</summary>
public static class PlayerLookaroundTransition
{
    public static bool Enter(PlayerLookaroundTransitionState transition, FixedJointTransform bodyRotation,
        FixedVector3 upDirection, bool wallProfile, bool ceilingProfile, IPlayerLookaroundTransitionHost host)
    {
        PlayerLookaroundState angles = transition.Angles;
        if (angles.ExitTicks != 0) return false;
        transition.Offset = upDirection * 128;
        angles.PreviousHeading = host.EffectiveHeading;
        transition.BodyRotation = FixedQuaternion.FromMatrix(bodyRotation);
        angles.Active = true; angles.HeadingTurnTicksRemaining = 0;
        transition.CameraRotation = FixedQuaternion.FromMatrix(FlipCameraColumns(host.CameraRotation.ToMatrix()));
        transition.EntryCameraPosition = host.CameraPosition;
        angles.EntranceTicks = 24; angles.ExitTicks = 0;
        angles.Pitch = wallProfile ? angles.WallPitch : ceilingProfile ? angles.CeilingPitch : angles.FloorPitch;
        transition.Anchor = host.GetPlayerHookPosition(8) + upDirection * 128;
        host.PushCameraMode(); host.SetCameraMode(7);
        FixedQuaternion[] path = host.AllocateTransitionPath(24);
        if (path.Length != 24) throw new InvalidDataException("Original lookaround transition requires exactly24 quaternion samples.");
        transition.Path = path;
        path[0] = transition.CameraRotation;
        for (int index = 1; index < 23; index++)
        {
            FixedQuaternion start = transition.CameraRotation;
            path[index] = FixedQuaternion.Slerp(ref start, transition.BodyRotation, index * 4096 / 23);
            transition.CameraRotation = start;
        }
        path[23] = transition.BodyRotation;
        return true;
    }

    public static FixedJointTransform FlipCameraColumns(FixedJointTransform matrix) => matrix with
    {
        M00 = unchecked((short)-matrix.M00), M10 = unchecked((short)-matrix.M10), M20 = unchecked((short)-matrix.M20),
        M02 = unchecked((short)-matrix.M02), M12 = unchecked((short)-matrix.M12), M22 = unchecked((short)-matrix.M22)
    };
}
