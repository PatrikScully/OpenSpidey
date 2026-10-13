using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

public sealed class RecordedActorMotion
{
    public int ElapsedTicks { get; set; }
    public int TotalTicks { get; set; }
    public FixedVector3 StartPosition { get; set; }
    public FixedVector3 EndPosition { get; set; }
    public FixedVector3 VelocityPerTick { get; set; }
    public ActorAngles StartAngles { get; set; }
    public ActorAngles EndAngles { get; set; }
    public ActorAngles AngularVelocityPerTick { get; set; }
}

/// <summary>Recovered shared CBaddy physics, independent of AI, terrain queries and rendering.</summary>
public sealed class ActorBodyMotion
{
    public FixedVector3 Position { get; set; }
    public FixedVector3 Velocity { get; set; }
    public FixedVector3 Acceleration { get; set; }
    public FixedVector3 LocalVelocity { get; set; }
    public ActorAngles Angles { get; set; }
    public ActorAngles AngularVelocity { get; set; }
    public ActorAngles AngularAcceleration { get; set; }
    public AxisDamping LinearDamping { get; set; }
    public AxisDamping AngularDamping { get; set; }
    public bool UseLocalVelocity { get; set; }
    public uint MovementFlags { get; set; }
    public ushort CollisionFlags { get; set; }
    public int ScriptWaitTicks { get; set; }
    public int LimitedMotionTicks { get; set; }
    public RecordedActorMotion Recorded { get; } = new();

    public void Integrate(int ticks, bool limitElapsed = false)
    {
        if (Recorded.TotalTicks != 0)
        {
            Recorded.ElapsedTicks = unchecked(Recorded.ElapsedTicks + ticks);
            if ((uint)Recorded.ElapsedTicks < (uint)Recorded.TotalTicks && ScriptWaitTicks != 0)
            {
                Position = Recorded.StartPosition + Recorded.VelocityPerTick * Recorded.ElapsedTicks;
                Angles = (Recorded.StartAngles + Multiply(Recorded.AngularVelocityPerTick, Recorded.ElapsedTicks)).Mask();
            }
            else
            {
                Recorded.ElapsedTicks = 0;
                Recorded.TotalTicks = 0;
                ScriptWaitTicks = 0;
                Position = Recorded.EndPosition;
                Angles = Recorded.EndAngles;
            }
            return;
        }
        if (Recorded.ElapsedTicks != 0)
        {
            if ((uint)ticks < (uint)Recorded.ElapsedTicks && ScriptWaitTicks != 0)
                Recorded.ElapsedTicks = unchecked(Recorded.ElapsedTicks - ticks);
            else { Recorded.ElapsedTicks = 0; ScriptWaitTicks = 0; }
            return;
        }
        CollisionFlags = 0;
        int elapsed = ticks;
        if (limitElapsed)
        {
            elapsed = Math.Min(LimitedMotionTicks, ticks);
            if (elapsed == 0) return;
            LimitedMotionTicks = unchecked(LimitedMotionTicks - elapsed);
        }
        if (UseLocalVelocity)
        {
            if ((MovementFlags & 1) != 0) { MovementFlags = 0; return; }
            LocalVelocity += Acceleration;
            FixedJointTransform rotation = FixedJointTransform.FromJoint(new(Angles.X, Angles.Y, Angles.Z, 0, 0, 0));
            Angles = (Angles + AngularVelocity).Mask();
            FixedVector3 rotated = FixedRotation.Transform(rotation, LocalVelocity >> 6) << 6;
            Position += rotated * elapsed;
            Position += Velocity * elapsed;
        }
        else
        {
            Velocity = KillSmall(Damp(Velocity + Acceleration, LinearDamping));
            Position += Velocity * elapsed;
            Angles = (Angles + Multiply(AngularVelocity, elapsed)).Mask();
            ActorAngles angular = AngularVelocity + AngularAcceleration;
            AngularVelocity = new ActorAngles((short)Damp(angular.X, AngularDamping.X),
                (short)Damp(angular.Y, AngularDamping.Y), (short)Damp(angular.Z, AngularDamping.Z)).KillSmall();
        }
        MovementFlags = 0;
    }

    private static ActorAngles Multiply(ActorAngles angles, int amount) => new(unchecked((short)(angles.X * amount)),
        unchecked((short)(angles.Y * amount)), unchecked((short)(angles.Z * amount)));
    private static FixedVector3 Damp(FixedVector3 vector, AxisDamping damping) => new(Damp(vector.X, damping.X),
        Damp(vector.Y, damping.Y), Damp(vector.Z, damping.Z));
    private static int Damp(int value, byte shift) => unchecked(value - (value >> shift));
    private static FixedVector3 KillSmall(FixedVector3 vector) => new(Small(vector.X), Small(vector.Y), Small(vector.Z));
    private static int Small(int value) => value is >= -2048 and <= 2048 ? 0 : value;
}
