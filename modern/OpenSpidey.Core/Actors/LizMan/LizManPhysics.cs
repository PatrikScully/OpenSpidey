using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>Original CLizMan::DoLizmanPhysics (0x450920): CBody integration with anim-scaled roll.</summary>
public static class LizManPhysics
{
    public static FixedVector3 TrajectoryVector { get; set; }

    public static void Advance(ActorBodyMotion body, int animationId, int animationFrame, int elapsedTicks,
        out FixedVector3 trajectoryScratch)
    {
        ArgumentNullException.ThrowIfNull(body);
        trajectoryScratch = TrajectoryVector;
        body.CollisionFlags = 0;
        body.Velocity = KillSmall(Damp(body.Velocity + body.Acceleration, body.LinearDamping));
        int rollFactor = RollFactor(animationId, animationFrame);
        body.Position += ((body.Velocity * rollFactor) >> 12) * elapsedTicks;
        short angSpeed = unchecked((short)elapsedTicks);
        body.Angles = new ActorAngles(
            unchecked((short)(body.Angles.X + body.AngularVelocity.X * angSpeed)),
            unchecked((short)(body.Angles.Y + body.AngularVelocity.Y * angSpeed)),
            unchecked((short)(body.Angles.Z + body.AngularVelocity.Z * angSpeed))).Mask();
        ActorAngles angular = body.AngularVelocity + body.AngularAcceleration;
        body.AngularVelocity = new ActorAngles(
            (short)Damp(angular.X, body.AngularDamping.X),
            (short)Damp(angular.Y, body.AngularDamping.Y),
            (short)Damp(angular.Z, body.AngularDamping.Z)).KillSmall();
    }

    private static int RollFactor(int animationId, int animationFrame) => animationId switch
    {
        2 => (Abs(GameAngle.Heading((animationFrame << 7) & 0xFFF).Sine) >> 1) + 1024,
        8 => (Abs(GameAngle.Heading(((animationFrame << 12) / 20) & 0xFFF).Sine) >> 2) + 2048,
        0x23 => ((Abs(GameAngle.Heading(((animationFrame << 12) / 24) & 0xFFF).Sine) * 3) >> 2) + 1024,
        _ => 0x1000
    };

    private static FixedVector3 Damp(FixedVector3 vector, AxisDamping damping) =>
        new(Damp(vector.X, damping.X), Damp(vector.Y, damping.Y), Damp(vector.Z, damping.Z));
    private static int Damp(int value, byte shift) => unchecked(value - (value >> shift));
    private static FixedVector3 KillSmall(FixedVector3 vector) =>
        new(Small(vector.X), Small(vector.Y), Small(vector.Z));
    private static int Small(int value) => value is >= -2048 and <= 2048 ? 0 : value;
    private static int Abs(int value) => value < 0 ? unchecked(-value) : value;
}
