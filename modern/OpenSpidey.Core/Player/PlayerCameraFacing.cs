using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>The real E70 camera handle, with existing AD6/201/551/56C/input owners retained by reference.</summary>
public sealed class PlayerCameraFacingState(PlayerMeleeState melee, PlayerSurfaceTransitionState surface, PlayerLookaroundState lookaround)
{
    public PlayerMeleeState Melee { get; } = melee;
    public PlayerSurfaceTransitionState Surface { get; } = surface;
    public PlayerLookaroundState Lookaround { get; } = lookaround;
    public PlayerKickHandle CameraTarget { get; set; }
}

public interface IPlayerCameraFacingHost
{
    short CameraHeading { get; }
    short CameraReferenceHeading { get; }
    short InputHeading { get; }
    short GetEffectiveHeading();
    FixedVector3? RecoverCameraTarget(ref PlayerKickHandle handle);
    void PutCameraBehind(int duration);
    void SetCameraAngle(short heading, ushort duration);
}

public interface IPlayerCameraFacingFrameHost
{
    short UpdateCameraFacing();
}

/// <summary>Original complete4B6E8C–4B70D5 camera-facing slice, before heading/gravity/velocity finalization.</summary>
public static class PlayerCameraFacing
{
    public static void Update(PlayerCameraFacingState state, IPlayerCameraFacingHost host)
    {
        PlayerMeleeState melee = state.Melee;
        PlayerCombatState combat = melee.Combat;
        if (state.Surface.ApplyHookPosition == 0 || combat.CameraAngleLock != 0 || combat.Field54C != 0) return;
        if (combat.Field8E8 != 0)
        {
            combat.Field551 = 0;
            if ((combat.ActionState & 0x3000) == 0) host.PutCameraBehind(30);
            return;
        }
        if (melee.Input.Pressed[5] != 0 && state.CameraTarget.Target is not null)
        {
            PlayerKickHandle handle = state.CameraTarget;
            FixedVector3? position = host.RecoverCameraTarget(ref handle);
            state.CameraTarget = handle;
            if (position is not { } target) throw new InvalidOperationException("The source camera-facing branch dereferences its recovered E70 target; its live caller must retain a valid handle.");
            host.SetCameraAngle(FixedAim.Calculate(combat.Position, target).Yaw, 0);
            combat.Field551 = 1;
            return;
        }
        if (combat.FieldEA6 != 0)
        {
            if (melee.Input.Triggered[4] != 0)
            {
                ResetCamera();
                combat.Field551 = 1;
            }
            if (combat.Field551 == 0) { host.PutCameraBehind(20); combat.Field551 = 1; }
            return;
        }
        combat.Field551 = 0;
        _ = host.GetEffectiveHeading();
        if (melee.Input.Triggered[4] != 0) { ResetCamera(); return; }
        if (melee.Input.Triggered[17] != 0 && !combat.LookaroundActive && (combat.ActionState & 0x2001C001) != 0)
        {
            melee.Input.Triggered[17] = melee.Input.Mirrors[17] = 0;
            host.PutCameraBehind(0);
            return;
        }
        if ((melee.Input.Sideways != 0 || melee.Input.Forward != 0) && combat.ActionState == 16)
        {
            short cameraHeading = host.CameraHeading;
            short wanted = unchecked((short)(host.InputHeading + cameraHeading));
            int delta = (cameraHeading - wanted) & 4095;
            if (delta is < 1200 or > 2896) host.SetCameraAngle(wanted, 60);
            else if (delta is > 2032 and < 2064) host.SetCameraAngle(host.CameraReferenceHeading, 0);
            else
            {
                int angle = ((delta - 1200) << 11) / 1696;
                (short sine, _) = GameAngle.Heading(angle);
                host.SetCameraAngle(wanted, unchecked((ushort)((sine * 128 >> 12) + 60)));
            }
        }
        else host.SetCameraAngle(host.CameraReferenceHeading, 0);

        void ResetCamera()
        {
            melee.Input.Triggered[4] = 0;
            combat.SwingCameraResetLatch = 0;
            host.PutCameraBehind(0);
            state.Lookaround.HoldTicks = 0;
        }
    }
}
