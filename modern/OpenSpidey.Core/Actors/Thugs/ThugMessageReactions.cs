using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugMessageReactionHost : IActorMotionHost
{
    FixedVector3 PlayerPosition { get; }
    int PlayerUnavailableFlag { get; }
    uint WaypointFlags { get; set; }
    bool TryGetMessageSenderPosition(ThugMessage message, out FixedVector3 position);
    /// <summary>Source Utils_LineOfSight(from,to,0,null), without a path-clearance query.</summary>
    bool MessageLineOfSight(FixedVector3 from, FixedVector3 to);
    void StoreHostageHandle(object? senderHandle);
    void TrySendToMessageSender(ThugMessage message, int code);
    void SendSelfMessage(int code);
    bool AddPointToPath(FixedVector3 point, int distanceLimit);
    ActorPathCheckResult CheckMessagePath(FixedVector3 from, FixedVector3 to, bool wantHitPoint,
        int clearance, FixedVector3 initialHit);
}

/// <summary>Source message-triggered hostage ownership and action path selection.</summary>
public static class ThugMessageReactions
{
    public static void GetReadyToShootHostage(ThugState state, ActorProcedureState actor,
        ActorProcedureList procedures, ThugMessage message, IThugMessageReactionHost host)
    {
        if (!host.TryGetMessageSenderPosition(message, out FixedVector3 position)) return;
        if (host.MessageLineOfSight(actor.Body.Position, position))
        {
            host.StoreHostageHandle(message.SenderHandle);
            state.State = 8;
            state.Substate = 0;
            host.Neutralize();
            procedures.Attach(new ThugStateSwitchMessageProcedure(state, 16, host.SendSelfMessage));
        }
        else host.TrySendToMessageSender(message, 1);
        message.Flags |= 1;
    }

    public static void RunToWhereTheActionIs(ThugState state, ActorProcedureState actor,
        FixedVector3 target, IThugMessageReactionHost host)
    {
        FixedVector3 position = actor.Body.Position;
        if (ApproximateDistance(position, target) > 3500) return;
        if (!host.AddPointToPath(position, 0)) return;

        FixedVector3 farPoint = new(
            unchecked(target.X + (unchecked(target.X - position.X) <= 0 ? 409600 : -409600)),
            position.Y,
            unchecked(target.Z + (unchecked(target.Z - position.Z) <= 0 ? 409600 : -409600)));
        bool added;
        if (host.PlayerUnavailableFlag == 0 &&
            host.CheckMessagePath(position, host.PlayerPosition, false, 55, default).Code == 0 &&
            host.AddPointToPath(host.PlayerPosition, 0))
            added = true;
        else
        {
            // Source passes &farPoint as both destination and hit output. A path
            // result can replace it before the subsequent insertion/retreat test.
            ActorPathCheckResult path = host.CheckMessagePath(position, farPoint, true, 55, farPoint);
            farPoint = path.HitPoint;
            if (path.Code == 0)
            {
                if (!host.AddPointToPath(farPoint, 0)) return;
                added = true;
            }
            else added = path.Code == 2 && TryAddingCollidePointToPath(actor, farPoint, host);
        }

        if (!added) return;
        host.Neutralize();
        host.WaypointFlags |= 1;
        actor.BehaviorFlags &= ~0x10000000u;
        state.AlertTicks = 60;
        state.State = 2;
        state.Substate = 0;
    }

    public static bool TryAddingCollidePointToPath(ActorProcedureState actor,
        FixedVector3 point, IThugMessageReactionHost host)
    {
        if (ApproximateDistance(point, actor.Body.Position) < 100) return false;
        FixedVector3 candidate = ((point - actor.Body.Position) >> 12) * 3700 + actor.Body.Position;
        return host.AddPointToPath(candidate, 0);
    }

    private static uint ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = ThugPerception.SourceAbs(unchecked(first.X - second.X));
        int middle = ThugPerception.SourceAbs(unchecked(first.Y - second.Y));
        int smallest = ThugPerception.SourceAbs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }
}
