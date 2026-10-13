using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Recovered CAIProc_LookAt point, yaw and recoverable body targets.</summary>
public sealed class ActorLookAtProcedure : IActorProcedure
{
    private readonly ActorProcedureState actor;
    private readonly FixedVector3 targetPoint;
    private readonly int targetYaw;
    private readonly Func<ActorBodyMotion?>? recoverBody;

    public int SourceType { get; }
    public int Flags { get; set; }
    public int WaitTicks { get; set; }
    public uint CompletionMask { get; private set; }
    public int Tolerance { get; }
    public int Rate { get; }
    public Action? ExecuteNext { get; set; }

    private ActorLookAtProcedure(ActorProcedureState actor, int sourceType, uint completionMask, int rate,
        int tolerance, FixedVector3 point = default, int yaw = 0, Func<ActorBodyMotion?>? body = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        this.actor = actor;
        SourceType = sourceType;
        CompletionMask = completionMask;
        targetPoint = point;
        targetYaw = yaw;
        recoverBody = body;
        Tolerance = Math.Max(16, SourceAbs(tolerance));
        Rate = SourceAbs(rate);
        actor.CompletionFlags &= ~completionMask;
        actor.BehaviorFlags &= 0xBFFFFFFF;
    }

    public static ActorLookAtProcedure ForPoint(ActorProcedureState actor, FixedVector3 target,
        uint completionMask, int rate, int tolerance) => new(actor, 258, completionMask, rate, tolerance, point: target);

    public static ActorLookAtProcedure ForYaw(ActorProcedureState actor, int yaw,
        uint completionMask, int rate, int tolerance) => new(actor, 259, completionMask, rate, tolerance, yaw: yaw);

    public static ActorLookAtProcedure ForBody(ActorProcedureState actor, Func<ActorBodyMotion?> recoverBody,
        uint completionMask, int rate, int tolerance)
    {
        ArgumentNullException.ThrowIfNull(recoverBody);
        return new(actor, 257, completionMask, rate, tolerance, body: recoverBody);
    }

    public void Execute()
    {
        if (Wait() == 0) Turn();
        ExecuteNext?.Invoke();
    }

    public void Execute(int elapsedTicks) => Execute();

    private void Turn()
    {
        int yaw;
        if (SourceType == 258)
        {
            if (targetPoint.X == actor.Body.Position.X && targetPoint.Z == actor.Body.Position.Z)
            {
                SignalCompletion();
                return;
            }
            yaw = FixedAim.Calculate(actor.Body.Position, targetPoint).Yaw;
        }
        else if (SourceType == 259) yaw = unchecked((short)targetYaw);
        else
        {
            ActorBodyMotion? body = recoverBody!();
            if (body is null)
            {
                SignalCompletion();
                Flags |= 1;
                return;
            }
            yaw = FixedAim.Calculate(actor.Body.Position, body.Position).Yaw;
        }

        int rate = (actor.BehaviorFlags & 0x40000004) != 0 ? 0 : Rate;
        int residual = ActorYawSteering.YawTowards(actor.Body, yaw, rate);
        if (residual < 0) actor.Body.MovementFlags |= 0x10000;
        else if (residual > 0) actor.Body.MovementFlags |= 0x20000;
        if (Tolerance >= SourceAbs(residual)) SignalCompletion();
    }

    private int Wait()
    {
        if ((Flags & 3) != 0) return 1;
        if ((Flags & 4) != 0) { Flags |= 1; return 2; }
        int remaining = WaitTicks;
        if (remaining > 0) WaitTicks = unchecked(remaining - 1);
        return remaining > 0 ? remaining : 0;
    }

    private void SignalCompletion()
    {
        if (CompletionMask == 0) return;
        actor.CompletionFlags |= CompletionMask;
        CompletionMask &= 0xF0F0;
    }

    private static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
