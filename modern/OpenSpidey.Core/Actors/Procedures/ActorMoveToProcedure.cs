using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Source waypoint movement, waiting for facing alignment before advancing.</summary>
public sealed class ActorMoveToProcedure : IActorProcedure
{
    private readonly ActorProcedureState actor;
    private readonly FixedVector3 destination;
    public FixedVector3 Velocity { get; private set; }
    public uint CompletionMask { get; private set; }
    public int SourceType => 1794;
    public int Flags { get; set; }
    public bool Started { get; private set; }
    public ActorLookAtProcedure? Facing { get; }
    public Action? ExecuteNext { get; set; }

    public ActorMoveToProcedure(ActorProcedureState actor, FixedVector3 destination, int speed,
        int turnRate, int facingTolerance, uint completionMask)
    {
        ArgumentNullException.ThrowIfNull(actor);
        this.actor = actor;
        // 0x4018D0 replaces the destination height with the body's before deriving velocity and facing.
        destination = destination with { Y = actor.Body.Position.Y };
        this.destination = destination;
        CompletionMask = completionMask;
        actor.CompletionFlags &= ~completionMask;
        FixedVector3 direction = (destination - actor.Body.Position) >> 12;
        if (direction == default) direction = new(1, 0, 0);
        Velocity = (FixedVectorMath.Normalize(direction) * speed) >> 4;
        if (turnRate != 0)
            Facing = ActorLookAtProcedure.ForPoint(actor,
                actor.Body.Position + ((destination - actor.Body.Position) << 2),
                0x10000, turnRate, facingTolerance);
        else actor.CompletionFlags |= 0x10000;
    }

    public void Execute(int elapsedTicks)
    {
        if (!Started && (actor.CompletionFlags & 0x10000) != 0)
        {
            actor.CompletionFlags &= ~0x10000u;
            Started = true;
        }
        if (Started)
        {
            FixedVector3 position = actor.Body.Position;
            int velocityX = Velocity.X, velocityZ = Velocity.Z;
            int x = AdvanceAxis(position.X, destination.X, elapsedTicks, ref velocityX);
            int z = AdvanceAxis(position.Z, destination.Z, elapsedTicks, ref velocityZ);
            actor.Body.Position = new(x, position.Y, z);
            Velocity = new(velocityX, Velocity.Y, velocityZ);
            if ((velocityX | velocityZ) == 0)
            {
                actor.BehaviorFlags &= 0xF7FFFFFF;
                actor.Body.MovementFlags |= 0x80000;
                if (CompletionMask != 0)
                {
                    actor.CompletionFlags |= CompletionMask;
                    CompletionMask &= 0xF0F0;
                }
                Flags |= 1;
            }
            else
            {
                actor.BehaviorFlags |= 0x08000000;
                actor.Body.MovementFlags |= 0x40000;
            }
        }
        ExecuteNext?.Invoke();
    }

    private static int AdvanceAxis(int current, int target, int elapsedTicks, ref int velocity)
    {
        if (velocity == 0) return current;
        int next = unchecked(current + elapsedTicks * velocity);
        if ((current > target) == (next > target)) return next;
        velocity = 0;
        return target;
    }
}
