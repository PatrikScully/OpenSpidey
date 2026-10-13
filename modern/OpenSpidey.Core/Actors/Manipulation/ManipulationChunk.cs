using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Manipulation;

public readonly record struct ManipulationSmokeRequest(FixedVector3 Position, FixedVector3 Origin, int CameraSide);
public interface IManipulationImpactHost
{
    IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder { get; }
    FixedVector3 PlayerPosition { get; }
    FixedVector3 CameraPositionWholeUnits { get; }
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    void PlayPositionSound(ushort sound, FixedVector3 position, int volume);
    void SendExplosionMessage(object target, FixedVector3 position);
    void CreateBigExplosion(FixedVector3 position);
    void HitPlayer(ActorHit hit);
    void HitBaddy(object target, ActorHit hit);
    void ReactSymbioteToExplosion(object target);
    /// <summary>Call the factory only when a genuine source debris allocation succeeds.</summary>
    void TrySpawnDebris(Func<ManipulationDebrisRequest> factory);
    /// <summary>Construct actual CChunkSmoke, preserving its own original random consumption and render behavior.</summary>
    void SpawnChunkSmoke(ManipulationSmokeRequest smoke);
    void SendAttachedPulse(ushort node);
}

/// <summary>CManipOb::Chunk side effects, damage, actual chunk model selection and source smoke placement.</summary>
public static class ManipulationChunk
{
    public static void Create(ManipulationState state, FixedVector3 normal, IManipulationImpactHost host, Func<int, int> random)
    {
        FixedVector3 position = state.Body.Position;
        ushort sound = unchecked((ushort)state.ImpactSound);
        host.PlayPositionSound(sound != 0 ? (ushort)(sound | 0x8000) : (ushort)((state.Flags & 8) != 0 ? 28 : 27), position, 0);
        FixedVector3 messagePosition = position;
        int ground = host.GroundHeight(position, 0, 700, 0);
        if (ground != -1) messagePosition = messagePosition with { Y = unchecked(ground - 204800) };
        foreach (ManipulationTargetBody target in host.SourceBaddyOrder)
        {
            if ((target.Sphere.BodyFlags & 0x200) == 0) continue;
            if (HorizontalDistance(messagePosition, target.Sphere.Position) >= 2000u) continue;
            if (GameWord.Absolute(unchecked(target.Sphere.Position.Y - messagePosition.Y)) >= 1638400) continue;
            // Actual PC403250 sends NumParams0, NumVects1 and this position, unlike recovered garbage-data comment.
            host.SendExplosionMessage(target.Sphere.Identity, messagePosition);
        }
        if ((state.Flags & 0x20) != 0)
        {
            host.CreateBigExplosion(position);
            int vertical = unchecked(position.Y - host.PlayerPosition.Y) >> 12;
            int distance = Distance(position, host.PlayerPosition);
            if (vertical > -300 && vertical < 300 && distance < 1024)
                host.HitPlayer(new(12, 0, 0, unchecked((ushort)(30 * distance / 1024)),
                    FixedVectorMath.Normalize(((host.PlayerPosition - position) >> 12) with { Y = 0 })));
            foreach (ManipulationTargetBody target in host.SourceBaddyOrder)
            {
                if ((target.Sphere.BodyFlags & 0x1000) == 0) continue;
                vertical = unchecked(position.Y - target.Sphere.Position.Y) >> 12;
                if (vertical <= -300 || vertical >= 300) continue;
                distance = Distance(position, target.Sphere.Position);
                if (target.Type == 324)
                {
                    if (distance <= 1024) host.ReactSymbioteToExplosion(target.Sphere.Identity);
                }
                else if (distance < 1024)
                    host.HitBaddy(target.Sphere.Identity, new(28, 0, 0, unchecked((ushort)(50 * distance / 1024)),
                        FixedVectorMath.Normalize((target.Sphere.Position - position) >> 12), 500, 15));
            }
        }
        int modelCount = state.ChunkModels.Count;
        if (modelCount != 0)
        {
            int halfHorizontalSpeed = HorizontalSpeed(state.Body.Velocity) >> 1;
            FixedVector3 first, second;
            if (normal.Y >= -3000)
            {
                int halfSpeed = FixedAim.Distance(default, state.Body.Velocity) >> 1;
                (first, second) = Perpendiculars(normal);
                for (int angle = 0; angle < 4092; angle += 682)
                {
                    var heading = GameAngle.Heading(angle);
                    FixedVector3 velocity = ((first * heading.Sine) >> 12) * halfSpeed;
                    velocity += normal * halfHorizontalSpeed;
                    velocity += ((second * heading.Cosine) >> 12) * halfSpeed;
                    SpawnDebris(state, position, velocity, host, random);
                }
            }
            else
            {
                for (int angle = 0; angle < 4092; angle += 682)
                {
                    var heading = GameAngle.Heading(angle);
                    FixedVector3 velocity = new(unchecked(halfHorizontalSpeed * heading.Sine), unchecked(-state.Body.Velocity.Y), unchecked(halfHorizontalSpeed * heading.Cosine));
                    SpawnDebris(state, position, velocity, host, random);
                }
                first = new(0, 0, 4096); second = new(4096, 0, 0);
            }
            int cameraX = unchecked((position.X >> 12) - host.CameraPositionWholeUnits.X), cameraZ = unchecked((position.Z >> 12) - host.CameraPositionWholeUnits.Z);
            for (int count = 24; count != 0; count--)
            {
                int angle = random(4096) & 4095, magnitude = random(300);
                var heading = GameAngle.Heading(angle);
                int x = unchecked(magnitude * heading.Sine) >> 12, z = unchecked(magnitude * heading.Cosine) >> 12;
                FixedVector3 smokePosition = position + first * x + second * z;
                int side = unchecked(cameraX * (z >> 12) - cameraZ * (x >> 12)) >= 0 ? 1 : -1;
                host.SpawnChunkSmoke(new(smokePosition, position, side));
            }
        }
        if (state.PulseSent == 0) { state.PulseSent = 1; host.SendAttachedPulse(state.Node); }
    }
    private static void SpawnDebris(ManipulationState state, FixedVector3 position, FixedVector3 velocity, IManipulationImpactHost host, Func<int, int> random)
    {
        host.TrySpawnDebris(() => new(state.ChunkModels[random(state.ChunkModels.Count)], position, velocity));
    }
    private static (FixedVector3 First, FixedVector3 Second) Perpendiculars(FixedVector3 normal)
    {
        uint x = unchecked((uint)GameWord.Absolute(normal.X)), y = unchecked((uint)GameWord.Absolute(normal.Y)), z = unchecked((uint)GameWord.Absolute(normal.Z));
        FixedVector3 first;
        if (x > y || x > z)
            first = y > x || y > z ? new(unchecked(-normal.Y), normal.X, 0) : new(normal.Z, 0, unchecked(-normal.X));
        else first = new(0, unchecked(-normal.Z), normal.Y);
        FixedVector3 second = new(unchecked(normal.Y * first.Z - normal.Z * first.Y) >> 12,
            unchecked(normal.Z * first.X - normal.X * first.Z) >> 12, unchecked(normal.X * first.Y - normal.Y * first.X) >> 12);
        return (first, second);
    }
    private static int HorizontalSpeed(FixedVector3 velocity)
    {
        FixedVector3 whole = (default(FixedVector3) - velocity) >> 12;
        return FixedVectorMath.OriginalSquareRoot(unchecked(whole.X * whole.X + whole.Z * whole.Z));
    }
    private static uint HorizontalDistance(FixedVector3 first, FixedVector3 second) => unchecked((uint)Distance(first with { Y = second.Y }, second));
    private static int Distance(FixedVector3 first, FixedVector3 second) => unchecked((int)ActorLifecycle.Distance(first, second));
}
