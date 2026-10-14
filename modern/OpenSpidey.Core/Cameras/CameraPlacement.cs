using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>One completed camera InitLineInfo/LineToItem(mode1) query with CameraCollisionCheck set.</summary>
public readonly record struct CameraLineHit(int Distance, FixedVector3 Position, short NormalX, short NormalY, short NormalZ);

public interface ICameraPlacementHost
{
    CameraLineHit? CameraLineToItem(FixedVector3 start, FixedVector3 end);
    bool PlayerActionLocked { get; } // Player byte0x8E8.
    bool PlayerMoving { get; } // Player velocity X|Z (0x60|0x68).
    bool PlayerHiddenWhenFar { get; } // Player byte0x57C.
    short PlayerEffectiveHeading();
    void HidePlayer();
    void ShowPlayer();
    void SetCameraAngle(short heading);
}

/// <summary>Original416B10 placement for camera modes3, 6, 8, 15, 16 and17, including the terrain pull-in branch.</summary>
public static class CameraPlacement
{
    public static void Move(CameraModeState camera, CameraNormalProjectionCache projection, FixedVector3 tripod, FixedVector3 offset,
        short pitch, short roll, bool tripodPresent, bool trackingEnabled, bool postWaterEffect, ICameraPlacementHost host,
        Action? hideItems, Action<string>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(host);
        if (camera.Mode is 4 or 5 or 7) throw new NotSupportedException("Camera modes4, 5 and7 use the separate rotation-only placement exit.");
        camera.CollisionFlag = 0;
        if (!tripodPresent && trackingEnabled) diagnostic?.Invoke("No valid tripod");
        if (postWaterEffect) return;
        FixedVector3 previousPosition = camera.Position;
        camera.InterpolateToDesiredRotation();
        FixedVector3 back = FixedRotation.Transform(camera.CurrentRotation.ToMatrix(), new(0, 0, unchecked(-projection.Distance)));
        FixedVector3 direction = FixedVectorMath.Normalize(back);
        back <<= 12;
        bool collided = camera.Mode is not (16 or 17) && PullIn(camera, projection, tripod, offset, pitch, roll, back, direction, host);
        camera.DisplayRotation = camera.CurrentRotation;
        if (!collided)
        {
            if (trackingEnabled)
            {
                int length = FixedVectorMath.OriginalDistance(back, default);
                camera.DesiredDistance = length;
                if (camera.SmoothedDistance != length)
                    camera.SmoothedDistance = unchecked(camera.SmoothedDistance + unchecked(length - camera.SmoothedDistance) / 8);
                camera.Position = offset + tripod + direction * camera.SmoothedDistance;
                if (camera.SmoothedDistance < 200) host.HidePlayer();
            }
            else camera.Position = tripod;
        }
        if (camera.ItemHidingEnabled != 0)
            (hideItems ?? throw new NotSupportedException("Source camera item hiding requires its real line-query and item-generation owners."))();
        camera.Velocity = camera.Position - previousPosition;
        camera.Field1A4 = 0;
    }

    /// <summary>The416B10 exit for fixed modes4 and5: damped rotation only, without velocity or the1A4 clear.</summary>
    public static void MoveFixed(CameraModeState camera, bool tripodPresent, bool trackingEnabled, bool postWaterEffect,
        Action<string>? diagnostic = null)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (camera.Mode is not (4 or 5)) throw new NotSupportedException("Only camera modes4 and5 use the fixed rotation-only placement exit.");
        camera.CollisionFlag = 0;
        if (!tripodPresent && trackingEnabled) diagnostic?.Invoke("No valid tripod");
        if (postWaterEffect) return;
        camera.InterpolateToDesiredRotation();
        camera.DisplayRotation = camera.CurrentRotation;
    }

    private static bool PullIn(CameraModeState camera, CameraNormalProjectionCache projection, FixedVector3 tripod, FixedVector3 offset,
        short pitch, short roll, FixedVector3 back, FixedVector3 direction, ICameraPlacementHost host)
    {
        FixedVector3 anchor = offset + tripod;
        FixedVector3 end = anchor + back + (direction << 8);
        if (host.CameraLineToItem(anchor, end) is not { } hit) return false;
        int facing = unchecked(-(direction.X * hit.NormalX + direction.Y * hit.NormalY + direction.Z * hit.NormalZ));
        int margin = unchecked((224 * (4096 - (Abs(facing) >> 12)) >> 12) + 32);
        bool close = hit.Distance <= unchecked(projection.Distance + margin);
        camera.CollisionPosition = hit.Position - direction * margin;
        if (!close) return false;
        if (!Retains(camera, hit, end, pitch, roll, projection, anchor, host)) return false;
        camera.CollisionFlag = 1;
        int length = FixedVectorMath.OriginalDistance(camera.CollisionPosition - anchor, default);
        camera.DesiredDistance = length;
        if (camera.SmoothedDistance != length)
            camera.SmoothedDistance = unchecked(camera.SmoothedDistance + unchecked(length - camera.SmoothedDistance) / 2);
        camera.Position = anchor + direction * camera.SmoothedDistance;
        if (camera.SmoothedDistance < 200 || host.PlayerHiddenWhenFar) host.HidePlayer();
        else host.ShowPlayer();
        return true;
    }

    /// <summary>False only when the player-heading retry is clear; the source then rotates behind the player instead of pulling in.</summary>
    private static bool Retains(CameraModeState camera, CameraLineHit hit, FixedVector3 end, short pitch, short roll,
        CameraNormalProjectionCache projection, FixedVector3 anchor, ICameraPlacementHost host)
    {
        if (hit.NormalY is < -2600 or > 3400 || host.PlayerActionLocked || !host.PlayerMoving) return true;
        if (host.CameraLineToItem(end, camera.CollisionPosition) is not { } second || second.NormalY is < -2600 or > 3400 ||
            Abs(unchecked((hit.NormalX * second.NormalX >> 12) + (hit.NormalY * second.NormalY >> 12) + (hit.NormalZ * second.NormalZ >> 12))) >= 2048)
            return true;
        short heading = host.PlayerEffectiveHeading();
        (short sine, short cosine) = GameAngle.Heading(projection.LookAngle);
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(pitch, heading, roll, 0, 0, 0));
        FixedVector3 retry = FixedRotation.Transform(rotation, new(0, unchecked(-sine), cosine)) * projection.Distance;
        if (host.CameraLineToItem(anchor, anchor + retry) is not null) return true;
        host.SetCameraAngle(heading);
        return false;
    }

    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
