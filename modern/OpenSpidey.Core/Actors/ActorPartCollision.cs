using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

/// <summary>Recovered Web_CollideWithSuper posed-part sweeps and strict BoundingBoxCollisionCheck face clipping.</summary>
public static class ActorPartCollision
{
    /// <param name="pose">Original model-space animation poses, before any render-only player part swaps.</param>
    /// <param name="hiddenParts">Source field_194: a set bit disables that part's box.</param>
    /// <param name="boxScale">Q12 source box scale; melee uses 8192, doubling each signed bound.</param>
    public static bool TryCollide(FixedVector3 actorPosition, FixedJointTransform actorRotation,
        ReadOnlySpan<FixedJointTransform> pose, IReadOnlyList<ActorPartBox> boxes, uint hiddenParts,
        FixedVector3 from, FixedVector3 to, int boxScale, out ActorHook hit)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        if (boxes.Count > pose.Length || boxes.Count > ushort.MaxValue)
            throw new ArgumentException("Actor collision boxes reference missing source pose parts.", nameof(boxes));
        FixedVector3 localStart = RotateTranspose(actorRotation, Narrow((from - actorPosition) >> 12));
        FixedVector3 localEnd = RotateTranspose(actorRotation, Narrow((to - actorPosition) >> 12));
        hit = new(0, 0, 0, ushort.MaxValue);
        uint bestDistance = uint.MaxValue;
        uint bit = 2;
        for (int part = 1; part < boxes.Count; part++, bit <<= 1)
        {
            if ((hiddenParts & bit) != 0) continue;
            FixedJointTransform transform = pose[part];
            var pivot = new FixedVector3(transform.X >> 4, transform.Y >> 4, transform.Z >> 4);
            FixedVector3 hookStart = RotateTranspose(transform, Narrow(localStart - pivot));
            FixedVector3 hookEnd = RotateTranspose(transform, Narrow(localEnd - pivot));
            ActorPartBox box = Scale(boxes[part], boxScale);
            FixedVector3 point = hookEnd;
            if (!TryClipBox(box, hookStart, ref point)) continue;
            FixedVector3 difference = hookStart - point;
            int squared = unchecked(difference.X * difference.X + difference.Y * difference.Y + difference.Z * difference.Z);
            uint distance = (uint)FixedVectorMath.OriginalSquareRoot(squared);
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            hit = new(unchecked((short)(point.X << 4)), unchecked((short)(point.Y << 4)),
                unchecked((short)(point.Z << 4)), (ushort)part);
        }
        return hit.PartIndex != ushort.MaxValue;
    }

    /// <summary>Returns the first source face crossing in X/Y/Z order; inside-only, edge and endpoint contacts miss.</summary>
    public static bool TryClipBox(ActorPartBox box, FixedVector3 from, ref FixedVector3 to)
    {
        FixedVector3 start = Narrow(from), end = Narrow(to);
        for (int axis = 0; axis < 3; axis++)
            if ((Component(start, axis) > Upper(box, axis) && Component(end, axis) > Upper(box, axis)) ||
                (Component(start, axis) < Lower(box, axis) && Component(end, axis) < Lower(box, axis))) return false;
        FixedVector3 delta = end - start;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Component(start, axis) > Upper(box, axis) && Component(end, axis) < Upper(box, axis) &&
                Cross(box, start, delta, axis, Upper(box, axis), out FixedVector3 point))
            { to = point; return true; }
            if (Component(start, axis) < Lower(box, axis) && Component(end, axis) > Lower(box, axis) &&
                Cross(box, start, delta, axis, Lower(box, axis), out point))
            { to = point; return true; }
        }
        return false;
    }

    private static bool Cross(ActorPartBox box, FixedVector3 start, FixedVector3 delta, int axis, int plane, out FixedVector3 point)
    {
        point = start;
        for (int other = 0; other < 3; other++)
        {
            int value = plane;
            if (other != axis)
            {
                value = unchecked(Component(start, other) + unchecked(Component(delta, other) * (plane - Component(start, axis))) / Component(delta, axis));
                if (value >= Upper(box, other) || value <= Lower(box, other))
                { point = default; return false; }
            }
            value = unchecked((short)value);
            point = other switch { 0 => point with { X = value }, 1 => point with { Y = value }, _ => point with { Z = value } };
        }
        return true;
    }

    private static int Component(FixedVector3 vector, int axis) => axis switch { 0 => vector.X, 1 => vector.Y, _ => vector.Z };
    private static int Upper(ActorPartBox box, int axis) => axis switch { 0 => box.UpperX, 1 => box.UpperY, _ => box.UpperZ };
    private static int Lower(ActorPartBox box, int axis) => axis switch { 0 => box.LowerX, 1 => box.LowerY, _ => box.LowerZ };
    private static FixedVector3 Narrow(FixedVector3 value) => new(unchecked((short)value.X), unchecked((short)value.Y), unchecked((short)value.Z));
    private static FixedVector3 RotateTranspose(FixedJointTransform basis, FixedVector3 value) => Narrow(FixedRotation.TransformTranspose(basis, value));
    private static ActorPartBox Scale(ActorPartBox box, int scale)
    {
        if (scale == 4096) return box;
        return new(Scaled(box.UpperX), Scaled(box.LowerX), Scaled(box.UpperY), Scaled(box.LowerY), Scaled(box.UpperZ), Scaled(box.LowerZ));
        short Scaled(short value) => unchecked((short)(unchecked(scale * value) >> 12));
    }
}
