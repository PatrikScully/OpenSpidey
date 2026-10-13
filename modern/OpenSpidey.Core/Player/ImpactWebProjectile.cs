using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public sealed record ImpactWebEnvironmentContact(object Identity, ushort ItemFlags, uint FaceFlags,
    FixedVector3 Position, ActorAngles Normal, int Distance);
public readonly record struct ImpactWebRingRequest(FixedVector3 Position, byte Red, byte Green, byte Blue, int Size, int Growth);
public readonly record struct ImpactWebFragmentRequest(int GroundY, FixedVector3 Previous, FixedVector3 FirstHook,
    FixedVector3 SecondHook, FixedVector3 OwnerPosition, int Speed, int Jitter);

public interface IImpactWebHost
{
    int ScaleDamage(int damage); // Return unchanged damage when G_MECHLIST is absent.
    // Constructor: source zoned mode 0 with G_LINE_OF_SIGHT_CHECK set.
    ImpactWebEnvironmentContact? TraceInitialEnvironment(FixedVector3 start, FixedVector3 end);
    // Utils_CheckObjectCollision: source ordered baddy spheres(scale4096), then environmental LOS traversal.
    object? CheckObjectCollision(FixedVector3 start, FixedVector3 end);
    void SetPlayerFirstContact(); // Includes source +534=360,+52C=(+528+11)<<10, when player exists.
    void HitBody(object identity, ActorHit hit);
    void PlayPositionSound(ushort sound, FixedVector3 position, int pitch);
    void CreateSplat(FixedVector3 position, ActorAngles normal);
    void FlickEligibleSwitch(object environmentalIdentity); // Resolve switch; Flick only when source +100 !=0.
    void CreateImpactRing(ImpactWebRingRequest request);
    int TraceGroundY(FixedVector3 start, FixedVector3 end); // Source zoned mode1, return end.Y on miss.
    bool LowMemory { get; }
    void CreateWebFragment(ImpactWebFragmentRequest request, Func<int, int> random);
}

/// <summary>Original CImpactWeb shot: cached environment lifetime and actual per-Move body collisions.</summary>
public sealed class ImpactWebProjectile
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
    public ImpactWebEnvironmentContact? EnvironmentContact { get; }
    public bool Alive { get; private set; } = true;
    public int AnimationIndex => 9;
    public int Frame => 0;
    public uint PostScale => 0x0A001000;

    public ImpactWebProjectile(FixedVector3 position, ActorAngles direction, int speed, int damage, int lifetime,
        uint clock, IImpactWebHost host, Func<int, int> random)
    {
        if (speed == 0) throw new ArgumentOutOfRangeException(nameof(speed), "Original impact-web lifetime divides by shot speed.");
        Damage = host.ScaleDamage(damage);
        Position = position;
        LastClock = clock;
        Velocity = FixedAim.Velocity(speed, new(direction.X, direction.Y));
        ushort duration = unchecked((ushort)lifetime);
        EnvironmentContact = host.TraceInitialEnvironment(position, position + Velocity * duration);
        if (EnvironmentContact is not null) duration = unchecked((ushort)(EnvironmentContact.Distance / speed));
        if (duration > lifetime) duration = unchecked((ushort)lifetime);
        Lifetime = duration;
        AngularVelocity = random(2) != 0 ? (short)768 : (short)-768;
        // Original missed-ray item/face pointers were uninitialized. Managed absence is explicit.
    }

    public void Advance(uint clock, IImpactWebHost host, Func<int, int> random)
    {
        if (!Alive) return;
        Scale = unchecked((short)(Scale + 150));
        if (Scale > 500) Scale = 500;
        Angle = unchecked((short)(Angle + AngularVelocity));
        FixedVector3 previous = Position;
        int elapsed = unchecked((int)(clock - LastClock));
        LastClock = clock;
        Position += Velocity * elapsed;
        FixedVector3 impact = previous;
        object? body = host.CheckObjectCollision(previous, Position);
        int mode = 0;
        if (body is not null) mode = 2;
        else
        {
            Age = unchecked((short)(Age + elapsed));
            if (Age >= Lifetime) mode = EnvironmentContact is null ? 1 : 3;
        }
        if (mode == 3)
        {
            ImpactWebEnvironmentContact contact = EnvironmentContact!;
            host.PlayPositionSound(16, Position, 0);
            impact = contact.Position + new FixedVector3(contact.Normal.X, contact.Normal.Y, contact.Normal.Z) * 10;
            host.CreateSplat(impact, contact.Normal);
            if ((contact.ItemFlags & 1) == 0 && (contact.FaceFlags & 0x2000000) != 0)
                host.FlickEligibleSwitch(contact.Identity);
        }
        else if (mode == 2)
        {
            host.SetPlayerFirstContact();
            FixedVector3 direction = FixedVectorMath.Normalize(new(Velocity.X >> 12, 0, Velocity.Z >> 12));
            host.HitBody(body!, new(6, 0, 30, unchecked((ushort)Damage), direction, 300, 12));
            host.PlayPositionSound(16, Position, 0);
        }
        if (mode == 0) return;
        host.CreateImpactRing(new(impact, 200, 210, 255, 3216, 316));
        if (mode == 2)
        {
            int ground = host.TraceGroundY(impact, impact + new FixedVector3(0, 0x1388000, 0));
            for (int i = 0; i < 30; i++)
            {
                if (host.LowMemory) break;
                var first = new FixedVector3(unchecked((random(21) - 10) << 12), unchecked((random(21) - 10) << 12), unchecked((random(21) - 10) << 12));
                var second = new FixedVector3(unchecked((random(21) - 10) << 12), unchecked((random(21) - 10) << 12), unchecked((random(21) - 10) << 12));
                host.CreateWebFragment(new(ground, impact, first + impact, second + impact, impact, 25, 1), random);
            }
        }
        Alive = false;
    }
}
