using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerOrientationSnapshot(FixedJointTransform Basis, FixedVector3 LastNormal, bool HasCachedNormal);
public readonly record struct PlayerStartOrientationResult(bool StandRequired, FixedVector3 NegativeNormal, FixedJointTransform InverseBasis);

/// <summary>Recovered surface basis construction; the last normal belongs to this actor instead of a global cache.</summary>
public sealed class PlayerOrientation
{
    private FixedVector3 lastNormal;
    private bool hasCachedNormal;
    public FixedJointTransform Basis { get; private set; }
    public FixedVector3 Right => new(Basis.M00, Basis.M10, Basis.M20);
    public FixedVector3 Up => new(-Basis.M01, -Basis.M11, -Basis.M21);
    public FixedVector3 Forward => new(Basis.M02, Basis.M12, Basis.M22);
    public short Heading => (short)((1024 - GameAngle.Atan2(Forward.Z, Forward.X)) & 4095);

    public PlayerOrientationSnapshot Capture() => new(Basis, lastNormal, hasCachedNormal);
    public void Restore(PlayerOrientationSnapshot snapshot)
    { Basis = snapshot.Basis; lastNormal = snapshot.LastNormal; hasCachedNormal = snapshot.HasCachedNormal; }

    public PlayerOrientation(short restartYaw)
    {
        Basis = Multiply(FixedJointTransform.FromJoint(new(0, 2048, 0, 0, 0, 0)),
            FixedJointTransform.FromJoint(new(0, restartYaw, 0, 0, 0, 0)));
    }

    /// <summary>Source MulMatrix updates the existing rotation; an authored restart yaw does not replace prior orientation.</summary>
    public void MultiplyYaw(short yaw)
    {
        FixedJointTransform previous = Basis;
        Basis = Multiply(previous, FixedJointTransform.FromJoint(new(0, yaw, 0, 0, 0, 0))) with
        { X = previous.X, Y = previous.Y, Z = previous.Z };
    }

    /// <summary>Actual4B9E50 yaw multiplication followed by4C4BB0's false/ZeroVector path, including its retained source cache and fallback.</summary>
    public PlayerStartOrientationResult SetStartOrientation(short yaw, FixedVector3 normal, int headingDelta = 0)
    {
        MultiplyYaw(yaw);
        FixedVector3 negativeNormal = normal * -1;
        int distance = unchecked(GameWord.Absolute(unchecked(normal.X - lastNormal.X)) +
            GameWord.Absolute(unchecked(normal.Y - lastNormal.Y)) + GameWord.Absolute(unchecked(normal.Z - lastNormal.Z)));
        if (distance > 16 || headingDelta != 0)
        {
            lastNormal = normal;
            hasCachedNormal = true;
            FixedVector3 right = FixedVectorMath.Normalize(Cross(negativeNormal, Forward));
            FixedVector3 forward = Cross(right, negativeNormal);
            Basis = new((short)right.X, (short)negativeNormal.X, (short)forward.X,
                (short)right.Y, (short)negativeNormal.Y, (short)forward.Y,
                (short)right.Z, (short)negativeNormal.Z, (short)forward.Z, Basis.X, Basis.Y, Basis.Z);
            if (headingDelta != 0) MultiplyYaw(unchecked((short)headingDelta));
        }
        // The actual source stores89C before checking for a degenerate basis.
        FixedJointTransform inverse = FixedRotation.TransposeRotation(Basis);
        bool stand = Magnitude(Right) < 2048 || Magnitude(new(Basis.M01, Basis.M11, Basis.M21)) < 2048 || Magnitude(Forward) < 2048;
        if (stand) Basis = new(-4096, 0, 0, 0, 4096, 0, 0, 0, -4096, Basis.X, Basis.Y, Basis.Z);
        return new(stand, negativeNormal, inverse);
    }

    public short GetEffectiveHeading(bool onWall)
    {
        if (!onWall) return Heading;
        FixedVector3 sideways = FixedVectorMath.Normalize(Cross(new(0, -4096, 0), Up));
        FixedVector3 vertical = Cross(sideways, Up) * -1;
        FixedVector3 local = FixedRotation.TransformTranspose(Basis, vertical);
        return (short)((GameAngle.Atan2(local.Z, local.X) + 1024) & 4095);
    }

    /// <returns>Whether the source's degenerate-basis fallback requires a stand reset.</returns>
    public bool Orient(FixedVector3 normal, int headingDelta, FixedVector3? target = null)
    {
        FixedVector3 up = normal * -1;
        if (!hasCachedNormal || target is not null || Math.Abs(normal.X - lastNormal.X) + Math.Abs(normal.Y - lastNormal.Y) +
            Math.Abs(normal.Z - lastNormal.Z) > 16 || headingDelta != 0)
        {
            // A target-based ceiling orientation must update the cache too; otherwise returning to
            // the previously cached floor normal can retain the ceiling basis.
            lastNormal = normal;
            hasCachedNormal = true;
            FixedVector3 right = FixedVectorMath.Normalize(Cross(up, target ?? Forward));
            FixedVector3 forward = Cross(right, up);
            Basis = new((short)right.X, (short)up.X, (short)forward.X,
                (short)right.Y, (short)up.Y, (short)forward.Y,
                (short)right.Z, (short)up.Z, (short)forward.Z, 0, 0, 0);
            if (target is null && headingDelta != 0)
                Basis = Multiply(Basis, FixedJointTransform.FromJoint(new(0, (short)headingDelta, 0, 0, 0, 0)));
        }
        if (Magnitude(new(Basis.M00, Basis.M10, Basis.M20)) >= 2048 &&
            Magnitude(new(Basis.M01, Basis.M11, Basis.M21)) >= 2048 && Magnitude(Forward) >= 2048) return false;
        Basis = new(-4096, 0, 0, 0, 4096, 0, 0, 0, -4096, 0, 0, 0);
        hasCachedNormal = false;
        return true;
    }

    private static int Magnitude(FixedVector3 axis) => Math.Abs(axis.X) + Math.Abs(axis.Y) + Math.Abs(axis.Z);

    private static FixedVector3 Cross(FixedVector3 a, FixedVector3 b) => new(
        unchecked(a.Y * b.Z - a.Z * b.Y) >> 12, unchecked(a.Z * b.X - a.X * b.Z) >> 12,
        unchecked(a.X * b.Y - a.Y * b.X) >> 12);

    private static FixedJointTransform Multiply(FixedJointTransform a, FixedJointTransform b) => new(
        (short)((a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20) >> 12),
        (short)((a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21) >> 12),
        (short)((a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22) >> 12),
        (short)((a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20) >> 12),
        (short)((a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21) >> 12),
        (short)((a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22) >> 12),
        (short)((a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20) >> 12),
        (short)((a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21) >> 12),
        (short)((a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22) >> 12), 0, 0, 0);
}
