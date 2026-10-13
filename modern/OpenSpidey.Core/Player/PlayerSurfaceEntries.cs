using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerSurfaceEntryContact(FixedVector3 Normal, int Distance, uint FaceFlags);

public interface IPlayerSurfaceEntryHost : IPlayerSurfaceTransitionHost
{
    void SetCameraAngle(short heading, ushort duration);
    void SetWallCamera(int duration);
    void SetCeilingCamera(int duration);
}

/// <summary>Original interior/exterior entry, input latches and action1000 recovery over the same line-normal/camera-hook owner as2000.</summary>
public static class PlayerSurfaceEntries
{
    public static void HandleControls(PlayerSurfaceTransitionState state, bool allowTransition)
    {
        PlayerCombatState combat = state.Melee.Combat;
        int input = state.Melee.Input.Sideways;
        if (combat.Field8E8 != 0)
        {
            if (state.LineNormal.Y > 3400)
            {
                if ((state.CeilingToWallMotion == 0 && input > 0) || (state.CeilingToWallMotion != 0 && input < 0))
                {
                    state.WallMotionRequested = 1; state.CeilingToWallMotion = 0;
                    return;
                }
                state.WallMotionRequested = 0;
                if (allowTransition) state.WallToCeilingMotion = 1;
                state.CeilingToWallMotion = 0;
            }
            else if (state.LineNormal.Y < -2600) state.CeilingToWallMotion = 0;
            return;
        }
        if (combat.Field8E9 == 0 || state.LineNormal.Y > 3400) return;
        if ((state.WallToCeilingMotion == 0 && input < 0) || (state.WallToCeilingMotion != 0 && input > 0))
        {
            state.CeilingToWallInputRequested = 1;
            if (allowTransition)
            {
                state.CeilingToWallMotion = 1; state.WallToCeilingMotion = 0;
                return;
            }
        }
        else state.CeilingToWallInputRequested = 0;
        state.WallToCeilingMotion = 0;
    }

    public static bool CheckInterior(PlayerSurfaceTransitionState state, PlayerSurfaceEntryContact? contact,
        bool hardSurface, IPlayerSurfaceEntryHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        if (contact is not { } hit || state.ExteriorCornerDetected != 0 || !combat.IsCrawling || hit.Distance < 0) return false;
        FixedVector3 normal = hit.Normal;
        bool aligned = Alignment(normal, combat.CrawlSurfaceNormal) >= 1567;
        bool alternative = hardSurface || (hit.FaceFlags & 0x1000000) != 0;
        bool toFloor = false, toWall = false, toCeiling = false;
        ushort animation = aligned ? (ushort)72 : (ushort)60;
        if (combat.Field8E8 != 0)
        {
            HandleControls(state, false);
            toCeiling = normal.Y > 3400; toFloor = normal.Y < -2600;
        }
        else if (combat.Field8E9 != 0)
        {
            HandleControls(state, false); toWall = normal.Y <= 3400;
        }
        else toWall = normal.Y is >= -2600 and <= 3400;
        if (alternative) animation = aligned ? (ushort)78 : (ushort)66;
        if (toFloor)
        {
            host.OrientToNormal(true, new(0, -4096, 0));
            animation = hardSurface ? (ushort)88 : (ushort)87;
        }
        else if (!aligned)
        {
            int side = Side(normal, host.RightDirection);
            if (side > 2048) animation = alternative ? (ushort)68 : (ushort)62;
            else if (side < -2048) animation = alternative ? (ushort)67 : (ushort)61;
        }
        state.ApplyHookPosition = 0;
        int frames = host.AnimationFrameCount(animation) & 0xFFFF;
        if (combat.CameraAngleLock == 0 && !toFloor && !toWall && !toCeiling) SetCameraAngle(normal, frames, host);
        PoseCameraTripod(animation, frames, host);
        if (toFloor) host.SetFloorCamera(16);
        else if (toWall) host.SetWallCamera(16);
        else if (toCeiling) host.SetCeilingCamera(16);
        combat.ActionState = 0x1000;
        host.SelectAndResetAnimationSounds(animation); host.RunAnimation(animation);
        combat.HeadingTurnTicksRemaining = 0; combat.FieldAE5 = 0;
        return true;
    }

    public static bool CheckExterior(PlayerSurfaceTransitionState state, PlayerSurfaceEntryContact? contact,
        bool hardSurface, IPlayerSurfaceEntryHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        if (contact is not { } hit || state.ExteriorCornerDetected == 0 || !combat.IsCrawling) return false;
        FixedVector3 normal = hit.Normal;
        bool aligned = Alignment(normal, combat.CrawlSurfaceNormal) >= 1567;
        bool alternative = hardSurface || (hit.FaceFlags & 0x1000000) != 0;
        ushort animation = aligned ? (ushort)75 : (ushort)63;
        if (alternative) animation = aligned ? (ushort)81 : (ushort)69;
        if (combat.Field8E8 != 0 && normal.Y < -2600)
        {
            host.OrientToNormal(true, new(0, 4096, 0));
            animation = alternative ? (ushort)86 : (ushort)59;
        }
        else
        {
            int side = Side(normal, host.RightDirection);
            if (side > 2048) animation = alternative ? (ushort)70 : (ushort)64;
            else if (side < -2048) animation = alternative ? (ushort)71 : (ushort)65;
        }
        host.SelectAndResetAnimationSounds(animation);
        int frames = host.AnimationFrameCount(animation) & 0xFFFF;
        PoseCameraTripod(animation, frames, host, resetSounds: false);
        int input = state.Melee.Input.Sideways;
        if (combat.Field8E8 != 0)
        {
            if (normal.Y > 3400)
            {
                state.WallMotionRequested = (state.CeilingToWallMotion == 0 && input > 0) ||
                    (state.CeilingToWallMotion != 0 && input < 0) ? (byte)1 : (byte)0;
                state.CeilingToWallMotion = 0; host.SetCeilingCamera(16);
            }
            else if (normal.Y < -2600) { state.CeilingToWallMotion = 0; host.SetFloorCamera(16); }
            else
            {
                state.ApplyHookPosition = 0;
                if (combat.CameraAngleLock == 0) SetCameraAngle(normal, frames, host);
            }
        }
        else if (combat.Field8E9 != 0)
        {
            if (normal.Y <= 3400)
            {
                state.ApplyHookPosition = 0;
                state.CeilingMotionRequested = (state.WallToCeilingMotion == 0 && input > 0) ||
                    (state.WallToCeilingMotion != 0 && input < 0) ? (byte)1 : (byte)0;
                state.WallToCeilingMotion = 0;
                if (combat.CameraAngleLock == 0) SetCameraAngle(normal, frames, host);
                host.SetWallCamera(16);
            }
        }
        else if (normal.Y <= 3400)
        {
            state.ApplyHookPosition = 0;
            if (combat.CameraAngleLock == 0) SetCameraAngle(normal, frames, host);
            host.SetWallCamera(16);
        }
        combat.ActionState = 0x2000;
        host.SelectAndResetAnimationSounds(animation); host.RunAnimation(animation);
        combat.HeadingTurnTicksRemaining = 0; combat.Velocity = default; combat.FieldAE5 = 0;
        return true;
    }

    public static void UpdateInterior(PlayerSurfaceTransitionState state, ushort completedAnimation, IPlayerSurfaceEntryHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (completedAnimation == ushort.MaxValue)
        {
            int remaining = state.InteriorSlideRemaining;
            if (remaining == 0) return;
            int step = remaining >= 8 ? 8 : remaining;
            combat.Position += combat.ForwardDirection * step;
            state.InteriorSlideRemaining = remaining >= 8 ? unchecked(remaining - 8) : 0;
            return;
        }
        int slide = completedAnimation switch { 87 => 12, 88 => 16, 60 => 66, _ => 55 };
        state.ApplyHookPosition = 1; combat.CrawlSurfaceNormal = state.LineNormal;
        if (state.LineNormal.Y < -2600)
        {
            combat.Field8E9 = combat.Field8E8 = 0; combat.IsCrawling = false;
            host.SwitchToStandMode();
            if (completedAnimation is not (87 or 88)) { host.PlayAnimation(20); combat.ActionState = 1; }
        }
        else
        {
            combat.ActionState = 16;
            if (state.LineNormal.Y > 3400)
            {
                if (state.WallMotionRequested == 0) state.WallToCeilingMotion = 1;
                combat.Field8E8 = 0; combat.Field8E9 = 1;
            }
            else
            {
                if (combat.Field8E8 == 0 && state.CeilingToWallInputRequested != 0) state.CeilingToWallMotion = 1;
                combat.Field8E8 = 1; combat.Field8E9 = 0;
            }
        }
        combat.Position = host.GetPlayerHookPosition(2);
        FixedVector3 reference = combat.UpDirection * -1;
        host.SetOrientationReference(reference); host.OrientToNormal(true, reference); host.LockTargetTorsoAngle();
        combat.Position += combat.UpDirection * slide;
    }

    public static void UpdateWallCompletion(PlayerSurfaceTransitionState state, bool finished, IPlayerSurfaceEntryHost host)
    {
        PlayerCombatState combat = state.Melee.Combat;
        combat.FieldEA6 = 0;
        if (!finished) return;
        combat.IsCrawling = true;
        combat.Position = host.GetPlayerHookPosition(2);
        FixedVector3 reference = new(0, 4096, 0);
        host.SetOrientationReference(reference); combat.CrawlSurfaceNormal = state.LineNormal;
        host.OrientToNormal(true, reference); host.LockTargetTorsoAngle(); host.SwitchToStandMode();
    }

    private static int Alignment(FixedVector3 normal, FixedVector3 direction) => unchecked(
        ((normal.X * direction.X) >> 12) + ((normal.Y * direction.Y) >> 12) + ((normal.Z * direction.Z) >> 12));
    private static int Side(FixedVector3 normal, FixedVector3 right) => unchecked(
        ((-(normal.X * right.X)) >> 12) + ((-(normal.Y * right.Y)) >> 12) + ((-(normal.Z * right.Z)) >> 12));
    private static void SetCameraAngle(FixedVector3 normal, int frames, IPlayerSurfaceEntryHost host) =>
        host.SetCameraAngle(unchecked((short)((1024 - GameAngle.Atan2(normal.Z, normal.X)) & 4095)), unchecked((ushort)(frames * 2)));
    private static void PoseCameraTripod(ushort animation, int frames, IPlayerSurfaceEntryHost host, bool resetSounds = true)
    {
        if (resetSounds) host.SelectAndResetAnimationSounds(animation);
        host.RunAnimation(animation);
        host.SetAnimationFrame(unchecked((short)(frames - 1))); host.ApplyPlayerPose();
        host.SetTripodMotion(host.GetPlayerHookPosition(2), unchecked((uint)(frames * 2)));
    }
}
