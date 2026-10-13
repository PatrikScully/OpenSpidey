using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>Original Euler fixed-camera target: rotated up/back vectors, their Q12 cross product and MToQ convention.</summary>
public static class SourceCameraEulerRotation
{
    public static FixedQuaternion FromAngles(AimAngles angles) => FromAngles(new ActorAngles(angles.Pitch, angles.Yaw, 0));

    public static FixedQuaternion FromAngles(ActorAngles angles)
    {
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        FixedVector3 up = FixedRotation.Transform(rotation, new(0, 4096, 0));
        FixedVector3 back = FixedRotation.Transform(rotation, new(0, 0, -4096));
        var right = new FixedVector3(unchecked(up.Y * back.Z - up.Z * back.Y) >> 12,
            unchecked(up.Z * back.X - up.X * back.Z) >> 12,
            unchecked(up.X * back.Y - up.Y * back.X) >> 12);
        return FixedQuaternion.FromMatrix(new(unchecked((short)right.X), unchecked((short)up.X), unchecked((short)back.X),
            unchecked((short)right.Y), unchecked((short)up.Y), unchecked((short)back.Y),
            unchecked((short)right.Z), unchecked((short)up.Z), unchecked((short)back.Z), 0, 0, 0));
    }
}
