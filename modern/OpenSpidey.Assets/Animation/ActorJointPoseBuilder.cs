using OpenSpidey.Assets.Models;

namespace OpenSpidey.Assets.Animation;

/// <summary>Recovered M3dUtils_BuildPose local joint corrections around absolute animation matrices.</summary>
public static class ActorJointPoseBuilder
{
    public static void Apply(ReadOnlySpan<FixedJointTransform> keyframe, IReadOnlyList<ActorPoseLink> links,
        ReadOnlySpan<RawJointPose> joints, Span<FixedJointTransform> destination, bool interpolatedAnimation)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (joints.Length != links.Count || destination.Length != keyframe.Length)
            throw new ArgumentException("Joint/pose storage does not match its source links and parts.");
        Span<FixedJointTransform> stack = stackalloc FixedJointTransform[6];
        Span<int> producingJoint = stackalloc int[6];
        int stackCount = 0;
        for (int part = 0; part < destination.Length; part++) destination[part] = destination[part] with { M00 = short.MinValue };
        for (int jointIndex = 0; jointIndex < links.Count; jointIndex++)
        {
            ActorPoseLink link = links[jointIndex];
            if (link.Part >= keyframe.Length) throw new InvalidDataException("Pose joint references a missing keyframe part.");
            int found = -1;
            for (int index = stackCount - 1; index >= 0; index--)
                if (link.ParentLink >= producingJoint[index]) { found = index; break; }
            stackCount = found + 1;
            RawJointPose joint = joints[jointIndex];
            bool changed = joint != default;
            if (changed)
            {
                FixedJointTransform key = keyframe[link.Part];
                key = key with { X = unchecked((short)(key.X + joint.TranslationX)),
                    Y = unchecked((short)(key.Y + joint.TranslationY)), Z = unchecked((short)(key.Z + joint.TranslationZ)) };
                FixedJointTransform local = JointRotation(joint);
                local = Compose(key, local);
                if (found >= 0) local = Compose(stack[found], local);
                if (stackCount >= stack.Length) throw new InvalidDataException("Source joint matrix stack exceeds six entries.");
                stack[stackCount] = Compose(local, Inverse(keyframe[link.Part]));
                producingJoint[stackCount++] = jointIndex;
                destination[link.Part] = local;
            }
            else destination[link.Part] = found >= 0 ? Compose(stack[found], keyframe[link.Part]) : keyframe[link.Part];
        }
        // The original leaves sentinel slots untouched on interval animations. Preserve prior fields,
        // rather than inventing a pose for a part absent from the correction packet.
        if (!interpolatedAnimation)
            for (int part = 0; part < destination.Length; part++)
                if (destination[part].M00 == short.MinValue) destination[part] = keyframe[part];
    }

    /// <summary>Original PC RotMatrixYXZ rotation, with its stored-float and x87 expression precision.</summary>
    public static FixedJointTransform JointRotation(RawJointPose joint)
    {
        const float angleScale = 0.0015360969118773937f;
        float sx = (float)Math.Sin(joint.RotationX * angleScale), cx = (float)Math.Cos(joint.RotationX * angleScale);
        float sy = (float)Math.Sin(joint.RotationY * angleScale), cy = (float)Math.Cos(joint.RotationY * angleScale);
        float sz = (float)Math.Sin(joint.RotationZ * angleScale), cz = (float)Math.Cos(joint.RotationZ * angleScale);
        float t1 = sz * sy, t2 = cz * cy, t3 = cz * sy, t4 = sz * cy;
        // The PC stores sine/cosine and t1..t4 as floats, then keeps each complete matrix
        // expression in x87 precision until integer truncation. Rounding the inner sum
        // to a managed float changes boundary entries (original fixture case 270).
        return new((short)(((double)t1 * sx + t2) * 4096), (short)(((double)t3 * sx - t4) * 4096), (short)((double)sy * cx * 4096),
            (short)((double)sz * cx * 4096), (short)((double)cz * cx * 4096), (short)(-sx * 4096),
            (short)(((double)t4 * sx - t3) * 4096), (short)(((double)t2 * sx + t1) * 4096), (short)((double)cy * cx * 4096), 0, 0, 0);
    }

    private static FixedJointTransform Compose(FixedJointTransform parent, FixedJointTransform child) => new(
        Product(parent.M00, parent.M01, parent.M02, child.M00, child.M10, child.M20),
        Product(parent.M00, parent.M01, parent.M02, child.M01, child.M11, child.M21),
        Product(parent.M00, parent.M01, parent.M02, child.M02, child.M12, child.M22),
        Product(parent.M10, parent.M11, parent.M12, child.M00, child.M10, child.M20),
        Product(parent.M10, parent.M11, parent.M12, child.M01, child.M11, child.M21),
        Product(parent.M10, parent.M11, parent.M12, child.M02, child.M12, child.M22),
        Product(parent.M20, parent.M21, parent.M22, child.M00, child.M10, child.M20),
        Product(parent.M20, parent.M21, parent.M22, child.M01, child.M11, child.M21),
        Product(parent.M20, parent.M21, parent.M22, child.M02, child.M12, child.M22),
        Translate(parent.X, parent.M00, parent.M01, parent.M02, child),
        Translate(parent.Y, parent.M10, parent.M11, parent.M12, child),
        Translate(parent.Z, parent.M20, parent.M21, parent.M22, child));

    private static short Product(short a, short b, short c, short x, short y, short z) =>
        unchecked((short)(((a * x) >> 12) + ((b * y) >> 12) + ((c * z) >> 12)));
    private static short Translate(short offset, short x, short y, short z, FixedJointTransform child) =>
        unchecked((short)(offset + (unchecked(x * child.X + y * child.Y + z * child.Z) >> 12)));
    private static FixedJointTransform Inverse(FixedJointTransform matrix) => new(
        matrix.M00, matrix.M10, matrix.M20, matrix.M01, matrix.M11, matrix.M21, matrix.M02, matrix.M12, matrix.M22,
        NegatedDot(matrix.M00, matrix.M10, matrix.M20, matrix),
        NegatedDot(matrix.M01, matrix.M11, matrix.M21, matrix),
        NegatedDot(matrix.M02, matrix.M12, matrix.M22, matrix));
    private static short NegatedDot(short x, short y, short z, FixedJointTransform matrix) =>
        unchecked((short)(unchecked(-(x * matrix.X + y * matrix.Y + z * matrix.Z)) >> 12));
}
