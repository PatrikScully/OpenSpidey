namespace OpenSpidey.Core.Actors;

/// <summary>Clears motion and retires ordinary AI procedures through the actor's real adapter.</summary>
public interface IActorMotionHost
{
    void Neutralize();
}
