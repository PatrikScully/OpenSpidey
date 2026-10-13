namespace OpenSpidey.Core.Actors;

/// <summary>Plays a source sound at the actor's current position with the default volume.</summary>
public interface IActorSoundHost
{
    void PlaySound(ushort sound);
}
