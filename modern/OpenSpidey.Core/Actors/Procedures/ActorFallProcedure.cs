namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Supplies the actor's recovered field_308 landing height.</summary>
public interface IActorFallHost
{
    int LandingY { get; }
}

/// <summary>Recovered CAIProc_Fall landing detection and completion signals.</summary>
public sealed class ActorFallProcedure : IActorProcedure
{
    private readonly ActorProcedureState actor;
    private readonly IActorFallHost host;
    public int SourceType => 770;
    public int Flags { get; set; }
    public uint CompletionMask { get; private set; }
    public Action? ExecuteNext { get; set; }

    public ActorFallProcedure(ActorProcedureState actor, IActorFallHost host, uint completionMask)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(host);
        this.actor = actor;
        this.host = host;
        CompletionMask = completionMask;
        actor.CompletionFlags &= ~completionMask;
    }

    public void Execute(int elapsedTicks)
    {
        if ((actor.BehaviorFlags & 0x400) != 0)
        {
            int landingY = host.LandingY;
            if (actor.Body.Position.Y >= landingY)
            {
                actor.Body.Position = actor.Body.Position with { Y = landingY };
                if (CompletionMask != 0)
                {
                    actor.CompletionFlags |= CompletionMask;
                    CompletionMask &= 0xF0F0;
                }
                Flags |= 1;
            }
        }
        else Flags |= 1;
        ExecuteNext?.Invoke();
    }
}
