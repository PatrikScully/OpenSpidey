namespace OpenSpidey.Core.Actors;

public interface IActorAnimationCycleHost
{
    void CycleAnimation(ushort clip, sbyte direction = 1);
}
