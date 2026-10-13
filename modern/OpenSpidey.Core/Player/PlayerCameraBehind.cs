using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerCameraBehindFrame(bool AngleLocked, bool OnWall, short EffectiveHeading,
    FixedVector3 UpDirection, uint CameraMode, sbyte AxisE2D, sbyte AxisE2E, uint Action, short InputHeadingE32, short WallVerticalDistance);

public interface IPlayerCameraBehindHost
{
    void SetCameraAngle(short heading, ushort duration);
    void SetCameraVerticalDistance(short distance, ushort duration);
}

/// <summary>Actual CPlayer::PutCameraBehind, including source wall input and profile adjustments.</summary>
public static class PlayerCameraBehind
{
    public static void Apply(PlayerCameraBehindFrame frame, int duration, IPlayerCameraBehindHost host)
    {
        if (frame.AngleLocked) return;
        ushort ticks = unchecked((ushort)duration);
        if (!frame.OnWall) { host.SetCameraAngle(frame.EffectiveHeading, ticks); return; }
        short heading = unchecked((short)((1024 - GameAngle.Atan2(frame.UpDirection.Z, frame.UpDirection.X)) & 4095));
        host.SetCameraAngle(heading, ticks);
        if (frame.CameraMode != 3) return;
        if ((frame.AxisE2D | frame.AxisE2E) != 0 && frame.Action == 16)
        {
            (short sine, short cosine) = GameAngle.Heading(frame.InputHeadingE32);
            host.SetCameraVerticalDistance(unchecked((short)(frame.WallVerticalDistance + ((500 * cosine) >> 12))), ticks);
            host.SetCameraAngle(unchecked((short)(heading + ((700 * sine) >> 12))), ticks);
        }
        else host.SetCameraVerticalDistance(frame.WallVerticalDistance, ticks);
    }
}
