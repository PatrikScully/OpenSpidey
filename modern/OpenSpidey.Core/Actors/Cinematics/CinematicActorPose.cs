using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cinematics;

/// <summary>Source cinematic head pan/tilt easing and limb-derived MGS footprint shadow.</summary>
public static class CinematicActorPose
{
    public static AimAngles AdvanceHead(AimAngles current, AimAngles target) => new(
        Ease(current.Pitch, target.Pitch), Ease(current.Yaw, target.Yaw));

    private static short Ease(short current, short target)
    {
        int delta = target - current;
        if (delta > 2048) delta -= 4096;
        else if (delta < -2048) delta += 4096;
        return unchecked((short)(current + Math.Clamp(delta, -48, 48)));
    }

    public static FixedVector3[] ShadowCorners(FixedVector3 position, FixedJointTransform rotation,
        short groundY, ReadOnlySpan<FixedVector3> limbHooks)
    {
        if (limbHooks.Length != 4) throw new ArgumentException("Source MGS shadow needs exactly four limb hooks.", nameof(limbHooks));
        FixedJointTransform inverse = Transpose(rotation);
        int minX = -32, maxX = 32, minZ = -64, maxZ = 64;
        foreach (FixedVector3 hook in limbHooks)
        {
            FixedVector3 local = FixedRotation.Transform(inverse, (hook - position) >> 12);
            if (local.X > maxX) maxX = local.X;
            else if (local.X < minX) minX = local.X;
            if (local.Z > maxZ) maxZ = local.Z;
            else if (local.Z < minZ) minZ = local.Z;
        }
        FixedVector3[] corners = [new(minX, 0, maxZ), new(minX, 0, minZ), new(maxX, 0, maxZ), new(maxX, 0, minZ)];
        for (int index = 0; index < corners.Length; index++)
        {
            FixedVector3 world = FixedRotation.Transform(rotation, corners[index]) << 12;
            corners[index] = new(unchecked(position.X + world.X), groundY << 12, unchecked(position.Z + world.Z));
        }
        return corners;
    }

    public static FixedJointTransform Transpose(FixedJointTransform value) => new(value.M00, value.M10, value.M20,
        value.M01, value.M11, value.M21, value.M02, value.M12, value.M22, 0, 0, 0);
}
