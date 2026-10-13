using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

/// <summary>Original CSpark pixel: cone RNG, fixed velocity, signed age and wrapping byte fade.</summary>
public sealed class SparkParticle
{
    public FixedVector3 Position { get; set; }
    public FixedVector3 Velocity { get; set; }
    public FixedVector3 Acceleration { get; set; }
    public FixedVector3 Friction { get; set; } = new(1, 1, 1);
    public short Age { get; set; }
    public ushort Lifetime { get; set; }
    public byte Code { get; set; }
    public uint Tag { get; set; }
    public uint WidthHeight { get; set; }
    public byte Red { get; set; }
    public byte Green { get; set; }
    public byte Blue { get; set; }
    public byte FadeRed { get; set; }
    public byte FadeGreen { get; set; }
    public byte FadeBlue { get; set; }
    public bool Alive { get; set; } = true;
    public int DisplayWidth => (int)(WidthHeight & 15);

    public SparkParticle(SparkSettings settings, SparkSpawnRequest request, Func<int, int> random,
        Action<SparkParticle>? allocated = null)
    {
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(random);
        // CBit/CPixel attaches before CSpark's first RNG call; the actual native owner supplies this boundary.
        allocated?.Invoke(this);
        (Code, Tag, WidthHeight) = (settings.Size & 15) == 1
            ? ((byte)104, 0x02000000u, 1u) : ((byte)96, 0x03000000u, settings.Size);
        Red = settings.Red; Green = settings.Green; Blue = settings.Blue;
        FadeRed = settings.FadeRed; FadeGreen = settings.FadeGreen; FadeBlue = settings.FadeBlue;
        Position = request.Position;
        ActorAngles trajectory = settings.Trajectory;
        ActorAngles direction = new(Angle(trajectory.X, settings.Cone.X),
            Angle(trajectory.Y, settings.Cone.Y), Angle(trajectory.Z, settings.Cone.Z));
        Velocity = FixedAim.Velocity(request.Speed, new(direction.X, direction.Y));
        Acceleration = new(0, request.VerticalAcceleration, 0);
        Friction = new(3, 3, 3);
        Lifetime = unchecked((ushort)random(request.MaximumLifetime));
        if (settings.SemiTransparent) Code |= 2;

        short Angle(short trajectory, short cone) => cone == 0 ? trajectory :
            unchecked((short)(trajectory + random(cone) - (cone >> 1)));
    }

    public void AdvanceBits()
    {
        Position += Velocity;
        FixedVector3 accelerated = Velocity + Acceleration;
        Velocity = new(Damp(accelerated.X), Damp(accelerated.Y), Damp(accelerated.Z));
        Age = unchecked((short)(Age + 1));
        if (Age >= Lifetime) Alive = false;
        Red = unchecked((byte)(Red - FadeRed));
        Green = unchecked((byte)(Green - FadeGreen));
        Blue = unchecked((byte)(Blue - FadeBlue));
    }

    private static int Damp(int velocity) => unchecked(velocity - (velocity >> 3));
}
