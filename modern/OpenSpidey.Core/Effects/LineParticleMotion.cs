using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Source CGLineParticle clock, ballistic motion and one-update trail.</summary>
public sealed class LineParticleMotion
{
    public FixedVector3 Head { get; private set; }
    public FixedVector3 Tail { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public int VerticalAcceleration { get; }
    public ushort RemainingTicks { get; private set; }
    public uint LastClock { get; private set; }
    public bool Alive { get; private set; } = true;

    public LineParticleMotion(FixedVector3 position, FixedVector3 velocity, ushort lifetime,
        int verticalAcceleration, uint clock)
    {
        Head = Tail = position;
        Velocity = velocity;
        RemainingTicks = lifetime;
        VerticalAcceleration = verticalAcceleration;
        LastClock = clock;
    }

    public void Advance(uint clock)
    {
        if (!Alive) return;
        uint elapsed = unchecked(clock - LastClock);
        if (RemainingTicks < elapsed)
        {
            Alive = false;
            return;
        }
        RemainingTicks = unchecked((ushort)(RemainingTicks - elapsed));
        LastClock = clock;
        Tail = Head;
        Velocity = Velocity with { Y = unchecked(Velocity.Y + VerticalAcceleration) };
        Head += Velocity * unchecked((int)elapsed);
    }
}
