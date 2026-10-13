using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugTorsoAimHost
{
    ThugPlayerState Player { get; }
    int AttackClock { get; }
    bool HasJoints { get; }
    short JointOneRoll { get; set; }
    ushort ItemFlags { get; set; }
    void ApplyPoseForType(int type);
}

/// <summary>Source local joint1 Z correction, unsigned aim ratio and asymmetric smoothing.</summary>
public static class ThugTorsoAim
{
    public static void Step(ThugState state, ActorProcedureState actor, FixedVector3 origin, IThugTorsoAimHost host)
    {
        int height = unchecked(host.Player.Position.Y - origin.Y) >> 12;
        int angle = 0;
        if (height != 0)
        {
            uint distance = ApproximateDistance(host.Player.Position, origin);
            int horizontalDistance = ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, 1);
            // Source denominator is u32; x86 uses DIV rather than IDIV here.
            int ratio = unchecked((int)(unchecked((uint)(horizontalDistance << 12)) / distance));
            angle = ArcCos(ratio);
            if (height < 0) angle = unchecked(-angle);
        }
        if (!host.HasJoints)
        {
            host.ItemFlags |= 4;
            host.ApplyPoseForType(state.Type);
            return;
        }
        int change = unchecked(angle - host.JointOneRoll);
        if (change > 8) change >>= 3;
        host.JointOneRoll = unchecked((short)(host.JointOneRoll + change));
        if (host.JointOneRoll != 0)
        {
            host.ItemFlags |= 4;
            host.ApplyPoseForType(state.Type);
        }
        else host.ItemFlags &= unchecked((ushort)~4);
    }

    public static int ArcCos(int value)
    {
        float input = value;
        float result = (float)Math.Acos((double)input / 4096.0);
        // PC stores acos to f32, then keeps the multiply/divide extended until _ftol.
        double angle = (double)result * 2048.0 / (double)3.1415927f;
        // Invalid x87 _ftol returns INT64_MIN; the original retains its zero low word.
        return double.IsFinite(angle) ? (int)angle : 0;
    }

    private static uint ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = GameWord.Absolute(unchecked(first.X - second.X));
        int middle = GameWord.Absolute(unchecked(first.Y - second.Y));
        int smallest = GameWord.Absolute(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }
}
