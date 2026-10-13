using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Combat;

public sealed class ActorLaserLine
{
    public FixedVector3 Start { get; set; }
    public FixedVector3 End { get; set; }
}

public sealed record ActorLaserBody(object Identity, FixedVector3 Position, ushort MinorRadius, uint BodyFlags);
public sealed record ActorLaserContact(object Identity, FixedVector3 Position, nuint SourceAddress = 0);
public sealed record ActorSphereContact(ActorLaserBody Body, FixedVector3 Position, int Parameter);

public interface IActorLaserHost
{
    object ActorIdentity { get; }
    object PlayerIdentity { get; }
    FixedVector3 ActorPosition { get; }
    FixedVector3 PlayerPosition { get; }
    IReadOnlyList<ActorLaserBody> PlayerBodies { get; }
    bool IgnoreSphereRadius { get; }
    bool LineOfSightCheck { get; set; }
    ActorLaserContact? TraceZoned(FixedVector3 start, FixedVector3 end, bool includeEnvironment);
    // Direct environmental-list traversal inherits the last zoned collision masks.
    ActorLaserContact? TraceEnvironmental(FixedVector3 start, FixedVector3 end);
    void PlayPlayerHitSound(ushort sound);
}

/// <summary>Recovered CThug::LaserCollision and its original sphere/environment object-collision test.</summary>
public static class ActorLaserCollision
{
    public static int Check(int actorType, ActorLaserLine line, ref FixedVector3 direction,
        bool checkPlayer, IActorLaserHost host, Func<int, int> random)
    {
        FixedVector3 start = line.Start, end = line.End;
        line.Start = host.ActorPosition;
        line.End = start;
        if (host.TraceZoned(line.Start, line.End, includeEnvironment: true) is not null) return -1;
        line.Start = start;
        line.End = end;
        bool hitPlayer = false;
        if (checkPlayer && CheckObjectCollision(start, end, host))
        {
            int distance = unchecked(Length(host.PlayerPosition - start) << 12);
            int scale = distance / Length(direction);
            direction = (direction >> 12) * scale;
            hitPlayer = true;
            line.End = line.Start + direction;
        }
        ActorLaserContact? contact = host.TraceZoned(line.Start, line.End, includeEnvironment: true);
        // The original overwrites its line-pointer argument with literal12 after the player test.
        bool ignoredLine = contact is not null && (hitPlayer ? contact.SourceAddress == 12 : ReferenceEquals(contact.Identity, line));
        if (contact is not null && !ignoredLine && !ReferenceEquals(contact.Identity, host.PlayerIdentity) &&
            !ReferenceEquals(contact.Identity, host.ActorIdentity))
        {
            line.End = contact.Position;
            return 0;
        }
        if (!hitPlayer)
        {
            direction >>= 3;
            line.End = line.Start + direction;
            return 0;
        }
        host.PlayPlayerHitSound(unchecked((ushort)(((actorType != 304 ? 37 : 6) + random(2)) | 0x8000)));
        return 1;
    }

    public static bool CheckObjectCollision(FixedVector3 start, FixedVector3 end, IActorLaserHost host)
    {
        if (CheckBodies(start, end, host.PlayerBodies, radiusScale: 4096, ignoreRadius: host.IgnoreSphereRadius) is not null) return true;
        host.LineOfSightCheck = true;
        try { return host.TraceEnvironmental(start, end) is not null; }
        finally { host.LineOfSightCheck = false; }
    }

    public static ActorSphereContact? CheckBodies(FixedVector3 start, FixedVector3 end,
        IReadOnlyList<ActorLaserBody> bodies, object? excluded = null, int radiusScale = 4096, bool ignoreRadius = false)
    {
        FixedVector3 delta = (end - start) >> 12;
        int length = FixedVectorMath.OriginalSquareRoot(Squared(delta));
        if (length == 0) return null;
        FixedVector3 direction = (delta << 12) / length;
        FixedVector3 shortDirection = new(unchecked((short)direction.X), unchecked((short)direction.Y), unchecked((short)direction.Z));
        FixedVector3 minimum = new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z));
        FixedVector3 maximum = new(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z));
        int bestParameter = int.MaxValue;
        ActorLaserBody? nearest = null;
        foreach (ActorLaserBody body in bodies)
        {
            if ((body.BodyFlags & 0x40) != 0 || ReferenceEquals(body.Identity, excluded)) continue;
            int radius = unchecked(radiusScale * body.MinorRadius);
            FixedVector3 position = body.Position;
            if (unchecked(position.X + radius) < minimum.X || unchecked(position.X - radius) > maximum.X ||
                unchecked(position.Y + radius) < minimum.Y || unchecked(position.Y - radius) > maximum.Y ||
                unchecked(position.Z + radius) < minimum.Z || unchecked(position.Z - radius) > maximum.Z) continue;
            FixedVector3 relative = (position - start) >> 12;
            FixedVector3 cross = new(unchecked(direction.Y * relative.Z - direction.Z * relative.Y) >> 12,
                unchecked(direction.Z * relative.X - direction.X * relative.Z) >> 12,
                unchecked(relative.Y * direction.X - direction.Y * relative.X) >> 12);
            int wholeRadius = radius >> 12;
            int radiusSquared = unchecked(wholeRadius * wholeRadius);
            if (!ignoreRadius && Squared(cross) >= radiusSquared) continue;
            int parameter = unchecked(shortDirection.X * relative.X + shortDirection.Y * relative.Y + shortDirection.Z * relative.Z) >> 12;
            if (parameter >= bestParameter) continue;
            if (!ignoreRadius)
            {
                if (parameter > length && Squared((position - end) >> 12) >= radiusSquared) continue;
                if (parameter < 0 && Squared(relative) >= radiusSquared) continue;
            }
            bestParameter = parameter;
            nearest = body;
        }
        if (nearest is null) return null;
        // Original gte_gpf shifts12, followed by a second CVector shift12.
        FixedVector3 hit = (((direction * bestParameter) >> 12) >> 12) + start;
        return new(nearest, hit, bestParameter);
    }

    private static int Length(FixedVector3 vector) => FixedVectorMath.OriginalSquareRoot(Squared(vector >> 12));
    private static int Squared(FixedVector3 vector) => unchecked(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
}
