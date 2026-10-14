using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Scorpion;

/// <summary>Damage/target fields composed with the encounter body and its canonical procedure owner.</summary>
public sealed class ScorpionDamageState
{
    public ScorpionState Body { get; }
    public ActorProcedureState Actor { get; }
    public ActorProcedureList Procedures { get; }
    public ScorpionStateFlags StateFlags { get; } = new(); // Source signed16 cache 0x314.
    public int ReactionContext { get; set; } // 0x318.
    public uint ReactionFlags { get; set; } // 0x324, distinct from procedure completion flags 0x288.
    public int Field310 { get; set; } // The source recovery clears this field; its wider role is not established.
    public int SlideUpdates { get => Actor.Body.LimitedMotionTicks; set => Actor.Body.LimitedMotionTicks = value; } // 0x1F8.
    public int HitFlashUpdates { get; set; } // 0x3EC, also selects the health-bar portrait.
    public int TargetKind { get; set; } // 0xBF8: 2 is the player.
    public int PlayerTargetUpdates { get; set; } // 0xC10.
    public object? CurrentTargetHandle { get; set; } // Weak source handle 0xBF0.
    public object? CachedJonah { get; set; } // Raw source identity 0xBEC, not the weak current-target handle.
    public int LastFireQuip { get; set; } // 0xBE0.
    public int FireQuipSuppressionUpdates { get; set; } // 0xC20.

    public ScorpionDamageState(ScorpionState body, ActorProcedureState actor, ActorProcedureList procedures)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(procedures);
        body.BindProcedures(actor);
        Body = body;
        Actor = actor;
        Procedures = procedures;
    }
}
