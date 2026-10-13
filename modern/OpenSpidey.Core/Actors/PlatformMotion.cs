using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

/// <summary>Recovered CPlatform translation, angular integration, destination and scale state.</summary>
public sealed class PlatformMotion
{
    private readonly ushort[] scaleTicks = new ushort[3];
    private readonly short[] scaleRates = new short[3];
    private readonly short[] scale = new short[3];
    private FixedVector3 destinationDirection;

    public PlatformMotion(ActorBodyMotion? body = null)
    {
        Body = body ?? new();
        Scale = Array.AsReadOnly(scale);
        ScaleTicks = Array.AsReadOnly(scaleTicks);
        ScaleRates = Array.AsReadOnly(scaleRates);
    }

    public ActorBodyMotion Body { get; }
    public FixedVector3 Position { get => Body.Position; set => Body.Position = value; }
    public FixedVector3 Velocity { get => Body.Velocity; set => Body.Velocity = value; }
    public FixedVector3 Acceleration { get => Body.Acceleration; set => Body.Acceleration = value; }
    public ActorAngles Angles { get => Body.Angles; set => Body.Angles = value; }
    public ActorAngles AngularVelocity { get => Body.AngularVelocity; set => Body.AngularVelocity = value; }
    public ActorAngles AngularAcceleration { get => Body.AngularAcceleration; set => Body.AngularAcceleration = value; }
    public FixedVector3 Destination { get; private set; }
    public bool MovingToDestination { get; private set; }
    public bool ScalingEnabled { get; private set; }
    public IReadOnlyList<short> Scale { get; }
    public IReadOnlyList<ushort> ScaleTicks { get; }
    public IReadOnlyList<short> ScaleRates { get; }

    public void MoveTo(FixedVector3 target)
    {
        if (Destination == target) return;
        Destination = target;
        destinationDirection = (Position - target) >> 12;
        MovingToDestination = true;
    }

    public void UpdateDestination(int speed)
    {
        if (!MovingToDestination) return;
        FixedVector3 delta = (Position - Destination) >> 12;
        if (unchecked(destinationDirection.X * delta.X + destinationDirection.Y * delta.Y + destinationDirection.Z * delta.Z) < 0)
            Position = Destination;
        if (FixedAim.Distance(Destination, Position) > speed)
            Velocity = FixedAim.Velocity(speed, FixedAim.Calculate(Position, Destination));
        else
        {
            Position = Destination;
            Velocity = default;
            MovingToDestination = false;
        }
    }

    public void Integrate(int ticks, short physicsMode = 0, int levelId = 0, bool recordedMotionActive = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        if (recordedMotionActive || (Body.Recorded.ElapsedTicks | Body.Recorded.TotalTicks) != 0)
        {
            Body.Integrate(ticks);
            return;
        }
        if (physicsMode == 0)
        {
            int step = levelId == 0x301 && ticks >= 4 ? 2 : 1;
            for (int index = 0; index < ticks; index += step) IntegrateOne();
        }
        else if (physicsMode == 3) IntegrateOne();
    }

    private void IntegrateOne()
    {
        Velocity = KillSmall(Velocity + Acceleration);
        Position += Velocity;
        Angles = (Angles + AngularVelocity).Mask();
        AngularVelocity = (AngularVelocity + AngularAcceleration).KillSmall();
    }

    public void ChangeScale(int axis, short percent, short duration)
    {
        if (axis is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(axis));
        if (!ScalingEnabled)
        {
            ScalingEnabled = true;
            Array.Fill(scale, (short)4096);
        }
        scaleTicks[axis] = unchecked((ushort)duration);
        int divisor = duration == 0 ? 100 : 100 * duration;
        scaleRates[axis] = unchecked((short)((percent << 12) / divisor));
        scale[axis] = unchecked((short)(scale[axis] + scaleRates[axis]));
    }

    public void AdvanceScale(int ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ticks);
        int half = ticks >> 1;
        for (int axis = 0; axis < 3; axis++)
        {
            if (scaleTicks[axis] == 0) continue;
            int amount = Math.Min(scaleTicks[axis], half);
            scaleTicks[axis] = (ushort)(scaleTicks[axis] - amount);
            scale[axis] = unchecked((short)(scale[axis] + scaleRates[axis] * amount));
        }
    }

    private static FixedVector3 KillSmall(FixedVector3 vector) => new(Small(vector.X), Small(vector.Y), Small(vector.Z));
    private static int Small(int value) => value is >= -2048 and <= 2048 ? 0 : value;
}
