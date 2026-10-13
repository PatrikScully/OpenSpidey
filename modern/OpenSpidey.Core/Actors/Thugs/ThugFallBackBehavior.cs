using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugFallBackHost
{
    ActorPathCheckResult PathCheck(FixedVector3 start, FixedVector3 end, bool wantHitPoint, int groundRange);
}

/// <summary>Original75-unit forward/backward ground-gap probe and four-invocation recoil turn.</summary>
public static class ThugFallBackBehavior
{
    public static void Check(ActorProcedureState actor, ActorProcedureList procedures, IThugFallBackHost host)
    {
        int forward = ((actor.BehaviorFlags & 0x10) != 0 ? -75 : 75) << 12;
        (short sine, short cosine) = GameAngle.Heading(actor.Body.Angles.Y);
        FixedVector3 offset = new(unchecked((forward >> 3) * sine) >> 9, 0, unchecked((forward >> 3) * cosine) >> 9);
        if (host.PathCheck(actor.Body.Position, actor.Body.Position + offset, false, 55).Code == 2)
            procedures.Attach(new ActorYawRotationProcedure(actor, 2047, 4, 0));
    }
}
