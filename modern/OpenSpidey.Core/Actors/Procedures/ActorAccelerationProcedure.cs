namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Recovered CAIProc_AccZ local forward acceleration and target-speed clamp.</summary>
public sealed class ActorAccelerationProcedure : IActorProcedure
{
    private readonly ActorProcedureState actor;
    public int SourceType => 1025;
    public int Flags { get; set; }
    public int WaitTicks { get; set; }
    public int Acceleration { get; }
    public int TargetSpeed { get; }
    public uint CompletionMask { get; private set; }
    public Action? ExecuteNext { get; set; }

    public ActorAccelerationProcedure(ActorProcedureState actor, int acceleration, int targetSpeed, uint completionMask)
    {
        ArgumentNullException.ThrowIfNull(actor);
        this.actor = actor;
        CompletionMask = completionMask;
        actor.CompletionFlags &= ~completionMask;
        TargetSpeed = unchecked((targetSpeed >> 4) << 12);
        if (TargetSpeed != 0) actor.BehaviorFlags |= 0x80000000;
        int amount = unchecked((acceleration >> 4) << 12);
        int magnitude = amount < 0 ? unchecked(-amount) : amount;
        Acceleration = TargetSpeed > actor.Body.LocalVelocity.Z ? magnitude : unchecked(-magnitude);
    }

    public void Execute(int elapsedTicks)
    {
        int wait = Wait();
        if (wait != 0)
        {
            if (wait == 2) actor.Body.Acceleration = actor.Body.Acceleration with { Z = 0 };
        }
        else
        {
            int nextSpeed = unchecked(actor.Body.LocalVelocity.Z + Acceleration);
            bool reached = Acceleration >= 0 ? TargetSpeed <= nextSpeed : TargetSpeed >= nextSpeed;
            if (reached)
            {
                actor.Body.LocalVelocity = actor.Body.LocalVelocity with { Z = TargetSpeed };
                if (TargetSpeed == 0) actor.BehaviorFlags &= 0xF7FFFFFF;
                actor.Body.Acceleration = actor.Body.Acceleration with { Z = 0 };
                if (CompletionMask != 0)
                {
                    actor.CompletionFlags |= CompletionMask;
                    CompletionMask &= 0xF0F0;
                }
                Flags |= 1;
            }
            else actor.Body.Acceleration = actor.Body.Acceleration with { Z = Acceleration };
        }
        ExecuteNext?.Invoke();
    }

    private int Wait()
    {
        if ((Flags & 3) != 0) return 1;
        if ((Flags & 4) != 0) { Flags |= 1; return 2; }
        int remaining = WaitTicks;
        if (remaining > 0) WaitTicks = unchecked(remaining - 1);
        return remaining > 0 ? remaining : 0;
    }
}
