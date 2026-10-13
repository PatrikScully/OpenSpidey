using System.Numerics;

namespace OpenSpidey.Assets.Animation;

/// <summary>Original row-major Q12 rotation and signed 16-bit translation, before engine coordinate conversion.</summary>
public readonly record struct FixedJointTransform(short M00, short M01, short M02,
    short M10, short M11, short M12, short M20, short M21, short M22,
    short X, short Y, short Z)
{
    public Vector3 TransformPoint(Vector3 point) => new(
        (M00 * point.X + M01 * point.Y + M02 * point.Z) / 4096 + X,
        (M10 * point.X + M11 * point.Y + M12 * point.Z) / 4096 + Y,
        (M20 * point.X + M21 * point.Y + M22 * point.Z) / 4096 + Z);

    public static FixedJointTransform FromJoint(RawJointPose joint)
    {
        // Keep the recovered PC conversion factor and float rounding, rather than substituting Tau / 4096.
        const float angleScale = 0.0015360969118773937f;
        float sx = (float)Math.Sin(joint.RotationX * angleScale);
        float cx = (float)Math.Cos(joint.RotationX * angleScale);
        float sy = (float)Math.Sin(joint.RotationY * angleScale);
        float cy = (float)Math.Cos(joint.RotationY * angleScale);
        float sz = (float)Math.Sin(joint.RotationZ * angleScale);
        float cz = (float)Math.Cos(joint.RotationZ * angleScale);
        float t1 = sz * sy;
        float t2 = cz * cy;
        float t3 = cz * sy;
        float t4 = sz * cy;
        return new((short)((t1 * sx + t2) * 4096), (short)((t3 * sx - t4) * 4096), (short)(sy * cx * 4096),
            (short)(sz * cx * 4096), (short)(cz * cx * 4096), (short)(-sx * 4096),
            (short)((t4 * sx - t3) * 4096), (short)((t2 * sx + t1) * 4096), (short)(cy * cx * 4096),
            joint.TranslationX, joint.TranslationY, joint.TranslationZ);
    }

    internal FixedJointTransform WithParentTranslation(FixedJointTransform parent) => this with
    {
        X = unchecked((short)(((parent.M00 * X + parent.M01 * Y + parent.M02 * Z) >> 12) + parent.X)),
        Y = unchecked((short)(((parent.M10 * X + parent.M11 * Y + parent.M12 * Z) >> 12) + parent.Y)),
        Z = unchecked((short)(((parent.M20 * X + parent.M21 * Y + parent.M22 * Z) >> 12) + parent.Z))
    };
}
