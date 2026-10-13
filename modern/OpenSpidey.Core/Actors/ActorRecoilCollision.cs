using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

/// <summary>Source CBody identity and the live fields used by recoil collision and injury dispatch.</summary>
public sealed record ActorCollisionBody(object Identity, int Type, FixedVector3 Position, ushort MinorRadius,
    uint BodyFlags, short Health) : IThugInjuryBody;

public readonly record struct ActorElasticHit(FixedVector3 Direction, short SlideDistance, ushort SlideFrames)
{
    // Original flags42 do not enable damage or part-index fields, which ElasticCollision never writes.
    public ActorHit ToActorHit() => new(42, 0, 25, 0, Direction, SlideDistance, SlideFrames);
}

public interface IActorRecoilCollisionHost
{
    IReadOnlyList<ActorCollisionBody> SourcePlayerBodies { get; }
    IReadOnlyList<ActorCollisionBody> SourceBaddyBodies { get; }
    bool IgnoreSphereRadius { get; }
    bool LineOfSightCheck { get; set; }
    // Direct original environmental-list traversal inherits the most recent zoned query masks.
    ActorCollisionBody? TraceEnvironmental(FixedVector3 start, FixedVector3 end);
    void ApplyElasticHit(ActorCollisionBody target, ActorElasticHit hit);
}

/// <summary>Recovered CBaddy::StruckGameObject and ElasticCollision, using the original sphere query.</summary>
public static class ActorRecoilCollision
{
    public static ActorCollisionBody? StruckGameObject(object sourceIdentity, FixedVector3 previousPosition,
        FixedVector3 currentPosition, bool checkPlayer, bool checkBaddies, IActorRecoilCollisionHost host)
    {
        if (checkPlayer && Check(host.SourcePlayerBodies) is { } player) return player;
        return checkBaddies ? Check(host.SourceBaddyBodies) : null;

        ActorCollisionBody? Check(IReadOnlyList<ActorCollisionBody> sourceBodies)
        {
            ActorLaserBody[] spheres = sourceBodies.Select(body => new ActorLaserBody(body.Identity, body.Position, body.MinorRadius, body.BodyFlags)).ToArray();
            ActorSphereContact? hit = ActorLaserCollision.CheckBodies(previousPosition, currentPosition, spheres,
                excluded: sourceIdentity, radiusScale: 4096, ignoreRadius: host.IgnoreSphereRadius);
            if (hit is not null) return sourceBodies.First(body => ReferenceEquals(body.Identity, hit.Body.Identity));
            host.LineOfSightCheck = true;
            try { return host.TraceEnvironmental(previousPosition, currentPosition); }
            finally { host.LineOfSightCheck = false; }
        }
    }

    public static void ElasticCollision(ActorCollisionBody source, ActorCollisionBody target, FixedVector3 velocity,
        int strength, IActorRecoilCollisionHost host)
    {
        if (target.Health <= 0) return;
        FixedVector3 direction = target.Position - (source.Position - velocity * 2);
        direction = FixedVectorMath.Normalize((direction with { Y = 0 }) >> 10);
        FixedVector3 wholeVelocity = velocity >> 12;
        int length = FixedVectorMath.OriginalSquareRoot(unchecked(wholeVelocity.X * wholeVelocity.X + wholeVelocity.Y * wholeVelocity.Y + wholeVelocity.Z * wholeVelocity.Z));
        host.ApplyElasticHit(target, new(direction, unchecked((short)(strength * length)), unchecked((ushort)strength)));
    }
}
