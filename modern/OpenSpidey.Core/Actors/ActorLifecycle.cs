using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors;

public enum ActorLifePhase { Active, Suspended, Dead, PendingDeletion }
public enum ActorUpdateAction { RunBehavior, Skip, Delete }

/// <summary>Recovered distance suspension and two-update dead-actor cleanup.</summary>
public sealed class ActorLifecycle(bool distanceSuspension, bool startsSuspended)
{
    public bool DistanceSuspension { get; } = distanceSuspension;
    private bool suspended = startsSuspended;
    private bool dead;
    private bool pendingDeletion;
    public ActorLifePhase Phase
    {
        get
        {
            if (suspended) return ActorLifePhase.Suspended;
            if (pendingDeletion) return ActorLifePhase.PendingDeletion;
            return dead ? ActorLifePhase.Dead : ActorLifePhase.Active;
        }
    }

    public ActorUpdateAction PrepareUpdate(uint distance, uint suspensionDistance)
    {
        if (suspended) return ActorUpdateAction.Skip;
        if (pendingDeletion) return ActorUpdateAction.Delete;
        if (dead) { pendingDeletion = true; return ActorUpdateAction.Skip; }
        if (DistanceSuspension && distance > suspensionDistance) { Suspend(); return ActorUpdateAction.Skip; }
        return ActorUpdateAction.RunBehavior;
    }

    public void Suspend() => suspended = true;
    public void Kill() => dead = true;
    public bool ResumeNearby(uint distance, uint suspensionDistance)
    {
        if (!suspended || !DistanceSuspension || distance > suspensionDistance) return false;
        suspended = false;
        return true;
    }

    public static uint Distance(FixedVector3 first, FixedVector3 second)
    {
        FixedVector3 delta = first - second;
        int largest = GameWord.Absolute(delta.X), middle = GameWord.Absolute(delta.Y), smallest = GameWord.Absolute(delta.Z);
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }
}
