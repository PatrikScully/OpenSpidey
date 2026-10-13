namespace OpenSpidey.Core.Actors.Procedures;

/// <summary>Shared body and signals exchanged by an actor's AI procedures.</summary>
public sealed class ActorProcedureState
{
    public ActorBodyMotion Body { get; }
    public uint CompletionFlags { get; set; }
    public uint BehaviorFlags { get; set; }

    public ActorProcedureState(ActorBodyMotion body)
    {
        ArgumentNullException.ThrowIfNull(body);
        Body = body;
    }

    public void Neutralize(ActorProcedureList procedures)
    {
        ArgumentNullException.ThrowIfNull(procedures);
        procedures.Mark(ordinary: true);
        Body.Acceleration = default;
        Body.Velocity = default;
        Body.AngularVelocity = default;
        Body.AngularAcceleration = default;
        Body.LocalVelocity = default;
        BehaviorFlags &= 0xB7FFFFFB;
    }
}
