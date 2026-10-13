using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

public interface IActorPathHost
{
    int CollisionCheckMode { get; set; }
    bool LineOfSight(FixedVector3 from, FixedVector3 to, int flags, ref FixedVector3 hit);
    int GroundHeight(FixedVector3 position, int above, int below);
    FixedVector3 WaypointPosition(int node);
}

public readonly record struct ActorPathCheckResult(int Code, FixedVector3 HitPoint);

/// <summary>Recovered three-ray enemy path clearance, ground-gap sampling and six source point slots.</summary>
public sealed class ActorPathController
{
    private readonly ActorProcedureState actor;
    private readonly IActorPathHost host;
    private readonly FixedVector3[] points = new FixedVector3[6];
    private int pathCount;
    public ushort Radius { get; set; }
    public short Clearance { get; set; }
    public int StoredBodyY { get; set; }
    public int CurrentWaypoint { get; set; } = -1;
    public int PathCount
    {
        get => pathCount;
        set
        {
            if ((uint)value > 5) throw new ArgumentOutOfRangeException(nameof(value));
            pathCount = value;
        }
    }

    public ActorPathController(ActorProcedureState actor, IActorPathHost host)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(host);
        this.actor = actor;
        this.host = host;
    }

    public FixedVector3 GetPoint(int index) => points[PointIndex(index)];
    public void SetPoint(int index, FixedVector3 position) => points[PointIndex(index)] = position;

    public ActorPathCheckResult PathCheck(FixedVector3 start, FixedVector3 end, bool wantHitPoint,
        int groundRange, FixedVector3 initialHit = default)
    {
        int saved = host.CollisionCheckMode;
        if (!wantHitPoint) initialHit = default;
        try
        {
            if ((actor.BehaviorFlags & 0x2000) != 0) host.CollisionCheckMode = 1;
            int code = PathCheckGuts(start, end, ref initialHit, wantHitPoint, groundRange);
            return new(code, wantHitPoint ? initialHit : default);
        }
        finally { host.CollisionCheckMode = saved; }
    }

    public bool AddPointToPath(FixedVector3 position, int distanceLimit)
    {
        position = position with { Y = actor.Body.Position.Y };
        if (CurrentWaypoint > 0)
        {
            FixedVector3 start = host.WaypointPosition(CurrentWaypoint) with { Y = StoredBodyY };
            if (CanConnect(start, position, distanceLimit))
            {
                PathCount = 1;
                points[1] = position;
                actor.BehaviorFlags |= 0x20000000;
                return true;
            }
        }
        else if (PathCount == 0)
        {
            PathCount = 1;
            points[1] = position;
            return true;
        }
        for (int i = 0; i < 4 && i < PathCount; i++)
        {
            if (!CanConnect(points[1 + i], position, distanceLimit)) continue;
            PathCount = i + 2;
            points[i + 2] = position;
            actor.BehaviorFlags |= 0x20000000;
            return true;
        }
        return false;
    }

    public static FixedVector3 GetWaypointNearTarget(FixedVector3 target, int distance, int phase)
    {
        int index = phase & 0x3F;
        int direction = 1 << (index & 7);
        int xScale = (index & 8) != 0 ? 2 : 1;
        int zScale = (index & 16) != 0 ? 2 : 1;
        if (index > 31) { xScale += 2; zScale += 2; }
        if ((direction & 0xEE) != 0)
            target = target with { X = unchecked(target.X + distance * xScale * (direction > 8 ? -1 : 1)) };
        if ((direction & 0xBB) != 0)
            target = target with { Z = unchecked(target.Z + distance * zScale * ((direction & 0x83) != 0 ? 1 : -1)) };
        return target;
    }

    private bool CanConnect(FixedVector3 start, FixedVector3 end, int limit) =>
        (limit == 0 || ActorLifecycle.Distance(start, end) < unchecked((uint)limit)) && PathCheck(start, end, false, 55).Code == 0;

    private int PathCheckGuts(FixedVector3 start, FixedVector3 end, ref FixedVector3 hit, bool wantHitPoint, int flags)
    {
        int losFlags = (int)(actor.BehaviorFlags & 0x100000);
        FixedVector3 along = (end - start) >> 12;
        if ((actor.BehaviorFlags & 0x20000) == 0) along = along with { Y = 0 };
        along = FixedVectorMath.Normalize(along) * (Radius >> 1);
        FixedVector3 near = end + along, far = start - along;
        if ((actor.BehaviorFlags & 0x20000) == 0) near = near with { Y = far.Y };
        bool gotHit = false;
        if (!host.LineOfSight(far, near, losFlags, ref hit)) { gotHit = true; near = hit; }
        int dz = unchecked(far.Z - near.Z), dx = unchecked(far.X - near.X);
        int absDx = GameWord.Absolute(dx), absDz = GameWord.Absolute(dz);
        int xStep = 0, zStep = 0;
        if (absDz <= (absDx >> 1))
        {
            if (absDx <= (absDz >> 1)) xStep = zStep = Radius << 11;
            else zStep = Radius << 12;
        }
        else xStep = Radius << 12;
        FixedVector3 testA = far + new FixedVector3(xStep >> 1, 0, zStep >> 1);
        FixedVector3 testB = near + new FixedVector3(xStep >> 1, 0, zStep >> 1);
        FixedVector3 chord = default;
        if (!host.LineOfSight(testA, testB, losFlags, ref hit))
        {
            if (!wantHitPoint) goto FinalCheck;
            chord = testA - hit;
            gotHit = false;
            testA -= new FixedVector3(xStep, 0, zStep);
            testB = hit - new FixedVector3(xStep, 0, zStep);
            if (host.LineOfSight(testA, testB, losFlags, ref hit)) goto HaveChord;
            goto RecomputeChord;
        }
        testA -= new FixedVector3(xStep, 0, zStep);
        testB -= new FixedVector3(xStep, 0, zStep);
        if (!host.LineOfSight(testA, testB, losFlags, ref hit)) gotHit = false;
        else if (!gotHit)
        {
            if ((actor.BehaviorFlags & 0x2020000) != 0) return 0;
            int targetY = unchecked(StoredBodyY + (Clearance << 12));
            bool dominantX = absDz <= absDx;
            int steps = 1;
            for (int i = 1; GameWord.Absolute(dominantX ? dx : dz) > 1024000; i = unchecked(i * 2))
            {
                steps = unchecked(steps + i);
                dz >>= 1; dx >>= 1;
            }
            if (steps == 0) return 0;
            FixedVector3 walk = near with { Y = targetY };
            int remaining = unchecked(steps - 1);
            while (host.GroundHeight(walk, flags, flags) != -1)
            {
                walk += new FixedVector3(dx, 0, dz);
                if (remaining-- == 0) return 0;
            }
            return 1;
        }
        if (!(wantHitPoint && !gotHit)) goto FinalCheck;
    RecomputeChord:
        chord = testA - hit;
    HaveChord:
        if (!gotHit)
        {
            FixedVector3 full = near - far;
            int lenNear = Length(chord), divisor = unchecked((Radius >> 1) + Length(full));
            full = (((full >> 12) * lenNear) / divisor) << 12;
            hit = far + full;
        }
    FinalCheck:
        if ((unchecked(hit.X - start.X) > 0) != (unchecked(end.X - start.X) > 0)) return 4;
        if ((unchecked(hit.Z - start.Z) > 0) != (unchecked(end.Z - start.Z) > 0)) return 4;
        return 2;
    }

    private static int Length(FixedVector3 vector)
    {
        vector >>= 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z));
    }
    private static int PointIndex(int index) => (uint)index < 6 ? index : throw new ArgumentOutOfRangeException(nameof(index));
}
