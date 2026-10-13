using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Effects;

public readonly record struct FlamingWebTrailRequest(FixedVector3 Position, FixedVector3 Velocity,
    int AnimationIndex, short Scale, ushort ScaleDecay, short ColourDecay);
public readonly record struct FireSparkRequest(FixedVector3 Position, FixedVector3 Velocity, int GroundY);

public interface IFlamingWebTrail
{
    short Angle { get; set; }
}

public interface IFlamingImpactWebHost
{
    ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end);
    object? CheckObjectCollision(FixedVector3 start, FixedVector3 end);
    void HitBody(object identity, ActorHit hit);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void FlickEligibleSwitch(object identity);
    int GetGroundY(FixedVector3 position);
    IFlamingWebTrail CreateTrail(FlamingWebTrailRequest request);
    void CreateSpark(FireSparkRequest request, Func<int, int> random);
}

/// <summary>Actual CFlamingImpactWeb constructor and PC Move0x4A40D0, with separate blur/pixel effect ownership.</summary>
public sealed class FlamingImpactWeb
{
    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; }
    public int Damage { get; }
    public uint LastClock { get; private set; }
    public ushort Lifetime { get; }
    public short Age { get; private set; }
    public short Scale { get; private set; }
    public short Angle { get; private set; }
    public short AngularVelocity { get; }
    public int TrailCountdown { get; private set; }
    public ImpactWebEnvironmentContact? EnvironmentContact { get; }
    public bool Alive { get; private set; } = true;
    public int AnimationIndex => 16;
    public int Frame => 0;
    public uint CodeRgb => 0x2E646464;
    public uint PostScale => 0x0A001000;

    public FlamingImpactWeb(FixedVector3 position, ActorAngles direction, int damage, uint clock,
        IFlamingImpactWebHost host, Func<int, int> random)
    {
        Position = position;
        Damage = damage; // Simby does not call the ordinary projectile's player-difficulty damage scaler.
        LastClock = clock;
        Velocity = FixedAim.Velocity(32, new(direction.X, direction.Y));
        ushort lifetime = 120;
        EnvironmentContact = host.TraceInitialEnvironment(position, position + Velocity * lifetime);
        if (EnvironmentContact is { } contact) lifetime = unchecked((ushort)(contact.Distance / 32));
        if (lifetime > 120) lifetime = 120;
        Lifetime = lifetime;
        Angle = unchecked((short)random(4096));
        AngularVelocity = random(2) != 0 ? (short)768 : (short)-768;
    }

    public void Advance(uint clock, IFlamingImpactWebHost host, Func<int, int> random)
    {
        if (!Alive) return;
        if (TrailCountdown != 0) TrailCountdown--;
        else
        {
            TrailCountdown = 1;
            IFlamingWebTrail trail = host.CreateTrail(new(Position, (Velocity * 128) >> 8, 16, Scale, 100, 20));
            trail.Angle = unchecked((short)random(4096));
        }
        Scale = unchecked((short)(Scale + 1500));
        if (Scale > 1000) Scale = 1000;
        Angle = unchecked((short)(Angle + AngularVelocity));
        FixedVector3 previous = Position;
        int elapsed = unchecked((int)(clock - LastClock));
        LastClock = clock;
        Position += Velocity * elapsed;
        object? body = host.CheckObjectCollision(previous, Position);
        int mode = 0;
        if (body is not null) mode = 2;
        else
        {
            Age = unchecked((short)(Age + elapsed));
            if (Age >= Lifetime) mode = EnvironmentContact is null ? 1 : 3;
        }
        if (mode == 0) return;
        if (mode == 2)
        {
            FixedVector3 direction = FixedVectorMath.Normalize(Velocity >> 12);
            // Original Move leaves unused part/slide fields uninitialized. Explicit zeroes avoid arbitrary slide data/crashes.
            host.HitBody(body!, new(14, 0, 7, unchecked((ushort)Damage), direction));
            host.PlayPositionSound(16, Position, 0);
        }
        else if (mode == 3 && (EnvironmentContact!.FaceFlags & 0x02000000) != 0)
            host.FlickEligibleSwitch(EnvironmentContact.Identity);

        FixedVector3 normal = mode switch
        {
            2 => new(0, -4096, 0),
            3 => new(EnvironmentContact!.Normal.X, EnvironmentContact.Normal.Y, EnvironmentContact.Normal.Z),
            _ => default
        };
        (FixedVector3 first, FixedVector3 second) = Perpendiculars(normal);
        int ground = host.GetGroundY(Position + normal * 100);
        for (int index = 0; index < 20; index++)
        {
            (short sine, short cosine) = GameAngle.Heading((random(1601) - 1824) & 4095);
            FixedVector3 velocity = (first * cosine + second * sine) >> 12;
            (sine, cosine) = GameAngle.Heading(random(512));
            velocity = (normal * sine + velocity * cosine) >> 12;
            velocity *= random(40) + 30;
            host.CreateSpark(new(Position, velocity, ground), random);
        }
        Alive = false;
    }

    private static (FixedVector3 First, FixedVector3 Second) Perpendiculars(FixedVector3 direction)
    {
        int x = Math.Abs(direction.X), y = Math.Abs(direction.Y), z = Math.Abs(direction.Z);
        FixedVector3 first = x > y || x > z
            ? y > x || y > z ? new(-direction.Y, direction.X, 0) : new(direction.Z, 0, -direction.X)
            : new(0, -direction.Z, direction.Y);
        var second = new FixedVector3(unchecked(direction.Y * first.Z - direction.Z * first.Y) >> 12,
            unchecked(direction.Z * first.X - direction.X * first.Z) >> 12,
            unchecked(direction.X * first.Y - direction.Y * first.X) >> 12);
        return (first, second);
    }
}
